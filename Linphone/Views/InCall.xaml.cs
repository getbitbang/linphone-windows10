/*
InCall.xaml.cs
Copyright (C) 2015  Belledonne Communications, Grenoble, France
This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using Linphone;
using Linphone.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Phone.Media.Devices;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Threading;

using GLuint = System.UInt32;
using OpenGlFunctions = System.IntPtr;

using EGLDisplay = System.IntPtr;
using EGLContext = System.IntPtr;
using EGLConfig = System.IntPtr;
using EGLSurface = System.IntPtr;
using EGLNativeDisplayType = System.IntPtr;
using EGLNativeWindowType = System.Object;
using glbool = System.Int32;

namespace Linphone.Views {
    public partial class InCall : Page {
        private DispatcherTimer oneSecondTimer;
        private Timer fadeTimer;
        private DateTimeOffset startTime;
        private Boolean askingVideo;
        private Call pausedCall;

        private bool statsVisible = false;

        private ApplicationViewOrientation displayOrientation;
        private DisplayInformation displayInformation;
        private SimpleOrientationSensor orientationSensor;
        private SimpleOrientation deviceOrientation;

        private readonly object popupLock = new Object();

        public InCall() {
            this.InitializeComponent();
            this.DataContext = new InCallModel();
            askingVideo = false;
//------------------------------------------------------------------------
            mMainOpenGLES = new OpenGLES();
            mPreviewOpenGLES = new OpenGLES();
            mMainRenderSurface = OpenGLES.EGL_NO_SURFACE;
            mPreviewRenderSurface = OpenGLES.EGL_NO_SURFACE;
            Loaded += OnPageLoaded;
            //------------------------------------------------------------------------
            if (LinphoneManager.Instance.IsVideoAvailable) {
                VideoGrid.Visibility = Visibility.Collapsed;
            }

                if (LinphoneManager.Instance.Core.CurrentCall.State == CallState.StreamsRunning)
                Status.Text = "00:00:00";

            displayOrientation = ApplicationView.GetForCurrentView().Orientation;
            displayInformation = DisplayInformation.GetForCurrentView();
            deviceOrientation = SimpleOrientation.NotRotated;
            orientationSensor = SimpleOrientationSensor.GetDefault();
            if (orientationSensor != null) {
                deviceOrientation = orientationSensor.GetCurrentOrientation();
                SetVideoOrientation();
                orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;
            }

            buttons.HangUpClick += buttons_HangUpClick;
            buttons.StatsClick += buttons_StatsClick;
            buttons.CameraClick += buttons_CameraClick;
            buttons.PauseClick += buttons_PauseClick;
            buttons.SpeakerClick += buttons_SpeakerClick;
            buttons.MuteClick += buttons_MuteClick;
            buttons.VideoClick += buttons_VideoClick;
            buttons.BluetoothClick += buttons_BluetoothClick;
            buttons.DialpadClick += buttons_DialpadClick;

            // Handling event when app will be suspended
            Application.Current.Suspending += new SuspendingEventHandler(App_Suspended);
            Application.Current.Resuming += new EventHandler<object>(App_Resumed);
            pausedCall = null;
        }

        #region Buttons
        private async void buttons_VideoClick(object sender, bool isVideoOn) {
            // Workaround to pop the camera permission window
            await openCameraPopup();

            Call call = LinphoneManager.Instance.Core.CurrentCall;
            CallParams param = call.CurrentParams.Copy();
            param.VideoEnabled = isVideoOn;
            call.Update(param);
        }

        private void buttons_MuteClick(object sender, bool isMuteOn) {
            LinphoneManager.Instance.Core.MicEnabled = isMuteOn;
        }

        private void buttons_BluetoothClick(object sender, bool isBluetoothOn) {
            try {
                LinphoneManager.Instance.BluetoothEnabled = isBluetoothOn;
            } catch {
                Debug.WriteLine("Exception while trying to toggle bluetooth to " + isBluetoothOn.ToString());
            }
        }

        private void buttons_DialpadClick(object sender, bool isBluetoothOn) {

        }

        private bool buttons_SpeakerClick(object sender, bool isSpeakerOn) {
            try {
                LinphoneManager.Instance.SpeakerEnabled = isSpeakerOn;
                return true;
            } catch {
                Debug.WriteLine("Exception while trying to toggle speaker to " + isSpeakerOn.ToString());
            }
            return false;
        }

        private void buttons_PauseClick(object sender, bool isPaused) {
            if (isPaused)
                LinphoneManager.Instance.PauseCurrentCall();
            else
                LinphoneManager.Instance.ResumeCurrentCall();
        }

        private void buttons_CameraClick(object sender) {
            LinphoneManager.Instance.ToggleCameras();
            if (LinphoneManager.Instance.Core.VideoDevice.Contains("Front")) {
                PreviewRender.ScaleX = -1;
            } else {
                PreviewRender.ScaleX = 1;
            }
        }

        private void buttons_StatsClick(object sender, bool areStatsVisible) {
            statsVisible = areStatsVisible;
        }

        private void buttons_HangUpClick(object sender) {
            LinphoneManager.Instance.EndCurrentCall();
        }
        #endregion

        protected override void OnNavigatedTo(NavigationEventArgs nee) {
            List<string> parameters;
            base.OnNavigatedTo(nee);
            parameters = nee.Parameter as List<string>;

            LinphoneManager.Instance.CallStateChangedEvent += CallStateChanged;

            if (parameters == null)
                return;

            if (parameters.Count >= 1 && parameters[0].Contains("sip")) {
                String calledNumber = parameters[0];
                Address address = LinphoneManager.Instance.Core.InterpretUrl(calledNumber);
                calledNumber = String.Format("{0}@{1}", address.Username, address.Domain);
                Contact.Text = calledNumber;

                if (calledNumber != null && calledNumber.Length > 0) {
                    // ContactManager cm = ContactManager.Instance;
                    // cm.ContactFound += cm_ContactFound;
                    // cm.FindContact(calledNumber);
                }
            }
            if (parameters.Count >= 2 && parameters[1].Contains("incomingCall")) {
                if (LinphoneManager.Instance.Core.CurrentCall != null) {
                    LinphoneManager.Instance.Core.CurrentCall.Accept();
                } else {
                    if (Frame.CanGoBack) {
                        Frame.GoBack();
                    }
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs nee) {
            StopVideoStream();
            if (oneSecondTimer != null) {
                oneSecondTimer.Start();
            }

            if (LinphoneManager.Instance.isMobileVersion()) {
                ToggleFullScreenMode(false);
            }
            /*if (fadeTimer != null)
            {
                fadeTimer.Dispose();
                fadeTimer = null;
            }*/
            Frame.BackStack.Clear();
            base.OnNavigatedFrom(nee);

            //LinphoneManager.Instance.CallStateChangedEvent -= CallStateChanged;
        }

        public void CallStateChanged(Call call, CallState state) {
            if (call == null)
                return;

            if (state == CallState.Connected && oneSecondTimer == null) {
                oneSecondTimer = new DispatcherTimer();
                oneSecondTimer.Interval = TimeSpan.FromSeconds(1);
                oneSecondTimer.Tick += timerTick;
                oneSecondTimer.Start();
                statusIcon.Visibility = Visibility.Visible;
                buttons.enabledVideo(false);
            } else if (state == CallState.Resuming) {
                oneSecondTimer = new DispatcherTimer();
                oneSecondTimer.Interval = TimeSpan.FromSeconds(1);
                oneSecondTimer.Tick += timerTick;
                oneSecondTimer.Start();
            } else if (state == CallState.StreamsRunning) {
                statusIcon.Glyph = "\uE768";
                if (!call.MediaInProgress()) {
                    buttons.enabledPause(true);
                    if (LinphoneManager.Instance.IsVideoAvailable) {
                        buttons.enabledVideo(true);
                    }
                }
                if (call.CurrentParams.VideoEnabled) {
                    displayVideo(true);
                    buttons.checkedVideo(true);
                } else {
                    displayVideo(false);
                    if (LinphoneManager.Instance.IsVideoAvailable)
                    {
                        buttons.enabledVideo(true);
                    }
                }
            } else if (state == CallState.PausedByRemote) {
                if (call.CurrentParams.VideoEnabled) {
                    displayVideo(false);
                }
                buttons.enabledVideo(false);
                statusIcon.Glyph = "\uE769";
            } else if (state == CallState.Paused) {
                if (call.CurrentParams.VideoEnabled) {
                    displayVideo(false);
                }
                buttons.enabledVideo(false);
                statusIcon.Glyph = "\uE769";
            } else if (state == CallState.Error || state == CallState.End) {
                if (oneSecondTimer != null) {
                    oneSecondTimer.Stop();
                }
            } else if (state == CallState.UpdatedByRemote) {
                if (!LinphoneManager.Instance.IsVideoAvailable) {
                    CallParams parameters = call.CurrentParams.Copy();
                    call.AcceptUpdate(parameters);
                } else {
                    bool remoteVideo = call.RemoteParams.VideoEnabled;
                    bool localVideo = call.CurrentParams.VideoEnabled;
                    bool autoAcceptCameraPolicy = LinphoneManager.Instance.Core.VideoActivationPolicy.AutomaticallyAccept;
                    if (remoteVideo && !localVideo && !autoAcceptCameraPolicy) {
                        lock (popupLock) {
                            if (askingVideo) return;
                            askingVideo = true;
                            AskVideoPopup(call);
                        }
                    }
                }
            }
            refreshUI();
        }

        private void refreshUI() {
            if (!LinphoneManager.Instance.IsVideoAvailable) {
                buttons.enabledVideo(false);
            } else {
                if (LinphoneManager.Instance.Core.CurrentCall != null && LinphoneManager.Instance.Core.CurrentCall.CurrentParams.VideoEnabled) {
                    buttons.checkedVideo(true);
                } else {
                    buttons.checkedVideo(false);
                    askingVideo = false;
                }
            }
        }

        private async Task openCameraPopup() {
            MediaCapture mediaCapture = new Windows.Media.Capture.MediaCapture();
            await mediaCapture.InitializeAsync();
            mediaCapture.Dispose();
        }

        public async void AskVideoPopup(Call call) {
            MessageDialog dialog = new MessageDialog(ResourceLoader.GetForCurrentView().GetString("VideoActivationPopupContent"), ResourceLoader.GetForCurrentView().GetString("VideoActivationPopupCaption"));
            dialog.Commands.Clear();
            dialog.Commands.Add(new UICommand { Label = ResourceLoader.GetForCurrentView().GetString("Accept"), Id = 0 });
            dialog.Commands.Add(new UICommand { Label = ResourceLoader.GetForCurrentView().GetString("Dismiss"), Id = 1 });

            var res = await dialog.ShowAsync();
            CallParams parameters = LinphoneManager.Instance.Core.CreateCallParams(call);
            if ((int)res.Id == 0) {
                // Workaround to pop the camera permission window
                await openCameraPopup();

                parameters.VideoEnabled = true;
            }
            call.AcceptUpdate(parameters);
        }

        #region Video
        private async void App_Suspended(Object sender, Windows.ApplicationModel.SuspendingEventArgs e) {
            var deferral = e.SuspendingOperation.GetDeferral();
            // Pause the call when the application is about to be in background
            if (LinphoneManager.Instance.Core.CurrentCall != null && LinphoneManager.Instance.Core.CurrentCall.State != CallState.Paused) {
                pausedCall = LinphoneManager.Instance.Core.CurrentCall;
                pausedCall.Pause();

                // Wait for the Call to pass from Pausing to Paused
                await Task.Delay(1000);
            }
            deferral.Complete();
        }

        private void App_Resumed(Object sender, Object e) {
            if (pausedCall != null && pausedCall.State == CallState.Paused) {
                pausedCall.Resume();
                pausedCall = null;
            }
        }

        private void AdaptVideoSize() {
            if (ActualWidth > 640) {
                VideoGrid.Width = 640;
            } else {
                VideoGrid.Width = ActualWidth;
            }
            VideoGrid.Height = VideoGrid.Width * 3 / 4;
            //PreviewSwapChainPanel.Width = VideoGrid.Width / 4;
            //PreviewSwapChainPanel.Height = VideoGrid.Height / 4;
        }

        private void Video_Tapped(object sender, TappedRoutedEventArgs e) {
            if (buttons.Visibility == Visibility.Visible) {
                buttons.Visibility = Visibility.Collapsed;
            } else {
                buttons.Visibility = Visibility.Visible;
            }
        }

        private void displayVideo(bool isVisible) {
            if (LinphoneManager.Instance.isMobileVersion()) {
                ToggleFullScreenMode(isVisible);
            }
            if (isVisible) {
                if (LinphoneManager.Instance.Core.VideoDevice.Contains("Front")) {
                    PreviewRender.ScaleX = -1;
                } else {
                    PreviewRender.ScaleX = 1;
                }

                buttons.Visibility = Visibility.Collapsed;
                VideoGrid.Visibility = Visibility.Visible;
                ContactHeader.Visibility = Visibility.Collapsed;
                
                StartVideoStream();


            } else {
                buttons.Visibility = Visibility.Visible;
                VideoGrid.Visibility = Visibility.Collapsed;
                ContactHeader.Visibility = Visibility.Visible;
            }
        }

//------------------------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------------------------


        private EGLSurface mMainRenderSurface;
        private EGLSurface mPreviewRenderSurface;
        private OpenGLES mMainOpenGLES;
        private OpenGLES mPreviewOpenGLES;

        private void CreateRenderSurface()
        {
            if (mMainOpenGLES != null && mMainRenderSurface == OpenGLES.EGL_NO_SURFACE)
            {
                // The app can configure the the SwapChainPanel which may boost performance. 
                mMainRenderSurface = mMainOpenGLES.CreateSurface(VideoSwapChainPanel);
            }
            /*
            if (mPreviewOpenGLES != null && mPreviewRenderSurface == OpenGLES.EGL_NO_SURFACE)
            {
                // The app can configure the the SwapChainPanel which may boost performance. 
                mPreviewRenderSurface = mPreviewOpenGLES.CreateSurface(PreviewSwapChainPanel);
            }*/
            
        }
        private void DestroyRenderSurface()
        {
            if (mMainOpenGLES == null)
            {
                mMainOpenGLES.DestroySurface(mMainRenderSurface);
            }
            mMainRenderSurface = OpenGLES.EGL_NO_SURFACE;
            if (mPreviewOpenGLES == null)
            {
                mPreviewOpenGLES.DestroySurface(mPreviewRenderSurface);
            }
            mPreviewRenderSurface = OpenGLES.EGL_NO_SURFACE;
        }

        public struct ContextInfo
        {
            public GLuint width;
            public GLuint height;

            public OpenGlFunctions functions;// OpenGlFunctions* functions;
        };
        //private MSWinRTVideo.SwapChainPanelSource _videoSource;
        //private MSWinRTVideo.SwapChainPanelSource _previewSource;

        
        private object mRenderSurfaceCriticalSection = new object();

        // Create a task for rendering that will be run on a background thread. 

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // The SwapChainPanel has been created and arranged in the page layout, so EGL can be initialized. 
            //CreateRenderSurface();
        }
        private void RenderLoop(IAsyncAction action)
        {
            lock (mRenderSurfaceCriticalSection)
            {
                CreateRenderSurface();
                //_videoSource = new MSWinRTVideo.SwapChainPanelSource();
                //_videoSource.Start(VideoSwapChainPanel);
                //_previewSource = new MSWinRTVideo.SwapChainPanelSource();
                //_previewSource.Start(PreviewSwapChainPanel);
                //mRenderSurface = mOpenGLES->CreateSurface(PreviewSwapChainPanel, nullptr, nullptr);
                //ContextInfo c, cp;
                //c.width = 100;
                //c.height = 100;
                //c.functions = IntPtr.Zero;
                //cp.width = 50;
                //cp.height = 50;
                //cp.functions = IntPtr.Zero;
                //IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(c));
                //IntPtr pntp = Marshal.AllocHGlobal(Marshal.SizeOf(cp));
                //Marshal.StructureToPtr(c, pnt, false);
                //Marshal.StructureToPtr(cp, pntp, false);
                //IntPtr context = -1;
                //IntPtr id = (IntPtr)(-1);
                //LinphoneManager.Instance.Core.NativeVideoWindowId = pnt;// context;
                //LinphoneManager.Instance.Core.NativePreviewWindowId = pntp;// context;
                //LinphoneManager.Instance.Core.CurrentCall.NativeVideoWindowId = pnt;// context;


                mMainOpenGLES.MakeCurrent(mMainRenderSurface);
                //mPreviewOpenGLES.MakeCurrent(mPreviewRenderSurface);
                var oldMainSize = mMainOpenGLES.GetSurfaceDimensions(mMainRenderSurface);
                //var oldPreviewSize = mPreviewOpenGLES.GetSurfaceDimensions(mPreviewRenderSurface);
                ContextInfo c;
                c.width = (GLuint)oldMainSize.Width;
                c.height = (GLuint)oldMainSize.Height;
                c.functions = IntPtr.Zero;
                IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(c));
                Marshal.StructureToPtr(c, pnt, false);
                //LinphoneManager.Instance.Core.CurrentCall.NativeVideoWindowId = pnt;
                //c.width = (GLuint)oldPreviewSize.Width;
                //c.height = (GLuint)oldPreviewSize.Height;
                //c.functions = IntPtr.Zero;
                //pnt = Marshal.AllocHGlobal(Marshal.SizeOf(c));
                //Marshal.StructureToPtr(c, pnt, false);
                LinphoneManager.Instance.Core.NativePreviewWindowId = pnt;
                while (action.Status == AsyncStatus.Started && LinphoneManager.Instance.Core.CurrentCall != null)
                {
                    var size = mMainOpenGLES.GetSurfaceDimensions(mMainRenderSurface);
                    
                    if (oldMainSize != size)
                    {
                        c.width = (GLuint)size.Width;
                        c.height = (GLuint)size.Height;
                        c.functions = IntPtr.Zero;
                        pnt = Marshal.AllocHGlobal(Marshal.SizeOf(c));
                        Marshal.StructureToPtr(c, pnt, false);
                        //LinphoneManager.Instance.Core.CurrentCall.NativeVideoWindowId = pnt;
                        LinphoneManager.Instance.Core.NativePreviewWindowId = pnt;
                        oldMainSize = size;
                    }
                    /*
                    size = mPreviewOpenGLES.GetSurfaceDimensions(mPreviewRenderSurface);
                    if (oldPreviewSize != size)
                    {
                        c.width = (GLuint)size.Width;
                        c.height = (GLuint)size.Height;
                        c.functions = IntPtr.Zero;
                        pnt = Marshal.AllocHGlobal(Marshal.SizeOf(c));
                        Marshal.StructureToPtr(c, pnt, false);
                        LinphoneManager.Instance.Core.NativePreviewWindowId = pnt;
                        oldPreviewSize = size;
                    }*/
                    //LinphoneManager.Instance.Core.CurrentCall.OglRender();
                    //LinphoneManager.Instance.Core.PreviewOglRender();
                    
                    mMainOpenGLES.MakeCurrent(mMainRenderSurface);

                    /* clear the color buffer */
                    OpenGLES.ClearColor(1.0f, 1.0f, 0.0f, 1.0f);
                    OpenGLES.Flush();

                    float[] vertices = { -0.5f, -0.5f, 0.5f, 0.5f };
                    float[] color= { 1.0f, 1.0f, 0.5f, 0.5f };
                    int[] indices = { 0, 1 };

                    IntPtr verticesSource= Marshal.AllocHGlobal(4*sizeof(float)), indicesSource= Marshal.AllocHGlobal(2*sizeof(int));
                    IntPtr colorSource = Marshal.AllocHGlobal(4 * sizeof(float));
                    Marshal.Copy(vertices, 0, verticesSource, 4);
                    Marshal.Copy(indices, 0, indicesSource, 2);

                    OpenGLES.EnableVertexAttribArray(0);
                    OpenGLES.EnableVertexAttribArray(1);
                    OpenGLES.VertexAttribPointer(0, 3, 5126, false, sizeof(float) * 3, verticesSource);// GL_FALSE:0
                    OpenGLES.VertexAttribPointer(1, 3, 5126, false, sizeof(float) * 3, colorSource);//color
                    OpenGLES.DrawElements(1, 2, 5125, indicesSource);
                    Debug.WriteLine(String.Format("Renderloop:"+ OpenGLES.eglGetError()));

                    //   OpenGLES.EnableClientState(32884);// GL_VERTEX_ARRAY);
                    //OpenGLES.VertexPointer(2, 5126, 0, verticesSource);//GL_FLOAT:5126 
                    //OpenGLES.DrawElements(1, 2, 5125, indicesSource);//GL_LINES:1;GL_UNSIGNED_INT:5125

                    //LinphoneManager.Instance.Core.PreviewOglRender();

                    if (mMainOpenGLES.SwapBuffers(mMainRenderSurface) != OpenGLES.EGL_TRUE)
                    {
                        Debug.WriteLine(String.Format("Renderloop: cannot swap buffer"));
                    }
                    /*
                    LinphoneManager.Instance.Core.PreviewOglRender();
                    if (mPreviewOpenGLES.SwapBuffers(mPreviewRenderSurface) != OpenGLES.EGL_TRUE)
                    {
                        Debug.WriteLine(String.Format("Renderloop: cannot swap buffer in preview"));
                    }*/

                    Marshal.FreeHGlobal(verticesSource);
                    Marshal.FreeHGlobal(colorSource);
                    Marshal.FreeHGlobal(indicesSource);
                }
            }
        }

        private IAsyncAction mRenderLoopWorker;
        private void StartVideoStream() {
            try {
               
                //LinphoneManager.Instance.Core.NativeVideoWindowIdString = VideoSwapChainPanel.Name;
                //LinphoneManager.Instance.Core.NativePreviewWindowIdString = PreviewSwapChainPanel.Name;

                // If the render loop is already running then do not start another thread. 
                if (mRenderLoopWorker != null && mRenderLoopWorker.Status == AsyncStatus.Started)
                {
                    return;
                }
                // Run task on a dedicated high priority background thread. 
                mRenderLoopWorker = ThreadPool.RunAsync(RenderLoop, WorkItemPriority.High, WorkItemOptions.TimeSliced);
            } catch (Exception e) {
                Debug.WriteLine(String.Format("StartVideoStream: Exception {0}", e.Message));
            }
        }

        private void StopVideoStream() {
            try {/*
                if (_videoSource != null) {
                    _videoSource.Stop();
                    _videoSource = null;
                }
                if (_previewSource != null) {
                    _previewSource.Stop();
                    _previewSource = null;
                }*/
                if (mRenderLoopWorker != null)
                {
                    mRenderLoopWorker.Cancel();
                    mRenderLoopWorker = null;
                }
                DestroyRenderSurface();
            } catch (Exception e) {
                Debug.WriteLine(String.Format("StopVideoStream: Exception {0}", e.Message));
            }

        }


        public class OpenGLES : IDisposable
        {
            // Out-of-band handle values
            public static readonly EGLNativeDisplayType EGL_DEFAULT_DISPLAY = IntPtr.Zero;
            public static readonly IntPtr EGL_NO_DISPLAY = IntPtr.Zero;
            public static readonly IntPtr EGL_NO_CONTEXT = IntPtr.Zero;
            public static readonly IntPtr EGL_NO_SURFACE = IntPtr.Zero;

            public const glbool EGL_FALSE = 0;
            public const glbool EGL_TRUE = 1;

            // Config attributes
            public const int EGL_BUFFER_SIZE = 0x3020;
            public const int EGL_ALPHA_SIZE = 0x3021;
            public const int EGL_BLUE_SIZE = 0x3022;
            public const int EGL_GREEN_SIZE = 0x3023;
            public const int EGL_RED_SIZE = 0x3024;
            public const int EGL_DEPTH_SIZE = 0x3025;
            public const int EGL_STENCIL_SIZE = 0x3026;

            // QuerySurface / SurfaceAttrib / CreatePbufferSurface targets
            public const int EGL_HEIGHT = 0x3056;
            public const int EGL_WIDTH = 0x3057;

            // Attrib list terminator
            public const int EGL_NONE = 0x3038;

            // CreateContext attributes
            public const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;

            // ANGLE
            public const int EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER = 0x320B;
            public const int EGL_ANGLE_SURFACE_RENDER_TO_BACK_BUFFER = 0x320C;

            public const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 0x3203;
            public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MAJOR_ANGLE = 0x3204;
            public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MINOR_ANGLE = 0x3205;
            public const int EGL_PLATFORM_ANGLE_TYPE_DEFAULT_ANGLE = 0x3206;

            public const int EGL_PLATFORM_ANGLE_ANGLE = 0x3202;

            public const int EGL_PLATFORM_ANGLE_TYPE_D3D9_ANGLE = 0x3207;
            public const int EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE = 0x3208;
            public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE = 0x3209;
            public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE = 0x320A;
            public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_WARP_ANGLE = 0x320B;
            public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_REFERENCE_ANGLE = 0x320C;
            public const int EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE = 0x320F;


            // fields

            private EGLDisplay mEglDisplay;
            private EGLContext mEglContext;
            private EGLConfig mEglConfig;

            public OpenGLES()
            {
                mEglDisplay = EGL_NO_DISPLAY;
                mEglContext = EGL_NO_CONTEXT;
                mEglConfig = default(EGLConfig);

                Initialize();
            }

            public void Dispose()
            {
                Cleanup();
            }

            public void Initialize()
            {
                int[] configAttributes =
                {
                    EGL_RED_SIZE, 8,
                    EGL_GREEN_SIZE, 8,
                    EGL_BLUE_SIZE, 8,
                    EGL_ALPHA_SIZE, 8,
                    EGL_DEPTH_SIZE, 8,
                    EGL_STENCIL_SIZE, 8,
                    EGL_NONE
                };

                int[] contextAttributes =
                {
                    EGL_CONTEXT_CLIENT_VERSION, 2,
                    EGL_NONE
                };

                int[] defaultDisplayAttributes =
                {
					// These are the default display attributes, used to request ANGLE's D3D11 renderer.
					// eglInitialize will only succeed with these attributes if the hardware supports D3D11 Feature Level 10_0+.
					EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,

					// EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER is an optimization that can have large performance benefits on mobile devices.
					// Its syntax is subject to change, though. Please update your Visual Studio templates if you experience compilation issues with it.
					EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER, EGL_TRUE, 

					// EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE is an option that enables ANGLE to automatically call 
					// the IDXGIDevice3.Trim method on behalf of the application when it gets suspended. 
					// Calling IDXGIDevice3.Trim when an application is suspended is a Windows Store application certification requirement.
					EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE, EGL_TRUE,
                    EGL_NONE,
                };

                int[] fl9_3DisplayAttributes =
                {
					// These can be used to request ANGLE's D3D11 renderer, with D3D11 Feature Level 9_3.
					// These attributes are used if the call to eglInitialize fails with the default display attributes.
					EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,
                    EGL_PLATFORM_ANGLE_MAX_VERSION_MAJOR_ANGLE, 9,
                    EGL_PLATFORM_ANGLE_MAX_VERSION_MINOR_ANGLE, 3,
                    EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER, EGL_TRUE,
                    EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE, EGL_TRUE,
                    EGL_NONE,
                };

                int[] warpDisplayAttributes =
                {
					// These attributes can be used to request D3D11 WARP.
					// They are used if eglInitialize fails with both the default display attributes and the 9_3 display attributes.
					EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,
                    EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_DEVICE_TYPE_WARP_ANGLE,
                    EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER, EGL_TRUE,
                    EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE, EGL_TRUE,
                    EGL_NONE,
                };

                //
                // To initialize the display, we make three sets of calls to eglGetPlatformDisplayEXT and eglInitialize, with varying 
                // parameters passed to eglGetPlatformDisplayEXT:
                // 1) The first calls uses "defaultDisplayAttributes" as a parameter. This corresponds to D3D11 Feature Level 10_0+.
                // 2) If eglInitialize fails for step 1 (e.g. because 10_0+ isn't supported by the default GPU), then we try again 
                //    using "fl9_3DisplayAttributes". This corresponds to D3D11 Feature Level 9_3.
                // 3) If eglInitialize fails for step 2 (e.g. because 9_3+ isn't supported by the default GPU), then we try again 
                //    using "warpDisplayAttributes".  This corresponds to D3D11 Feature Level 11_0 on WARP, a D3D11 software rasterizer.
                //

                // This tries to initialize EGL to D3D11 Feature Level 10_0+. See above comment for details.
                mEglDisplay = eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, EGL_DEFAULT_DISPLAY, defaultDisplayAttributes);
                if (mEglDisplay == EGL_NO_DISPLAY)
                {
                    throw new Exception("Failed to get EGL display (D3D11 10.0+).");
                }

                int major, minor;
                if (eglInitialize(mEglDisplay, out major, out minor) == EGL_FALSE)
                {
                    // This tries to initialize EGL to D3D11 Feature Level 9_3, if 10_0+ is unavailable (e.g. on some mobile devices).
                    mEglDisplay = eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, EGL_DEFAULT_DISPLAY, fl9_3DisplayAttributes);
                    if (mEglDisplay == EGL_NO_DISPLAY)
                    {
                        throw new Exception("Failed to get EGL display (D3D11 9.3).");
                    }

                    if (eglInitialize(mEglDisplay, out major, out minor) == EGL_FALSE)
                    {
                        // This initializes EGL to D3D11 Feature Level 11_0 on WARP, if 9_3+ is unavailable on the default GPU.
                        mEglDisplay = eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, EGL_DEFAULT_DISPLAY, warpDisplayAttributes);
                        if (mEglDisplay == EGL_NO_DISPLAY)
                        {
                            throw new Exception("Failed to get EGL display (D3D11 11.0 WARP)");
                        }

                        if (eglInitialize(mEglDisplay, out major, out minor) == EGL_FALSE)
                        {
                            // If all of the calls to eglInitialize returned EGL_FALSE then an error has occurred.
                            throw new Exception("Failed to initialize EGL");
                        }
                    }
                }

                int numConfigs = 0;
                EGLDisplay[] configs = new EGLDisplay[1];
                if (eglChooseConfig(mEglDisplay, configAttributes, configs, configs.Length, out numConfigs) == EGL_FALSE || numConfigs == 0)
                {
                    throw new Exception("Failed to choose first EGLConfig");
                }
                mEglConfig = configs[0];

                mEglContext = eglCreateContext(mEglDisplay, mEglConfig, EGL_NO_CONTEXT, contextAttributes);
                if (mEglContext == EGL_NO_CONTEXT)
                {
                    throw new Exception("Failed to create EGL context");
                }
            }

            public void Cleanup()
            {
                if (mEglDisplay != EGL_NO_DISPLAY && mEglContext != EGL_NO_CONTEXT)
                {
                    eglDestroyContext(mEglDisplay, mEglContext);
                    mEglContext = EGL_NO_CONTEXT;
                }

                if (mEglDisplay != EGL_NO_DISPLAY)
                {
                    eglTerminate(mEglDisplay);
                    mEglDisplay = EGL_NO_DISPLAY;
                }
            }

            public EGLSurface CreateSurface(SwapChainPanel panel)
            {
                if (panel == null)
                {
                    throw new ArgumentNullException("SwapChainPanel parameter is invalid");
                }

                EGLSurface surface = EGL_NO_SURFACE;

                int[] surfaceAttributes =
                {
					// EGL_ANGLE_SURFACE_RENDER_TO_BACK_BUFFER is part of the same optimization as EGL_ANGLE_DISPLAY_ALLOW_RENDER_TO_BACK_BUFFER (see above).
					// If you have compilation issues with it then please update your Visual Studio templates.
					EGL_ANGLE_SURFACE_RENDER_TO_BACK_BUFFER, EGL_TRUE,
                    EGL_NONE
                };

                // Create a PropertySet and initialize with the EGLNativeWindowType.
                PropertySet surfaceCreationProperties = new PropertySet();
                surfaceCreationProperties.Add("EGLNativeWindowTypeProperty", panel);
                
                surface = eglCreateWindowSurface(mEglDisplay, mEglConfig, surfaceCreationProperties, surfaceAttributes);
                if (surface == EGL_NO_SURFACE)
                {
                    throw new Exception("Failed to create EGL surface : "+ eglGetError());
                }

                return surface;
            }

            public Size GetSurfaceDimensions(EGLSurface surface)
            {
                int width = 0;
                int height = 0;
                eglQuerySurface(mEglDisplay, surface, EGL_WIDTH, out width);
                eglQuerySurface(mEglDisplay, surface, EGL_HEIGHT, out height);
                return new Size(width, height);
            }

            public void DestroySurface(EGLSurface surface)
            {
                if (mEglDisplay != EGL_NO_DISPLAY && surface != EGL_NO_SURFACE)
                {
                    eglDestroySurface(mEglDisplay, surface);
                }
            }

            public void MakeCurrent(EGLSurface surface)
            {
                if (eglMakeCurrent(mEglDisplay, surface, surface, mEglContext) == EGL_FALSE)
                {
                    throw new Exception("Failed to make EGLSurface current" + eglGetError());
                }
            }

            public int SwapBuffers(EGLSurface surface)
            {
                return eglSwapBuffers(mEglDisplay, surface);
            }

            public void Reset()
            {
                Cleanup();
                Initialize();
            }

            // C API

            private const string libEGL = "libEGL.dll";

            [DllImport(libEGL)]
            private static extern IntPtr eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procname);
            [DllImport(libEGL)]
            public static extern EGLDisplay eglGetPlatformDisplayEXT(int platform, EGLNativeDisplayType native_display, int[] attrib_list);
            [DllImport(libEGL)]
            public static extern glbool eglInitialize(EGLDisplay dpy, out int major, out int minor);
            [DllImport(libEGL)]
            public static extern glbool eglChooseConfig(EGLDisplay dpy, int[] attrib_list, [In, Out] EGLConfig[] configs, int config_size, out int num_config);
            [DllImport(libEGL)]
            public static extern EGLContext eglCreateContext(EGLDisplay dpy, EGLConfig config, EGLContext share_context, int[] attrib_list);
            [DllImport(libEGL)]
            public static extern EGLSurface eglCreateWindowSurface(EGLDisplay dpy, EGLConfig config, [MarshalAs(UnmanagedType.IInspectable)] EGLNativeWindowType win, int[] attrib_list);
            [DllImport(libEGL)]
            public static extern glbool eglQuerySurface(EGLDisplay dpy, EGLSurface surface, int attribute, out int value);
            [DllImport(libEGL)]
            public static extern glbool eglDestroySurface(EGLDisplay dpy, EGLSurface surface);
            [DllImport(libEGL)]
            public static extern glbool eglMakeCurrent(EGLDisplay dpy, EGLSurface draw, EGLSurface read, EGLContext ctx);
            [DllImport(libEGL)]
            public static extern glbool eglSwapBuffers(EGLDisplay dpy, EGLSurface surface);
            [DllImport(libEGL)]
            public static extern glbool eglDestroyContext(EGLDisplay dpy, EGLContext ctx);
            [DllImport(libEGL)]
            public static extern glbool eglTerminate(EGLDisplay dpy);
            [DllImport(libEGL)]
            public static extern glbool eglGetError();
            
            private const string libGLESv2 = "libGLESv2.dll";


            [DllImport(libGLESv2, EntryPoint = "glActiveTexture", ExactSpelling = true)]
            public static extern void ActiveTexture(int texture);
            [DllImport(libGLESv2, EntryPoint = "glAttachShader", ExactSpelling = true)]
            public static extern void AttachShader(int program, int shader);
            [DllImport(libGLESv2, EntryPoint = "glBindAttribLocation", ExactSpelling = true)]
            public static extern void BindAttribLocation(int program, int index, string name);
            [DllImport(libGLESv2, EntryPoint = "glBindBuffer", ExactSpelling = true)]
            public static extern void BindBuffer(int target, int buffer);
            [DllImport(libGLESv2, EntryPoint = "glBindFramebuffer", ExactSpelling = true)]
            public static extern void BindFramebuffer(int target, int framebuffer);
            [DllImport(libGLESv2, EntryPoint = "glBindRenderbuffer", ExactSpelling = true)]
            public static extern void BindRenderbuffer(int target, int renderbuffer);
            [DllImport(libGLESv2, EntryPoint = "glBindTexture", ExactSpelling = true)]
            public static extern void BindTexture(int target, int texture);
            [DllImport(libGLESv2, EntryPoint = "glBlendColor", ExactSpelling = true)]
            public static extern void BlendColor(float red, float green, float blue, float alpha);
            [DllImport(libGLESv2, EntryPoint = "glBlendEquation", ExactSpelling = true)]
            public static extern void BlendEquation(int mode);
            [DllImport(libGLESv2, EntryPoint = "glBlendEquationSeparate", ExactSpelling = true)]
            public static extern void BlendEquationSeparate(int modeRGB, int modeAlpha);
            [DllImport(libGLESv2, EntryPoint = "glBlendFunc", ExactSpelling = true)]
            public static extern void BlendFunc(int sfactor, int dfactor);
            [DllImport(libGLESv2, EntryPoint = "glBlendFuncSeparate", ExactSpelling = true)]
            public static extern void BlendFuncSeparate(int srcRGB, int dstRGB, int srcAlpha, int dstAlpha);
            [DllImport(libGLESv2, EntryPoint = "glBufferData", ExactSpelling = true)]
            public static extern void BufferData(int target, int size, IntPtr data, int usage);
            [DllImport(libGLESv2, EntryPoint = "glBufferSubData", ExactSpelling = true)]
            public static extern void BufferSubData(int target, int offset, int size, IntPtr data);
            [DllImport(libGLESv2, EntryPoint = "glCheckFramebufferStatus", ExactSpelling = true)]
            public static extern int CheckFramebufferStatus(int target);
            [DllImport(libGLESv2, EntryPoint = "glClear", ExactSpelling = true)]
            public static extern void Clear(uint mask);
            [DllImport(libGLESv2, EntryPoint = "glClearColor", ExactSpelling = true)]
            public static extern void ClearColor(float red, float green, float blue, float alpha);
            [DllImport(libGLESv2, EntryPoint = "glClearDepthf", ExactSpelling = true)]
            public static extern void ClearDepthf(float depth);
            [DllImport(libGLESv2, EntryPoint = "glClearStencil", ExactSpelling = true)]
            public static extern void ClearStencil(int s);
            [DllImport(libGLESv2, EntryPoint = "glColorMask", ExactSpelling = true)]
            public static extern void ColorMask([MarshalAs(UnmanagedType.I1)] bool red, [MarshalAs(UnmanagedType.I1)] bool green, [MarshalAs(UnmanagedType.I1)] bool blue, [MarshalAs(UnmanagedType.I1)] bool alpha);
            [DllImport(libGLESv2, EntryPoint = "glCompileShader", ExactSpelling = true)]
            public static extern void CompileShader(int shader);
            [DllImport(libGLESv2, EntryPoint = "glCompressedTexImage2D", ExactSpelling = true)]
            public static extern void CompressedTexImage2D(int target, int level, int internalformat, int width, int height, int border, int imageSize, IntPtr data);
            [DllImport(libGLESv2, EntryPoint = "glCompressedTexSubImage2D", ExactSpelling = true)]
            public static extern void CompressedTexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int imageSize, IntPtr data);
            [DllImport(libGLESv2, EntryPoint = "glCopyTexImage2D", ExactSpelling = true)]
            public static extern void CopyTexImage2D(int target, int level, int internalformat, int x, int y, int width, int height, int border);
            [DllImport(libGLESv2, EntryPoint = "glCopyTexSubImage2D", ExactSpelling = true)]
            public static extern void CopyTexSubImage2D(int target, int level, int xoffset, int yoffset, int x, int y, int width, int height);
            [DllImport(libGLESv2, EntryPoint = "glCreateProgram", ExactSpelling = true)]
            public static extern int CreateProgram();
            [DllImport(libGLESv2, EntryPoint = "glCreateShader", ExactSpelling = true)]
            public static extern int CreateShader(int type);
            [DllImport(libGLESv2, EntryPoint = "glCullFace", ExactSpelling = true)]
            public static extern void CullFace(int mode);
            [DllImport(libGLESv2, EntryPoint = "glDeleteBuffers", ExactSpelling = true)]
            public static extern void DeleteBuffers(int n, int[] buffers);
            [DllImport(libGLESv2, EntryPoint = "glDeleteFramebuffers", ExactSpelling = true)]
            public static extern void DeleteFramebuffers(int n, int[] framebuffers);
            [DllImport(libGLESv2, EntryPoint = "glDeleteProgram", ExactSpelling = true)]
            public static extern void DeleteProgram(int program);
            [DllImport(libGLESv2, EntryPoint = "glDeleteRenderbuffers", ExactSpelling = true)]
            public static extern void DeleteRenderbuffers(int n, int[] renderbuffers);
            [DllImport(libGLESv2, EntryPoint = "glDeleteShader", ExactSpelling = true)]
            public static extern void DeleteShader(int shader);
            [DllImport(libGLESv2, EntryPoint = "glDeleteTextures", ExactSpelling = true)]
            public static extern void DeleteTextures(int n, int[] textures);
            [DllImport(libGLESv2, EntryPoint = "glDepthFunc", ExactSpelling = true)]
            public static extern void DepthFunc(int func);
            [DllImport(libGLESv2, EntryPoint = "glDepthMask", ExactSpelling = true)]
            public static extern void DepthMask([MarshalAs(UnmanagedType.I1)] bool flag);
            [DllImport(libGLESv2, EntryPoint = "glDepthRangef", ExactSpelling = true)]
            public static extern void DepthRangef(float zNear, float zFar);
            [DllImport(libGLESv2, EntryPoint = "glDetachShader", ExactSpelling = true)]
            public static extern void DetachShader(int program, int shader);
            [DllImport(libGLESv2, EntryPoint = "glDisable", ExactSpelling = true)]
            public static extern void Disable(int cap);
            [DllImport(libGLESv2, EntryPoint = "glDisableVertexAttribArray", ExactSpelling = true)]
            public static extern void DisableVertexAttribArray(int index);
            [DllImport(libGLESv2, EntryPoint = "glDrawArrays", ExactSpelling = true)]
            public static extern void DrawArrays(int mode, int first, int count);
            [DllImport(libGLESv2, EntryPoint = "glDrawElements", ExactSpelling = true)]
            public static extern void DrawElements(int mode, int count, int type, IntPtr indices);
            [DllImport(libGLESv2, EntryPoint = "glDrawElements", ExactSpelling = true)]
            public static extern void DrawElements(int mode, int count, int type, int indices);
            [DllImport(libGLESv2, EntryPoint = "glEnable", ExactSpelling = true)]
            public static extern void Enable(int cap);

            //[DllImport(libGLESv2, EntryPoint = "glEnableClientState", ExactSpelling = true)]// Doesn't exist
            //public static extern void EnableClientState(int cap);
            //[DllImport(libGLESv2, EntryPoint = "glVertexPointer", ExactSpelling = true)]  // 1.1, not 2.0
            //public static extern void VertexPointer(int size, int type, int stride, IntPtr pointer);

            [DllImport(libGLESv2, EntryPoint = "glEnableVertexAttribArray", ExactSpelling = true)]
            public static extern void EnableVertexAttribArray(int index);
            [DllImport(libGLESv2, EntryPoint = "glFinish", ExactSpelling = true)]
            public static extern void Finish();
            [DllImport(libGLESv2, EntryPoint = "glFlush", ExactSpelling = true)]
            public static extern void Flush();
            [DllImport(libGLESv2, EntryPoint = "glFramebufferRenderbuffer", ExactSpelling = true)]
            public static extern void FramebufferRenderbuffer(int target, int attachment, int renderbuffertarget, int renderbuffer);
            [DllImport(libGLESv2, EntryPoint = "glFramebufferTexture2D", ExactSpelling = true)]
            public static extern void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level);
            [DllImport(libGLESv2, EntryPoint = "glFrontFace", ExactSpelling = true)]
            public static extern void FrontFace(int mode);
            [DllImport(libGLESv2, EntryPoint = "glGenBuffers", ExactSpelling = true)]
            public static extern void GenBuffers(int n, int[] buffers);
            [DllImport(libGLESv2, EntryPoint = "glGenerateMipmap", ExactSpelling = true)]
            public static extern void GenerateMipmap(int target);
            [DllImport(libGLESv2, EntryPoint = "glGenFramebuffers", ExactSpelling = true)]
            public static extern void GenFramebuffers(int n, int[] framebuffers);
            [DllImport(libGLESv2, EntryPoint = "glGenRenderbuffers", ExactSpelling = true)]
            public static extern void GenRenderbuffers(int n, int[] renderbuffers);
            [DllImport(libGLESv2, EntryPoint = "glGenTextures", ExactSpelling = true)]
            public static extern void GenTextures(int n, int[] textures);
            //public static extern void glGetActiveAttrib (int program, int index, int bufsize, int* length, int size, int* type, string name);
            //public static extern void glGetActiveUniform (int program, int index, int bufsize, int* length, int size, int* type, string name);
            //public static extern void glGetAttachedShaders (int program, int maxcount, int* count, int* shaders);
            [DllImport(libGLESv2, EntryPoint = "glGetAttribLocation", ExactSpelling = true)]
            public static extern int GetAttribLocation(int program, string name);
            /*
            //public static extern void glGetBooleanv (int pname, GLboolean* params);
            //public static extern void glGetBufferParameteriv (int target, int pname, int  params);
            */
            [DllImport(libGLESv2, EntryPoint = "glGetError", ExactSpelling = true)]
            public static extern int GetError();
            //public static extern void glGetFloatv (int pname, float params);
            //public static extern void glGetFramebufferAttachmentParameteriv (int target, int attachment, int pname, int params);
            //public static extern void glGetIntegerv (int pname, int params);
            [DllImport(libGLESv2, EntryPoint = "glGetProgramiv", ExactSpelling = true)]
            public static extern void GetProgramiv(int program, int pname, int[] parameters);
            [DllImport(libGLESv2, EntryPoint = "glGetProgramiv", ExactSpelling = true)]
            public static extern void GetProgramiv(int program, int pname, out int parameter);
//            [DllImport(libGLESv2, EntryPoint = "glGetProgramInfoLog", ExactSpelling = true)]
//            static extern void GetProgramInfoLog(int program, int bufsize, IntPtr length, byte[] infolog);
//            public static string GetProgramInfoLog(int program)
//            {
//                int infoLogLength;
//                GetProgramiv(program, GL_INFO_LOG_LENGTH, out infoLogLength);
//                byte[] data = new byte[infoLogLength];
//                GetProgramInfoLog(program, infoLogLength, IntPtr.Zero, data);
//                return Encoding.ASCII.GetString(data);
//            }
            //public static extern void glGetRenderbufferParameteriv (int target, int pname, int params);
            [DllImport(libGLESv2, EntryPoint = "glGetShaderiv", ExactSpelling = true)]
            public static extern void GetShaderiv(int shader, int pname, int[] parameters);
//            [DllImport(libGLESv2, EntryPoint = "glGetShaderiv", ExactSpelling = true)]
//            public static extern void GetShaderiv(int shader, int pname, out int parameter);
//            public static string GetShaderInfoLog(int shader)
//            {
//                int infoLogLength;
//                GetShaderiv(shader, GL_INFO_LOG_LENGTH, out infoLogLength);
//                byte[] data = new byte[infoLogLength];
//                GetShaderInfoLog(shader, infoLogLength, IntPtr.Zero, data);
//                return Encoding.ASCII.GetString(data);
//            }
            [DllImport(libGLESv2, EntryPoint = "glGetShaderInfoLog", ExactSpelling = true)]
            static extern void GetShaderInfoLog(int shader, int bufsize, IntPtr length, byte[] infolog);
            //public static extern void glGetShaderPrecisionFormat (int shadertype, int precisiontype, int range, int precision);
            //public static extern void glGetShaderSource (int shader, int bufsize, int* length, GLchar* source);
            //GL_APICALL const GLubyte* GL_APIENTRY glGetString (int name);
            //public static extern void glGetTexParameterfv (int target, int pname, float params);
            //public static extern void glGetTexParameteriv (int target, int pname, int params);
            //public static extern void glGetUniformfv (int program, int location, float params);
            //public static extern void glGetUniformiv (int program, int location, int params);
            //public static extern void glGetVertexAttribfv (int index, int pname, float params);
            //public static extern void glGetVertexAttribiv (int index, int pname, int params);
            //public static extern void glGetVertexAttribPointerv (int index, int pname, GLvoid** pointer);
            [DllImport(libGLESv2, EntryPoint = "glGetUniformLocation", ExactSpelling = true)]
            public static extern int GetUniformLocation(int program, string name);
            [DllImport(libGLESv2, EntryPoint = "glHint", ExactSpelling = true)]
            public static extern void Hint(int target, int mode);
            [DllImport(libGLESv2, EntryPoint = "glIsBuffer", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsBuffer(int buffer);
            [DllImport(libGLESv2, EntryPoint = "glIsEnabled", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsEnabled(int cap);
            [DllImport(libGLESv2, EntryPoint = "glIsFramebuffer", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsFramebuffer(int framebuffer);
            [DllImport(libGLESv2, EntryPoint = "glIsProgram", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsProgram(int program);
            [DllImport(libGLESv2, EntryPoint = "glIsRenderbuffer", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsRenderbuffer(int renderbuffer);
            [DllImport(libGLESv2, EntryPoint = "glIsShader", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsShader(int shader);
            [DllImport(libGLESv2, EntryPoint = "glIsTexture", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsTexture(int texture);
            [DllImport(libGLESv2, EntryPoint = "glLineWidth", ExactSpelling = true)]
            public static extern void LineWidth(float width);
            [DllImport(libGLESv2, EntryPoint = "glLinkProgram", ExactSpelling = true)]
            public static extern void LinkProgram(int program);
            [DllImport(libGLESv2, EntryPoint = "glPixelStorei", ExactSpelling = true)]
            public static extern void PixelStorei(int pname, int param);
            [DllImport(libGLESv2, EntryPoint = "glPolygonOffset", ExactSpelling = true)]
            public static extern void PolygonOffset(float factor, float units);
            //public static extern void glReadPixels (int x, int y, int width, int height, int format, int type, GLvoid* pixels);
            [DllImport(libGLESv2, EntryPoint = "glReleaseShaderCompiler", ExactSpelling = true)]
            public static extern void ReleaseShaderCompiler();
            [DllImport(libGLESv2, EntryPoint = "glRenderbufferStorage", ExactSpelling = true)]
            public static extern void RenderbufferStorage(int target, int internalformat, int width, int height);
            [DllImport(libGLESv2, EntryPoint = "glSampleCoverage", ExactSpelling = true)]
            public static extern void SampleCoverage(float value, [MarshalAs(UnmanagedType.I1)] bool invert);
            [DllImport(libGLESv2, EntryPoint = "glScissor", ExactSpelling = true)]
            public static extern void Scissor(int x, int y, int width, int height);
            //public static extern void glShaderBinary (int n, const int* shaders, int binaryformat, IntPtr binary, int length);
            public static void ShaderSource(int shader, string source)
            {
                ShaderSource(shader, 1, new string[] { source }, 0);
            }
            [DllImport(libGLESv2, EntryPoint = "glShaderSource", ExactSpelling = true)]
            public static extern void ShaderSource(int shader, int count, string[] source, int length);
            [DllImport(libGLESv2, EntryPoint = "glStencilFunc", ExactSpelling = true)]
            public static extern void StencilFunc(int func, int @ref, int mask);
            [DllImport(libGLESv2, EntryPoint = "glStencilFuncSeparate", ExactSpelling = true)]
            public static extern void StencilFuncSeparate(int face, int func, int @ref, int mask);
            [DllImport(libGLESv2, EntryPoint = "glStencilMask", ExactSpelling = true)]
            public static extern void StencilMask(int mask);
            [DllImport(libGLESv2, EntryPoint = "glStencilMaskSeparate", ExactSpelling = true)]
            public static extern void StencilMaskSeparate(int face, int mask);
            [DllImport(libGLESv2, EntryPoint = "glStencilOp", ExactSpelling = true)]
            public static extern void StencilOp(int fail, int zfail, int zpass);
            [DllImport(libGLESv2, EntryPoint = "glStencilOpSeparate", ExactSpelling = true)]
            public static extern void StencilOpSeparate(int face, int fail, int zfail, int zpass);
            [DllImport(libGLESv2, EntryPoint = "glTexImage2D", ExactSpelling = true)]
            public static extern void TexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, IntPtr pixels);
            [DllImport(libGLESv2, EntryPoint = "glTexParameterf", ExactSpelling = true)]
            public static extern void TexParameterf(int target, int pname, float param);
            //public static extern void glTexParameterfv (int target, int pname, const float params);
            [DllImport(libGLESv2, EntryPoint = "glTexParameteri", ExactSpelling = true)]
            public static extern void TexParameteri(int target, int pname, int param);
            //public static extern void glTexParameteriv (int target, int pname, const int params);
            [DllImport(libGLESv2, EntryPoint = "glTexSubImage2D", ExactSpelling = true)]
            public static extern void TexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, IntPtr pixels);
            [DllImport(libGLESv2, EntryPoint = "glUniform1f", ExactSpelling = true)]
            public static extern void Uniform1f(int location, float x);
            //public static extern void glUniform1fv (int location, int count, const float v);
            [DllImport(libGLESv2, EntryPoint = "glUniform1i", ExactSpelling = true)]
            public static extern void Uniform1i(int location, int x);
            //public static extern void glUniform1iv (int location, int count, const int v);
            [DllImport(libGLESv2, EntryPoint = "glUniform2f", ExactSpelling = true)]
            public static extern void Uniform2f(int location, float x, float y);
            //public static extern void glUniform2fv (int location, int count, const float v);
            [DllImport(libGLESv2, EntryPoint = "glUniform2i", ExactSpelling = true)]
            public static extern void Uniform2i(int location, int x, int y);
            //public static extern void glUniform2iv (int location, int count, const int v);
            [DllImport(libGLESv2, EntryPoint = "glUniform3f", ExactSpelling = true)]
            public static extern void Uniform3f(int location, float x, float y, float z);
            //public static extern void glUniform3fv (int location, int count, const float v);
            [DllImport(libGLESv2, EntryPoint = "glUniform3i", ExactSpelling = true)]
            public static extern void Uniform3i(int location, int x, int y, int z);
            //public static extern void glUniform3iv (int location, int count, const int v);
            [DllImport(libGLESv2, EntryPoint = "glUniform4f", ExactSpelling = true)]
            public static extern void Uniform4f(int location, float x, float y, float z, float w);
            //public static extern void glUniform4fv (int location, int count, const float v);
            [DllImport(libGLESv2, EntryPoint = "glUniform4i", ExactSpelling = true)]
            public static extern void Uniform4i(int location, int x, int y, int z, int w);
            //public static extern void glUniform4iv (int location, int count, const int v);
            //public static extern void glUniformMatrix2fv (int location, int count, GLboolean transpose, const float value);
            [DllImport(libGLESv2, EntryPoint = "glUniformMatrix3fv", ExactSpelling = true)]
            public static extern void UniformMatrix3fv(int location, int count, [MarshalAs(UnmanagedType.I1)] bool transpose, float[] value);
            //public static extern void glUniformMatrix4fv (int location, int count, GLboolean transpose, const float value);
            [DllImport(libGLESv2, EntryPoint = "glUseProgram", ExactSpelling = true)]
            public static extern void UseProgram(int program);
            [DllImport(libGLESv2, EntryPoint = "glValidateProgram", ExactSpelling = true)]
            public static extern void ValidateProgram(int program);
            [DllImport(libGLESv2, EntryPoint = "glVertexAttrib1f", ExactSpelling = true)]
            public static extern void VertexAttrib1f(int indx, float x);
            //public static extern void glVertexAttrib1fv (int indx, const float values);
            [DllImport(libGLESv2, EntryPoint = "glVertexAttrib2f", ExactSpelling = true)]
            public static extern void VertexAttrib2f(int indx, float x, float y);
            //public static extern void glVertexAttrib2fv (int indx, const float  values);
            [DllImport(libGLESv2, EntryPoint = "glVertexAttrib3f", ExactSpelling = true)]
            public static extern void VertexAttrib3f(int indx, float x, float y, float z);
            //public static extern void glVertexAttrib3fv (int indx, const float  values);
            [DllImport(libGLESv2, EntryPoint = "glVertexAttrib4f", ExactSpelling = true)]
            public static extern void VertexAttrib4f(int indx, float x, float y, float z, float w);
            //public static extern void glVertexAttrib4fv (int indx, const float  values);
            [DllImport(libGLESv2, EntryPoint = "glVertexAttribPointer", ExactSpelling = true)]
            public static extern void VertexAttribPointer(int indx, int size, int type, [MarshalAs(UnmanagedType.I1)] bool normalized, int stride, IntPtr ptr);
            [DllImport(libGLESv2, EntryPoint = "glViewport", ExactSpelling = true)]
            public static extern void Viewport(int x, int y, int width, int height);
            /*


            [DllImport(libGLESv2)]
            public static extern glbool glActiveTexture();//
            [DllImport(libGLESv2)]
            public static extern glbool glAttachShader();//
            [DllImport(libGLESv2)]
            public static extern glbool glBindAttribLocation();
            [DllImport(libGLESv2)]
            public static extern glbool glBindTexture();
            [DllImport(libGLESv2)]
            public static extern glbool glClear();
            [DllImport(libGLESv2)]
            public static extern glbool glClearColor();
            [DllImport(libGLESv2)]
            public static extern glbool glCompileShader();
            [DllImport(libGLESv2)]
            public static extern glbool glCreateProgram();
            [DllImport(libGLESv2)]
            public static extern glbool glCreateShader();
            [DllImport(libGLESv2)]
            public static extern glbool glDeleteProgram();
            [DllImport(libGLESv2)]
            public static extern glbool glDeleteShader();
            [DllImport(libGLESv2)]
            public static extern glbool glDeleteTextures();
            [DllImport(libGLESv2)]
            public static extern glbool glDisable();
            [DllImport(libGLESv2)]
            public static extern glbool glDrawArrays();
            [DllImport(libGLESv2)]
            public static extern glbool glEnableVertexAttribArray();
            [DllImport(libGLESv2)]
            public static extern glbool glGenTextures();
            [DllImport(libGLESv2)]
            public static extern glbool glGetProgramInfoLog();
            [DllImport(libGLESv2)]
            public static extern glbool glGetProgramiv();
            [DllImport(libGLESv2)]
            public static extern glbool glGetShaderInfoLog();
            [DllImport(libGLESv2)]
            public static extern glbool glGetShaderiv();
            [DllImport(libGLESv2)]
            public static extern glbool glGetString();
            [DllImport(libGLESv2)]
            public static extern glbool glGetUniformLocation();
            [DllImport(libGLESv2)]
            public static extern glbool glLinkProgram();
            [DllImport(libGLESv2)]
            public static extern glbool glPixelStorei();
            [DllImport(libGLESv2)]
            public static extern glbool glShaderSource();
            [DllImport(libGLESv2)]
            public static extern glbool glTexImage2D();
            [DllImport(libGLESv2)]
            public static extern glbool glTexParameteri();
            [DllImport(libGLESv2)]
            public static extern glbool glTexSubImage2D();
            [DllImport(libGLESv2)]
            public static extern glbool glUniform1f();
            [DllImport(libGLESv2)]
            public static extern glbool glUniform1i();
            [DllImport(libGLESv2)]
            public static extern glbool glUniformMatrix4fv();
            [DllImport(libGLESv2)]
            public static extern glbool glUseProgram();
            [DllImport(libGLESv2)]
            public static extern glbool glValidateProgram();
            [DllImport(libGLESv2)]
            public static extern glbool glVertexAttribPointer();
            [DllImport(libGLESv2)]
            public static extern glbool glViewport();
            */



        }





        #endregion

        #region Orientation
        private async void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args) {
            // Keep previous orientation when the user puts its device faceup or facedown
            if ((args.Orientation != SimpleOrientation.Faceup) && (args.Orientation != SimpleOrientation.Facedown)) {
                deviceOrientation = args.Orientation;
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => SetVideoOrientation());
            }
        }

        private void SetVideoOrientation() {
            SimpleOrientation orientation = deviceOrientation;
            if (displayInformation.NativeOrientation == DisplayOrientations.Portrait) {
                switch (orientation) {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        orientation = SimpleOrientation.NotRotated;
                        break;
                    case SimpleOrientation.Rotated180DegreesCounterclockwise:
                        orientation = SimpleOrientation.Rotated90DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        orientation = SimpleOrientation.Rotated180DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.NotRotated:
                    default:
                        orientation = SimpleOrientation.Rotated270DegreesCounterclockwise;
                        break;
                }
            }
            int degrees = 0;
            switch (orientation) {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    degrees = 90;
                    break;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    degrees = 180;
                    break;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    degrees = 270;
                    break;
                case SimpleOrientation.NotRotated:
                default:
                    degrees = 0;
                    break;
            }

            int currentDegrees = LinphoneManager.Instance.Core.DeviceRotation;
            if (currentDegrees != degrees) {
                LinphoneManager.Instance.Core.DeviceRotation = degrees;
            }
        }
        #endregion

        /*private void cm_ContactFound(object sender, ContactFoundEventArgs e)
        {
            if (e.ContactFound != null)
            {
                Contact.Text = e.ContactFound.DisplayName;
                if (e.PhoneLabel != null)
                {
                    Number.Text = e.PhoneLabel + " : " + e.PhoneNumber;
                }
                else
                {
                    Number.Text = e.PhoneNumber;
                }
            }
        }*/

        private void timerTick(Object sender, Object e) {
            Call call = ((InCallModel)this.DataContext).GetCurrentCall();
            if (call == null)
                return;

            //startTime = (DateTimeOffset)call.CallStartTimeFromContext;


            TimeSpan callDuration = new TimeSpan(call.Duration * TimeSpan.TicksPerSecond);
            var hh = callDuration.Hours;
            var ss = callDuration.Seconds;
            var mm = callDuration.Minutes;
            Status.Text = hh.ToString("00") + ":" + mm.ToString("00") + ":" + ss.ToString("00");

            string audioPayloadType = "";
            string audioDownloadBandwidth = "";
            string audioUploadBandwidth = "";
            string videoPayloadType = "";
            string videoDownloadBandwidth = "";
            string videoUploadBandwidth = "";

            CallParams param = call.CurrentParams;
            ((InCallModel)this.DataContext).MediaEncryption = param.MediaEncryption.ToString();

            CallStats audioStats = null;
            try {
                audioStats = call.GetStats(StreamType.Audio);
            } catch { }

            if (audioStats != null) {
                audioDownloadBandwidth = String.Format("{0:0.00}", audioStats.DownloadBandwidth);
                audioUploadBandwidth = String.Format("{0:0.00}", audioStats.UploadBandwidth);
                ((InCallModel)this.DataContext).ICE = audioStats.IceState.ToString();
            }

            PayloadType audiopt = param.UsedAudioPayloadType;
            if (audiopt != null) {
                audioPayloadType = audiopt.MimeType + "/" + audiopt.ClockRate;
            }

            if (param.VideoEnabled) {
                CallStats videoStats = call.GetStats(StreamType.Video);
                if (videoStats != null) {
                    videoDownloadBandwidth = String.Format("{0:0.00}", videoStats.DownloadBandwidth);
                    videoUploadBandwidth = String.Format("{0:0.00}", videoStats.UploadBandwidth);
                }

                PayloadType videopt = param.UsedVideoPayloadType;
                if (videopt != null) {
                    videoPayloadType = videopt.MimeType;
                }
                VideoDefinition receivedVideoSize = param.ReceivedVideoDefinition;
                String NewReceivedVideoSize = String.Format("{0}x{1}", receivedVideoSize.Width, receivedVideoSize.Height);
                String OldReceivedVideoSize = ((InCallModel)this.DataContext).ReceivedVideoSize;
                if (OldReceivedVideoSize != NewReceivedVideoSize) {
                    ((InCallModel)this.DataContext).ReceivedVideoSize = String.Format("{0}x{1}", receivedVideoSize.Width, receivedVideoSize.Height);
                    ((InCallModel)this.DataContext).IsVideoActive = false;
                    if (NewReceivedVideoSize != "0x0") {
                        ((InCallModel)this.DataContext).IsVideoActive = true;
                    }
                }
                VideoDefinition sentVideoSize = param.SentVideoDefinition;
                ((InCallModel)this.DataContext).SentVideoSize = String.Format("{0}x{1}", sentVideoSize.Width, sentVideoSize.Height);
                ((InCallModel)this.DataContext).VideoStatsVisibility = Visibility.Visible;
            } else {
                ((InCallModel)this.DataContext).VideoStatsVisibility = Visibility.Collapsed;
            }

            string downloadBandwidth = audioDownloadBandwidth;
            if ((downloadBandwidth != "") && (videoDownloadBandwidth != ""))
                downloadBandwidth += " - ";
            if (videoDownloadBandwidth != "")
                downloadBandwidth += videoDownloadBandwidth;
            ((InCallModel)this.DataContext).DownBandwidth = String.Format("{0} kb/s", downloadBandwidth);
            string uploadBandwidth = audioUploadBandwidth;
            if ((uploadBandwidth != "") && (videoUploadBandwidth != ""))
                uploadBandwidth += " - ";
            if (videoUploadBandwidth != "")
                uploadBandwidth += videoUploadBandwidth;
            ((InCallModel)this.DataContext).UpBandwidth = String.Format("{0} kb/s", uploadBandwidth);
            string payloadType = audioPayloadType;
            if ((payloadType != "") && (videoPayloadType != ""))
                payloadType += " - ";
            if (videoPayloadType != "")
                payloadType += videoPayloadType;
            ((InCallModel)this.DataContext).PayloadType = payloadType;
        }

        private void remoteVideo_MediaOpened_1(object sender, RoutedEventArgs e) {
            Debug.WriteLine("[InCall] RemoteVideo Opened: " + ((MediaElement)sender).Source.AbsoluteUri);
            ((InCallModel)this.DataContext).RemoteVideoOpened();
        }

        private void remoteVideo_MediaFailed_1(object sender, ExceptionRoutedEventArgs e) {
            Debug.WriteLine("[InCall] RemoteVideo Failed: " + e.ErrorMessage);
        }

        /// <summary>
        /// Do not allow user to leave the incall page while call is active
        /// </summary>
        /*protected override void OnBackKeyPress(CancelEventArgs e)
        {
            e.Cancel = true;
        }*/

        private void ButtonsFadeOutAnimation_Completed(object sender, object e) {
            /*((InCallModel)this.DataContext).HideButtonsAndPanel();
            Status.Visibility = Visibility.Collapsed;
            Contact.Visibility = Visibility.Collapsed;
            Number.Visibility = Visibility.Collapsed;*/
        }

        private void HideButtons(Object state) {
#pragma warning disable CS4014 // Dans la mesure où cet appel n'est pas attendu, l'exécution de la méthode actuelle continue avant la fin de l'appel
            Status.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                //ButtonsFadeOutAnimation.Begin();
            });
#pragma warning restore CS4014 // Dans la mesure où cet appel n'est pas attendu, l'exécution de la méthode actuelle continue avant la fin de l'appel
        }

        private void StartFadeTimer() {
            if (fadeTimer != null) {
                fadeTimer.Dispose();
            }
            if (!statsVisible) {
                fadeTimer = new Timer(new TimerCallback(HideButtons), null, 4000, Timeout.Infinite);
            }
        }

        private void StopFadeTimer() {
            if (fadeTimer != null) {
                fadeTimer.Dispose();
                fadeTimer = null;
            }
        }

        private void LayoutRoot_Tap(object sender, RoutedEventArgs e) {
            ((InCallModel)this.DataContext).ShowButtonsAndPanel();
            Status.Visibility = Visibility.Visible;
            Contact.Visibility = Visibility.Visible;
            //Number.Visibility = Visibility.Visible;
            if (((InCallModel)this.DataContext).VideoShown) {
                //ButtonsFadeInVideoAnimation.Begin();
                //StartFadeTimer();
            } else {
                //ButtonsFadeInAudioAnimation.Begin();
            }
        }

        private void DoubleAnimation_Completed(object sender, object e) {

        }


        /* new private void OrientationChanged(object sender, OrientationChangedEventArgs e)
         {
             InCallModel model = (InCallModel)ViewModel;
             remoteVideo.Width = LayoutRoot.ActualWidth;
             remoteVideo.Height = LayoutRoot.ActualHeight;
             HUD.Width = LayoutRoot.ActualWidth;
             HUD.Height = LayoutRoot.ActualHeight;
             model.OrientationChanged(sender, e);
         }*/

        private void ToggleFullScreenMode(bool fullScreen) {
            var view = ApplicationView.GetForCurrentView();
            if (!fullScreen) {
                view.ExitFullScreenMode();
            } else {
                if (view.TryEnterFullScreenMode()) {
                    Debug.WriteLine("Entering full screen mode");
                } else {
                    Debug.WriteLine("Failed to enter full screen mode");
                }
            }
        }
    }
}
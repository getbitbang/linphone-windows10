﻿using Linphone.Agents;
using Linphone.Core;
using Linphone.Model;
using Linphone.Resources;
using Microsoft.Xna.Framework.GamerServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Linphone.Views
{
    /// <summary>
    /// InCall page, displayed for both incoming and outgoing calls.
    /// </summary>
    public partial class InCall : BasePage, MuteChangedListener, PauseChangedListener, CallUpdatedByRemoteListener
    {
        private const string micOn = "/Assets/AppBar/mic.png";
        private const string micOff = "/Assets/AppBar/mic.png";
        private const string pauseOn = "/Assets/AppBar/play.png";
        private const string pauseOff = "/Assets/AppBar/pause.png";

        private Timer oneSecondTimer;
        private Timer fadeTimer;
        private DateTimeOffset startTime;

        /// <summary>
        /// Public constructor.
        /// </summary>
        public InCall()
            : base(new InCallModel())
        {
            InitializeComponent();

            var call = LinphoneManager.Instance.LinphoneCore.GetCurrentCall();
            if (call != null && call.GetState() == Core.LinphoneCallState.StreamsRunning)
            {
                PauseStateChanged(call, false, false);
            }

            buttons.HangUpClick += buttons_HangUpClick;
            buttons_landscape.HangUpClick += buttons_HangUpClick;
            buttons.StatsClick += buttons_StatsClick;
            buttons_landscape.StatsClick += buttons_StatsClick;
            buttons.CameraClick += buttons_CameraClick;
            buttons_landscape.CameraClick += buttons_CameraClick;
            buttons.PauseClick += buttons_PauseClick;
            buttons_landscape.PauseClick += buttons_PauseClick;
            buttons.SpeakerClick += buttons_SpeakerClick;
            buttons_landscape.SpeakerClick += buttons_SpeakerClick;
            buttons.MuteClick += buttons_MuteClick;
            buttons_landscape.MuteClick += buttons_MuteClick;
            buttons.VideoClick += buttons_VideoClick;
            buttons_landscape.VideoClick += buttons_VideoClick;
            buttons.DialpadClick += buttons_DialpadClick;
            buttons_landscape.DialpadClick += buttons_DialpadClick;
        }

        private void buttons_DialpadClick(object sender, bool isDialpadShown)
        {
            ((InCallModel)ViewModel).DialpadButtonToggled = isDialpadShown;
            ((InCallModel)ViewModel).NumpadVisibility = isDialpadShown ? Visibility.Visible : Visibility.Collapsed;
        }

        private void buttons_VideoClick(object sender, bool isVideoOn)
        {
            ((InCallModel)ViewModel).VideoButtonToggled = isVideoOn;
            if (!LinphoneManager.Instance.EnableVideo(isVideoOn))
            {
                ((InCallModel)ViewModel).VideoButtonToggled = !isVideoOn;
            }
        }

        private void buttons_MuteClick(object sender, bool isMuteOn)
        {
            ((InCallModel)ViewModel).MuteButtonToggled = isMuteOn;
            LinphoneManager.Instance.MuteMic(isMuteOn);
        }

        private bool buttons_SpeakerClick(object sender, bool isSpeakerOn)
        {
            ((InCallModel)ViewModel).SpeakerButtonToggled = isSpeakerOn;
            try
            {
                LinphoneManager.Instance.EnableSpeaker(isSpeakerOn);
                return true;
            }
            catch
            {
                Logger.Warn("Exception while trying to toggle speaker to {0}", isSpeakerOn.ToString());
                ((InCallModel)ViewModel).SpeakerButtonToggled = !isSpeakerOn;
            }
            return false;
        }

        private void buttons_PauseClick(object sender, bool isPaused)
        {
            ((InCallModel)ViewModel).PauseButtonToggled = isPaused;
            buttons.pauseImg.Source = new BitmapImage(new Uri(isPaused ? pauseOn : pauseOff, UriKind.RelativeOrAbsolute));
            buttons_landscape.pauseImg.Source = new BitmapImage(new Uri(isPaused ? pauseOn : pauseOff, UriKind.RelativeOrAbsolute));

            if (isPaused)
                LinphoneManager.Instance.PauseCurrentCall();
            else
                LinphoneManager.Instance.ResumeCurrentCall();
        }

        private void buttons_CameraClick(object sender)
        {
            ((InCallModel)ViewModel).ToggleCameras();
        }

        private void buttons_StatsClick(object sender, bool areStatsVisible)
        {
            ((InCallModel)ViewModel).StatsButtonToggled = areStatsVisible;
            ((InCallModel)ViewModel).ChangeStatsVisibility(areStatsVisible);
        }

        private void buttons_HangUpClick(object sender)
        {
            if (oneSecondTimer != null)
            {
                oneSecondTimer.Dispose();
            }
            LinphoneManager.Instance.EndCurrentCall();
        }

        /// <summary>
        /// Method called when the page is displayed.
        /// Searches for a matching contact using the current call address or number and display information if found.
        /// </summary>
        protected override async void OnNavigatedTo(NavigationEventArgs nee)
        {
            // Create LinphoneCore if not created yet, otherwise do nothing
            Task t = LinphoneManager.Instance.InitLinphoneCore();

            base.OnNavigatedTo(nee);
            this.ViewModel.MuteListener = this;
            this.ViewModel.PauseListener = this;
            this.ViewModel.CallUpdatedByRemoteListener = this;
            LinphoneManager.Instance.CallStateChanged += CallStateChanged;

            if (NavigationContext.QueryString.ContainsKey("sip"))
            {
                String calledNumber = NavigationContext.QueryString["sip"];
                if (calledNumber.StartsWith("sip:"))
                {
                    calledNumber = calledNumber.Substring(4);
                }
                // While we dunno if the number matches a contact one, we consider it won't and we display the phone number as username
                Contact.Text = calledNumber;

                if (calledNumber != null && calledNumber.Length > 0)
                {
                    ContactManager cm = ContactManager.Instance;
                    cm.ContactFound += cm_ContactFound;
                    cm.FindContact(calledNumber);
                }
            }

            await t;
        }

        private void CallStateChanged(LinphoneCall call, LinphoneCallState state)
        {
            if (state == LinphoneCallState.StreamsRunning)
            {
                buttons.pause.IsEnabled = true;
                buttons.microphone.IsEnabled = true;
                buttons_landscape.pause.IsEnabled = true;
                buttons_landscape.microphone.IsEnabled = true;
            }
            else if (state == LinphoneCallState.PausedByRemote)
            {
                buttons.pause.IsEnabled = false;
                buttons.microphone.IsEnabled = false;
                buttons_landscape.pause.IsEnabled = false;
                buttons_landscape.microphone.IsEnabled = false;
            }
            else if (state == LinphoneCallState.Paused)
            {
                buttons.microphone.IsEnabled = false;
                buttons_landscape.microphone.IsEnabled = false;
            }
        }

        /// <summary>
        /// Method called when the page is leaved.
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs nee)
        {
            if (oneSecondTimer != null)
            {
                oneSecondTimer.Dispose();
            }
            if (fadeTimer != null)
            {
                fadeTimer.Dispose();
                fadeTimer = null;
            }

            base.OnNavigatedFrom(nee);
            this.ViewModel.MuteListener = null;
            this.ViewModel.PauseListener = null;
            LinphoneManager.Instance.CallStateChanged -= CallStateChanged;
        }

        /// <summary>
        /// Callback called when the search on a phone number for a contact has a match
        /// </summary>
        private void cm_ContactFound(object sender, ContactFoundEventArgs e)
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
        }

        /// <summary>
        /// Called when the mute status of the microphone changes.
        /// </summary>
        public void MuteStateChanged(Boolean isMicMuted)
        {
            ((InCallModel)ViewModel).MuteButtonToggled = isMicMuted;
            buttons.microImg.Source = new BitmapImage(new Uri(isMicMuted ? micOn : micOff, UriKind.RelativeOrAbsolute));
            buttons_landscape.microImg.Source = new BitmapImage(new Uri(isMicMuted ? micOn : micOff, UriKind.RelativeOrAbsolute));
        }

        /// <summary>
        /// Called when the call changes its state to paused or resumed.
        /// </summary>
        public void PauseStateChanged(LinphoneCall call, bool isCallPaused, bool isCallPausedByRemote)
        {
            ((InCallModel)ViewModel).PauseButtonToggled = isCallPaused || isCallPausedByRemote;

            if (oneSecondTimer == null)
            {
                oneSecondTimer = new Timer(new TimerCallback(timerTick), null, 0, 1000);
            }

            if (!isCallPaused && !isCallPausedByRemote)
            {
                if (call.GetCurrentParamsCopy().IsVideoEnabled() && !((InCallModel)ViewModel).IsVideoActive)
                {
                    // Show video if it was not shown yet
                    ((InCallModel)ViewModel).IsVideoActive = true;
                    ((InCallModel)ViewModel).VideoButtonToggled = true;
                    ButtonsFadeInVideoAnimation.Begin();
                    StartFadeTimer();
                }
                else if (!call.GetCurrentParamsCopy().IsVideoEnabled() && ((InCallModel)ViewModel).IsVideoActive)
                {
                    // Stop video if it is no longer active
                    ((InCallModel)ViewModel).IsVideoActive = false;
                    ((InCallModel)ViewModel).VideoButtonToggled = false;
                    ButtonsFadeInAudioAnimation.Begin();
                    StopFadeTimer();
                }
            }
        }

        /// <summary>
        /// Called when the call is updated by the remote party.
        /// </summary>
        /// <param name="call">The call that has been updated</param>
        /// <param name="isVideoAdded">A boolean telling whether the remote party added video</param>
        public void CallUpdatedByRemote(LinphoneCall call, bool isVideoAdded)
        {
            if (isVideoAdded)
            {
                Guide.BeginShowMessageBox(AppResources.VideoActivationPopupCaption,
                    AppResources.VideoActivationPopupContent,
                    new List<String> { "Accept", "Dismiss" },
                    0,
                    MessageBoxIcon.Alert,
                    asyncResult =>
                    {
                        int? res = Guide.EndShowMessageBox(asyncResult);
                        LinphoneCallParams parameters = call.GetCurrentParamsCopy();
                        if (res == 0)
                        {
                            parameters.EnableVideo(true);
                        }
                        LinphoneManager.Instance.LinphoneCore.AcceptCallUpdate(call, parameters);
                    },
                    null);
            }
        }

        private void timerTick(Object state)
        {
            try
            {
                if (LinphoneManager.Instance.LinphoneCore.GetCallsNb() == 0)
                {
                    oneSecondTimer.Dispose();
                    oneSecondTimer = null;
                    return;
                }

                LinphoneCall call = LinphoneManager.Instance.LinphoneCore.GetCurrentCall();
                if (call == null)
                    call = (LinphoneCall)LinphoneManager.Instance.LinphoneCore.GetCalls()[0];
                if (call == null)
                    return;

                startTime = (DateTimeOffset)call.GetCallStartTimeFromContext();
                DateTimeOffset now = DateTimeOffset.Now;
                TimeSpan elapsed = now.Subtract(startTime);
                var ss = elapsed.Seconds;
                var mm = elapsed.Minutes;

                Status.Dispatcher.BeginInvoke(delegate()
                {
                    if (LinphoneManager.Instance.LinphoneCore.GetCallsNb() == 0)
                    {
                        return;
                    }

                    LinphoneCallParams param = call.GetCurrentParamsCopy();
                    Status.Text = mm.ToString("00") + ":" + ss.ToString("00");

                    MediaEncryption.Text = String.Format(AppResources.StatMediaEncryption + ": {0}", param.GetMediaEncryption().ToString());

                    LinphoneCallStats audioStats = null;
                    try
                    {
                        audioStats = call.GetAudioStats();
                    }
                    catch { }

                    if (audioStats != null)
                    {
                        AudioDownBw.Text = String.Format(AppResources.StatDownloadBW + ": {0:0.00} kb/s", audioStats.GetDownloadBandwidth());
                        AudioUpBw.Text = String.Format(AppResources.StatUploadBW + ": {0:0.00} kb/s", audioStats.GetUploadBandwidth());
                        ICE.Text = String.Format(AppResources.StatICE + ": {0}", audioStats.GetIceState().ToString()); 
                    }

                    PayloadType audiopt = param.GetUsedAudioCodec();
                    if (audiopt != null) 
                    {
                        AudioPType.Text = AppResources.StatPayload + ": " + audiopt.GetMimeType() + "/" + audiopt.GetClockRate();
                    }

                    if (param.IsVideoEnabled())
                    {
                        LinphoneCallStats videoStats = call.GetVideoStats();
                        if (videoStats != null)
                        {
                            VideoDownBw.Text = String.Format(AppResources.StatDownloadBW + ": {0:0.00} kb/s", videoStats.GetDownloadBandwidth());
                            VideoUpBw.Text = String.Format(AppResources.StatUploadBW + ": {0:0.00} kb/s", videoStats.GetUploadBandwidth());
                        }

                        PayloadType videopt = param.GetUsedVideoCodec();
                        if (videopt != null)
                        {
                            VideoPType.Text = AppResources.StatPayload + ": " + videopt.GetMimeType() + "/" + videopt.GetClockRate();
                        }

                        VideoStats.Visibility = Visibility.Visible;
                        VideoDownBw.Visibility = Visibility.Visible;
                        VideoUpBw.Visibility = Visibility.Visible;
                        VideoPType.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        VideoStats.Visibility = Visibility.Collapsed;
                        VideoDownBw.Visibility = Visibility.Collapsed;
                        VideoUpBw.Visibility = Visibility.Collapsed;
                        VideoPType.Visibility = Visibility.Collapsed;
                    }
                });
            } catch {
                oneSecondTimer.Dispose();
                oneSecondTimer = null;
            }
        }

        private void remoteVideo_MediaOpened_1(object sender, System.Windows.RoutedEventArgs e)
        {
            Logger.Msg("RemoteVideo Opened: " + ((MediaElement)sender).Source.AbsoluteUri);
        }

        private void remoteVideo_MediaFailed_1(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            Logger.Err("RemoteVideo Failed: " + e.ErrorException.Message);
        }

        private void localVideo_MediaOpened_1(object sender, System.Windows.RoutedEventArgs e)
        {
            Logger.Msg("LocalVideo Opened: " + ((MediaElement)sender).Source.AbsoluteUri);
        }

        private void localVideo_MediaFailed_1(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            Logger.Err("LocalVideo Failed: " + e.ErrorException.Message);
        }

        /// <summary>
        /// Do not allow user to leave the incall page while call is active
        /// </summary>
        protected override void OnBackKeyPress(CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private void ButtonsFadeOutAnimation_Completed(object sender, EventArgs e)
        {
            ((InCallModel)ViewModel).HideButtonsAndPanel();
            Status.Visibility = Visibility.Collapsed;
            Contact.Visibility = Visibility.Collapsed;
            Number.Visibility = Visibility.Collapsed;
        }

        private void HideButtons(Object state)
        {
            Status.Dispatcher.BeginInvoke(delegate()
            {
                ButtonsFadeOutAnimation.Begin();
            });
        }

        private void StartFadeTimer()
        {
            if (fadeTimer != null)
            {
                fadeTimer.Dispose();
            }
            fadeTimer = new Timer(new TimerCallback(HideButtons), null, 4000, Timeout.Infinite);
        }

        private void StopFadeTimer()
        {
            if (fadeTimer != null)
            {
                fadeTimer.Dispose();
                fadeTimer = null;
            }
        }

        private void LayoutRoot_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ((InCallModel)ViewModel).ShowButtonsAndPanel();
            Status.Visibility = Visibility.Visible;
            Contact.Visibility = Visibility.Visible;
            Number.Visibility = Visibility.Visible;
            if (((InCallModel)ViewModel).IsVideoActive)
            {
                ButtonsFadeInVideoAnimation.Begin();
                StartFadeTimer();
            }
            else
            {
                ButtonsFadeInAudioAnimation.Begin();
            }
        }

        new private void OrientationChanged(object sender, Microsoft.Phone.Controls.OrientationChangedEventArgs e)
        {
            ((InCallModel)ViewModel).OrientationChanged(sender, e);
        }
    }
}
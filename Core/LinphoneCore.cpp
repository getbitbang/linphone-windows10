#include "LinphoneCore.h"
#include "LinphoneAddress.h"
#include "LinphoneAuthInfo.h"
#include "LinphoneCall.h"
#include "LinphoneCallLog.h"
#include "LinphoneCallParams.h"
#include "LinphoneProxyConfig.h"
#include "LinphoneCoreListener.h"
#include "LpConfig.h"
#include "PayloadType.h"
#include "CallController.h"
#include "Tunnel.h"
#include "Server.h"
#include "Enums.h"
#include "ApiLock.h"
#include <collection.h>

using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::System::Threading;

Linphone::Core::Transports::Transports() :
	udp(5060),
	tcp(0),
	tls(0)
{
}

Linphone::Core::Transports::Transports(int udp_port, int tcp_port, int tls_port) :
	udp(udp_port),
	tcp(tcp_port),
	tls(tls_port)
{
}

Linphone::Core::Transports::Transports(Linphone::Core::Transports^ t) :
	udp(t->UDP),
	tcp(t->TCP),
	tls(t->TLS)
{
}

int Linphone::Core::Transports::UDP::get()
{
	return udp;
}

void Linphone::Core::Transports::UDP::set(int value)
{
	this->udp = value;
	this->tcp = 0;
	this->tls = 0;
}

int Linphone::Core::Transports::TCP::get()
{
	return tcp;
}

void Linphone::Core::Transports::TCP::set(int value)
{
	this->udp = 0;
	this->tcp = value;
	this->tls = 0;
}

int Linphone::Core::Transports::TLS::get()
{
	return tls;
}

void Linphone::Core::Transports::TLS::set(int value)
{
	this->udp = 0;
	this->tcp = 0;
	this->tls = value;
}

Platform::String^ Linphone::Core::Transports::ToString()
{
	return "udp[" + udp + "] tcp[" + tcp + "] tls[" + tls + "]";
}


void Linphone::Core::LinphoneCore::SetLogLevel(OutputTraceLevel logLevel)
{
	int coreLogLevel = 0;
	if (logLevel == OutputTraceLevel::Error) {
		coreLogLevel = ORTP_ERROR | ORTP_FATAL;
	}
	else if (logLevel == OutputTraceLevel::Warning) {
		coreLogLevel = ORTP_WARNING | ORTP_ERROR | ORTP_FATAL;
	}
	else if (logLevel == OutputTraceLevel::Message) {
		coreLogLevel = ORTP_MESSAGE | ORTP_WARNING | ORTP_ERROR | ORTP_FATAL;
	}
	else if (logLevel == OutputTraceLevel::Debug) {
		coreLogLevel = ORTP_DEBUG | ORTP_MESSAGE | ORTP_WARNING | ORTP_ERROR | ORTP_FATAL;
	}
	Utils::LinphoneCoreSetLogLevel(coreLogLevel);
}

void Linphone::Core::LinphoneCore::ClearProxyConfigs()
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_clear_proxy_config(this->lc);
}

void Linphone::Core::LinphoneCore::AddProxyConfig(Linphone::Core::LinphoneProxyConfig^ proxyCfg)
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_add_proxy_config(this->lc, proxyCfg->proxy_config);
}

void Linphone::Core::LinphoneCore::SetDefaultProxyConfig(Linphone::Core::LinphoneProxyConfig^ proxyCfg)
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_default_proxy(this->lc, proxyCfg->proxy_config);
}

Linphone::Core::LinphoneProxyConfig^ Linphone::Core::LinphoneCore::GetDefaultProxyConfig()
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	::LinphoneProxyConfig *proxy=NULL;
	linphone_core_get_default_proxy(this->lc,&proxy);
	if (proxy != nullptr) {
		LinphoneProxyConfig^ defaultProxy = ref new Linphone::Core::LinphoneProxyConfig(proxy);
		return defaultProxy;
	}
	return nullptr;
}

Linphone::Core::LinphoneProxyConfig^ Linphone::Core::LinphoneCore::CreateEmptyProxyConfig()
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	Linphone::Core::LinphoneProxyConfig^ proxyConfig = ref new Linphone::Core::LinphoneProxyConfig();
	return proxyConfig;
}

static void AddProxyConfigToVector(void *vProxyConfig, void *vector)
{
	::LinphoneProxyConfig *pc = (LinphoneProxyConfig *)vProxyConfig;
	Linphone::Core::RefToPtrProxy<IVector<Object^>^> *list = reinterpret_cast< Linphone::Core::RefToPtrProxy<IVector<Object^>^> *>(vector);
	IVector<Object^>^ proxyconfigs = (list) ? list->Ref() : nullptr;

	Linphone::Core::LinphoneProxyConfig^ proxyConfig = (Linphone::Core::LinphoneProxyConfig^)Linphone::Core::Utils::CreateLinphoneProxyConfig(pc);
	proxyconfigs->Append(proxyConfig);
}

IVector<Object^>^ Linphone::Core::LinphoneCore::GetProxyConfigList() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	IVector<Object^>^ proxyconfigs = ref new Vector<Object^>();
	const MSList *configList = linphone_core_get_proxy_config_list(this->lc);
	RefToPtrProxy<IVector<Object^>^> *proxyConfigPtr = new RefToPtrProxy<IVector<Object^>^>(proxyconfigs);
	ms_list_for_each2(configList, AddProxyConfigToVector, proxyConfigPtr);

	return proxyconfigs;
}

void Linphone::Core::LinphoneCore::ClearAuthInfos() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_clear_all_auth_info(this->lc);
}

void Linphone::Core::LinphoneCore::AddAuthInfo(Linphone::Core::LinphoneAuthInfo^ info) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_add_auth_info(this->lc, info->auth_info);
}

Linphone::Core::LinphoneAuthInfo^ Linphone::Core::LinphoneCore::CreateAuthInfo(Platform::String^ username, Platform::String^ userid, Platform::String^ password, Platform::String^ ha1, Platform::String^ realm)
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	Linphone::Core::LinphoneAuthInfo^ authInfo = ref new Linphone::Core::LinphoneAuthInfo(username, userid, password, ha1, realm);
	return authInfo;
}

static void AddAuthInfoToVector(void *vAuthInfo, void *vector)
{
	::LinphoneAuthInfo *ai = (LinphoneAuthInfo *)vAuthInfo;
	Linphone::Core::RefToPtrProxy<IVector<Object^>^> *list = reinterpret_cast< Linphone::Core::RefToPtrProxy<IVector<Object^>^> *>(vector);
	IVector<Object^>^ authInfos = (list) ? list->Ref() : nullptr;

	Linphone::Core::LinphoneAuthInfo^ authInfo = (Linphone::Core::LinphoneAuthInfo^)Linphone::Core::Utils::CreateLinphoneAuthInfo(ai);
	authInfos->Append(authInfo);
}

IVector<Object^>^ Linphone::Core::LinphoneCore::GetAuthInfos()
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	IVector<Object^>^ authInfos = ref new Vector<Object^>();
	const MSList *authlist = linphone_core_get_auth_info_list(this->lc);
	RefToPtrProxy<IVector<Object^>^> *authInfosPtr = new RefToPtrProxy<IVector<Object^>^>(authInfos);
	ms_list_for_each2(authlist, AddAuthInfoToVector, authInfosPtr);

	return authInfos;
}

void Linphone::Core::LinphoneCore::Destroy() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	IterateTimer->Cancel();
}

Linphone::Core::LinphoneAddress^ Linphone::Core::LinphoneCore::InterpretURL(Platform::String^ destination) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char* url = Linphone::Core::Utils::pstoccs(destination);
	Linphone::Core::LinphoneAddress^ addr = (Linphone::Core::LinphoneAddress^) Linphone::Core::Utils::CreateLinphoneAddress(linphone_core_interpret_url(this->lc, url));
	delete(url);

	return addr;
}

Linphone::Core::LinphoneCall^ Linphone::Core::LinphoneCore::Invite(Platform::String^ destination) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char *cc = Linphone::Core::Utils::pstoccs(destination);
	::LinphoneCall *call = linphone_core_invite(this->lc, cc);
	delete(cc);
	
	if(call != NULL)
	{
		Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *proxy = reinterpret_cast< Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *>(linphone_call_get_user_pointer(call));
		Linphone::Core::LinphoneCall^ lCall = (proxy) ? proxy->Ref() : nullptr;
		if (lCall == nullptr)
			lCall = (Linphone::Core::LinphoneCall^)Linphone::Core::Utils::CreateLinphoneCall(call);

		return lCall;
	}

	return nullptr;
}

Linphone::Core::LinphoneCall^ Linphone::Core::LinphoneCore::InviteAddress(Linphone::Core::LinphoneAddress^ to) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return (Linphone::Core::LinphoneCall^) Linphone::Core::Utils::CreateLinphoneCall(linphone_core_invite_address(this->lc, to->address));
}

Linphone::Core::LinphoneCall^ Linphone::Core::LinphoneCore::InviteAddressWithParams(Linphone::Core::LinphoneAddress^ destination, LinphoneCallParams^ params) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return (Linphone::Core::LinphoneCall^) Linphone::Core::Utils::CreateLinphoneCall(linphone_core_invite_address_with_params(this->lc, destination->address, params->params));
}

void Linphone::Core::LinphoneCore::TerminateCall(Linphone::Core::LinphoneCall^ call) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_terminate_call(this->lc, call->call);
}

Linphone::Core::LinphoneCall^ Linphone::Core::LinphoneCore::GetCurrentCall() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	::LinphoneCall *call = linphone_core_get_current_call(this->lc);
	if (call == nullptr)
		return nullptr;

	Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *proxy = reinterpret_cast< Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *>(linphone_call_get_user_pointer(call));
	Linphone::Core::LinphoneCall^ lCall = (proxy) ? proxy->Ref() : nullptr;

	return lCall;
}

Platform::Boolean Linphone::Core::LinphoneCore::IsInCall() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_in_call(this->lc);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsIncomingInvitePending() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_inc_invite_pending(this->lc);
}

void Linphone::Core::LinphoneCore::AcceptCall(Linphone::Core::LinphoneCall^ call) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_accept_call(this->lc, call->call);
}

void Linphone::Core::LinphoneCore::AcceptCallWithParams(Linphone::Core::LinphoneCall^ call, Linphone::Core::LinphoneCallParams^ params) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_accept_call_with_params(this->lc, call->call, params->params);
}

void Linphone::Core::LinphoneCore::AcceptCallUpdate(Linphone::Core::LinphoneCall^ call, Linphone::Core::LinphoneCallParams^ params) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_accept_call_update(this->lc, call->call, params->params);
}

void Linphone::Core::LinphoneCore::DeferCallUpdate(Linphone::Core::LinphoneCall^ call) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_defer_call_update(this->lc, call->call);
}

void Linphone::Core::LinphoneCore::UpdateCall(Linphone::Core::LinphoneCall^ call, Linphone::Core::LinphoneCallParams^ params) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_update_call(this->lc, call->call, params->params);
}

Linphone::Core::LinphoneCallParams^ Linphone::Core::LinphoneCore::CreateDefaultCallParameters() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return (Linphone::Core::LinphoneCallParams^) Linphone::Core::Utils::CreateLinphoneCallParams(linphone_core_create_default_call_parameters(this->lc));
}

void AddLogToVector(void* nLog, void* vector)
{
	::LinphoneCallLog *cl = (LinphoneCallLog*)nLog;
	Linphone::Core::RefToPtrProxy<IVector<Object^>^> *list = reinterpret_cast< Linphone::Core::RefToPtrProxy<IVector<Object^>^> *>(vector);
	IVector<Object^>^ logs = (list) ? list->Ref() : nullptr;

	Linphone::Core::LinphoneCallLog^ log = (Linphone::Core::LinphoneCallLog^)Linphone::Core::Utils::CreateLinphoneCallLog(cl);
	logs->Append(log);
}

IVector<Object^>^ Linphone::Core::LinphoneCore::GetCallLogs() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	IVector<Object^>^ logs = ref new Vector<Object^>();

	const MSList* logslist = linphone_core_get_call_logs(this->lc);
	RefToPtrProxy<IVector<Object^>^> *logsptr = new RefToPtrProxy<IVector<Object^>^>(logs);
	ms_list_for_each2(logslist, AddLogToVector, logsptr);

	return logs;
}

void Linphone::Core::LinphoneCore::ClearCallLogs() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_clear_call_logs(this->lc);
}

void Linphone::Core::LinphoneCore::RemoveCallLog(Linphone::Core::LinphoneCallLog^ log) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_remove_call_log(this->lc, log->callLog);
}

void Linphone::Core::LinphoneCore::SetNetworkReachable(Platform::Boolean isReachable) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_network_reachable(this->lc, isReachable);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsNetworkReachable() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_is_network_reachable(this->lc);
}

void Linphone::Core::LinphoneCore::SetMicrophoneGain(float gain) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_mic_gain_db(this->lc, gain);
}

void Linphone::Core::LinphoneCore::SetPlaybackGain(float gain) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_playback_gain_db(this->lc, gain);
}

float Linphone::Core::LinphoneCore::GetPlaybackGain() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_get_playback_gain_db(this->lc);
}

void Linphone::Core::LinphoneCore::SetPlayLevel(int level) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_play_level(this->lc, level);
}

int Linphone::Core::LinphoneCore::GetPlayLevel() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_get_play_level(this->lc);
}

void Linphone::Core::LinphoneCore::MuteMic(Platform::Boolean isMuted) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_mute_mic(this->lc, isMuted);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsMicMuted() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_is_mic_muted(this->lc);
}

void Linphone::Core::LinphoneCore::SendDTMF(char16 number) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_send_dtmf(this->lc, number);
}

void Linphone::Core::LinphoneCore::PlayDTMF(char16 number, int duration) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_play_dtmf(this->lc, number, duration);
}

void Linphone::Core::LinphoneCore::StopDTMF() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_stop_dtmf(this->lc);
}

Linphone::Core::PayloadType^ Linphone::Core::LinphoneCore::FindPayloadType(Platform::String^ mime, int clockRate, int channels) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char* type = Linphone::Core::Utils::pstoccs(mime);
	::PayloadType* pt = linphone_core_find_payload_type(this->lc, type, clockRate, channels);
	delete type;

	return ref new Linphone::Core::PayloadType(pt);
}

Linphone::Core::PayloadType^ Linphone::Core::LinphoneCore::FindPayloadType(Platform::String^ mime, int clockRate) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char* type = Linphone::Core::Utils::pstoccs(mime);
	::PayloadType* pt = linphone_core_find_payload_type(this->lc, type, clockRate, LINPHONE_FIND_PAYLOAD_IGNORE_CHANNELS);
	delete type;

	return ref new Linphone::Core::PayloadType(pt);
}

bool Linphone::Core::LinphoneCore::PayloadTypeEnabled(PayloadType^ pt)
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	::PayloadType *payload = pt->payload;
	return linphone_core_payload_type_enabled(this->lc, payload);
}

void Linphone::Core::LinphoneCore::EnablePayloadType(PayloadType^ pt, Platform::Boolean enable) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	::PayloadType *payload = pt->payload;
	linphone_core_enable_payload_type(this->lc, payload, enable);
}

static void AddCodecToVector(void *vCodec, void *vector)
{
	::PayloadType *pt = (PayloadType *)vCodec;
	Linphone::Core::RefToPtrProxy<IVector<Object^>^> *list = reinterpret_cast< Linphone::Core::RefToPtrProxy<IVector<Object^>^> *>(vector);
	IVector<Object^>^ codecs = (list) ? list->Ref() : nullptr;

	Linphone::Core::PayloadType^ codec = (Linphone::Core::PayloadType^)Linphone::Core::Utils::CreatePayloadType(pt);
	codecs->Append(codec);
}

IVector<Object^>^ Linphone::Core::LinphoneCore::GetAudioCodecs()
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	IVector<Object^>^ codecs = ref new Vector<Object^>();
	const MSList *codecslist = linphone_core_get_audio_codecs(this->lc);
	RefToPtrProxy<IVector<Object^>^> *codecsPtr = new RefToPtrProxy<IVector<Object^>^>(codecs);
	ms_list_for_each2(codecslist, AddCodecToVector, codecsPtr);

	return codecs;
}

void Linphone::Core::LinphoneCore::EnableEchoCancellation(Platform::Boolean enable) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_enable_echo_cancellation(this->lc, enable);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsEchoCancellationEnabled() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_echo_cancellation_enabled(this->lc);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsEchoLimiterEnabled() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_echo_limiter_enabled(this->lc);
}

void Linphone::Core::LinphoneCore::StartEchoCalibration(Platform::Object^ data) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	//TODO
}

void Linphone::Core::LinphoneCore::EnableEchoLimiter(Platform::Boolean enable) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_enable_echo_limiter(this->lc, enable);
}

void Linphone::Core::LinphoneCore::SetSignalingTransportsPorts(Transports^ t) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	::LCSipTransports transports;
	memset(&transports, 0, sizeof(LCSipTransports));
	transports.udp_port = t->UDP;
	transports.tcp_port = t->TCP;
	transports.tls_port = t->TLS;
	linphone_core_set_sip_transports(this->lc, &transports);
}

Linphone::Core::Transports^ Linphone::Core::LinphoneCore::GetSignalingTransportsPorts()
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	::LCSipTransports transports;
	linphone_core_get_sip_transports(this->lc, &transports);
	return ref new Transports(transports.udp_port, transports.tcp_port, transports.tls_port);
}

void Linphone::Core::LinphoneCore::EnableIPv6(Platform::Boolean enable) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_enable_ipv6(this->lc, enable);
}

void Linphone::Core::LinphoneCore::SetPresenceInfo(int minuteAway, Platform::String^ alternativeContact, OnlineStatus status) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char* ac = Linphone::Core::Utils::pstoccs(alternativeContact);
	linphone_core_set_presence_info(this->lc, minuteAway, ac, (LinphoneOnlineStatus) status);
	delete(ac);
}

void Linphone::Core::LinphoneCore::SetStunServer(Platform::String^ stun) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	const char* stunserver = Linphone::Core::Utils::pstoccs(stun);
	linphone_core_set_stun_server(this->lc, stunserver);
	delete(stunserver);
}

Platform::String^ Linphone::Core::LinphoneCore::GetStunServer() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return Linphone::Core::Utils::cctops(linphone_core_get_stun_server(this->lc));
}

void Linphone::Core::LinphoneCore::SetFirewallPolicy(FirewallPolicy policy) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_firewall_policy(this->lc, (LinphoneFirewallPolicy) policy);
}

Linphone::Core::FirewallPolicy Linphone::Core::LinphoneCore::GetFirewallPolicy() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return (FirewallPolicy) linphone_core_get_firewall_policy(this->lc);
}

void Linphone::Core::LinphoneCore::SetRootCA(Platform::String^ path) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char *ccPath = Utils::pstoccs(path);
	linphone_core_set_root_ca(this->lc, ccPath);
	delete ccPath;
}

void Linphone::Core::LinphoneCore::SetUploadBandwidth(int bw) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_upload_bandwidth(this->lc, bw);
}

void Linphone::Core::LinphoneCore::SetDownloadBandwidth(int bw) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_download_bandwidth(this->lc, bw);
}

void Linphone::Core::LinphoneCore::SetDownloadPTime(int ptime) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_download_ptime(this->lc, ptime);
}

void Linphone::Core::LinphoneCore::SetUploadPTime(int ptime) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_upload_ptime(this->lc, ptime);
}

void Linphone::Core::LinphoneCore::EnableKeepAlive(Platform::Boolean enable)
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_enable_keep_alive(this->lc, enable);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsKeepAliveEnabled() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_keep_alive_enabled(this->lc);
}

void Linphone::Core::LinphoneCore::SetPlayFile(Platform::String^ path) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char* file = Linphone::Core::Utils::pstoccs(path);
	linphone_core_set_play_file(this->lc, file);
	delete(file);
}

Platform::Boolean Linphone::Core::LinphoneCore::PauseCall(LinphoneCall^ call) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_pause_call(this->lc, call->call);
}

Platform::Boolean Linphone::Core::LinphoneCore::ResumeCall(LinphoneCall^ call) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_resume_call(this->lc, call->call);;
}

Platform::Boolean Linphone::Core::LinphoneCore::PauseAllCalls() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_pause_all_calls(this->lc);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsInConference() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_is_in_conference(this->lc);
}

Platform::Boolean Linphone::Core::LinphoneCore::EnterConference() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_enter_conference(this->lc);
}

void Linphone::Core::LinphoneCore::LeaveConference() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_leave_conference(this->lc);
}

void Linphone::Core::LinphoneCore::AddToConference(LinphoneCall^ call) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_add_to_conference(this->lc, call->call);
}

void Linphone::Core::LinphoneCore::AddAllToConference() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_add_all_to_conference(this->lc);
}

void Linphone::Core::LinphoneCore::RemoveFromConference(LinphoneCall^ call) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_remove_from_conference(this->lc, call->call);
}

void Linphone::Core::LinphoneCore::TerminateConference() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_terminate_conference(this->lc);
}

int Linphone::Core::LinphoneCore::GetConferenceSize() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_get_conference_size(this->lc);
}

void Linphone::Core::LinphoneCore::TerminateAllCalls() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_terminate_all_calls(this->lc);
}

static void AddCallToVector(void *vCall, void *vector)
{
	::LinphoneCall* c = (::LinphoneCall *)vCall;
	Linphone::Core::RefToPtrProxy<IVector<Object^>^> *list = reinterpret_cast< Linphone::Core::RefToPtrProxy<IVector<Object^>^> *>(vector);
	IVector<Object^>^ calls = (list) ? list->Ref() : nullptr;

	Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *proxy = reinterpret_cast< Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *>(linphone_call_get_user_pointer(c));
	Linphone::Core::LinphoneCall^ call = (proxy) ? proxy->Ref() : nullptr;
	calls->Append(call);
}

IVector<Object^>^ Linphone::Core::LinphoneCore::GetCalls() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	
	Vector<Object^>^ calls = ref new Vector<Object^>();
	const MSList *callsList = linphone_core_get_calls(this->lc);
	RefToPtrProxy<IVector<Object^>^> *callsPtr = new RefToPtrProxy<IVector<Object^>^>(calls);
	ms_list_for_each2(callsList, AddCallToVector, callsPtr);

	return calls;
}

int Linphone::Core::LinphoneCore::GetCallsNb() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_get_calls_nb(this->lc);
}

Linphone::Core::LinphoneCall^ Linphone::Core::LinphoneCore::FindCallFromUri(Platform::String^ uri) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return nullptr;
}

int Linphone::Core::LinphoneCore::GetMaxCalls() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_get_max_calls(this->lc);
}

void Linphone::Core::LinphoneCore::SetMaxCalls(int max) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_max_calls(this->lc, max);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsMyself(Platform::String^ uri) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	LinphoneProxyConfig^ lpc = GetDefaultProxyConfig();

	if (lpc == nullptr)
		return false;

	return uri->Equals(lpc->GetIdentity());
}

Platform::Boolean Linphone::Core::LinphoneCore::IsSoundResourcesLocked() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_sound_resources_locked(this->lc);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsMediaEncryptionSupported(MediaEncryption menc) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_media_encryption_supported(this->lc, (LinphoneMediaEncryption) menc);
}

void Linphone::Core::LinphoneCore::SetMediaEncryption(MediaEncryption menc) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_media_encryption(this->lc, (LinphoneMediaEncryption) menc);
}

Linphone::Core::MediaEncryption Linphone::Core::LinphoneCore::GetMediaEncryption() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return (MediaEncryption) linphone_core_get_media_encryption(this->lc);
}

void Linphone::Core::LinphoneCore::SetMediaEncryptionMandatory(Platform::Boolean yesno) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_media_encryption_mandatory(this->lc, yesno);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsMediaEncryptionMandatory() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_is_media_encryption_mandatory(this->lc);
}

Platform::Boolean Linphone::Core::LinphoneCore::IsTunnelAvailable() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_tunnel_available();
}

Linphone::Core::Tunnel^ Linphone::Core::LinphoneCore::GetTunnel()
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	LinphoneTunnel *lt = linphone_core_get_tunnel(this->lc);
	if (lt == nullptr)
		return nullptr;
	return ref new Linphone::Core::Tunnel(lt);
}

void Linphone::Core::LinphoneCore::SetUserAgent(Platform::String^ name, Platform::String^ version) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);

	const char* ua = Linphone::Core::Utils::pstoccs(name);
	const char* v = Linphone::Core::Utils::pstoccs(version);
	linphone_core_set_user_agent(this->lc, ua, v);
	delete(v);
	delete(ua);
}

void Linphone::Core::LinphoneCore::SetCPUCount(int count) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	ms_set_cpu_count(count);
}

int Linphone::Core::LinphoneCore::GetMissedCallsCount() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return linphone_core_get_missed_calls_count(this->lc);
}

void Linphone::Core::LinphoneCore::ResetMissedCallsCount() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_reset_missed_calls_count(this->lc);
}

void Linphone::Core::LinphoneCore::RefreshRegisters() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_refresh_registers(this->lc);
}

Platform::String^ Linphone::Core::LinphoneCore::GetVersion() 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	return Linphone::Core::Utils::cctops(linphone_core_get_version());
}

void Linphone::Core::LinphoneCore::SetAudioPort(int port) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_audio_port(this->lc, port);
}

void Linphone::Core::LinphoneCore::SetAudioPortRange(int minP, int maxP) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_audio_port_range(this->lc, minP, maxP);
}

void Linphone::Core::LinphoneCore::SetIncomingTimeout(int timeout) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_inc_timeout(this->lc, timeout);
}

void Linphone::Core::LinphoneCore::SetInCallTimeout(int timeout) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_in_call_timeout(this->lc, timeout);
}

void Linphone::Core::LinphoneCore::SetPrimaryContact(Platform::String^ displayName, Platform::String^ userName) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	
	const char* dn = Linphone::Core::Utils::pstoccs(displayName);
	const char* un = Linphone::Core::Utils::pstoccs(userName);

	::LinphoneAddress* addr = linphone_core_get_primary_contact_parsed(this->lc);
	if (addr != nullptr) {
		linphone_address_set_display_name(addr, dn);
		linphone_address_set_username(addr, un);
		char* contact = linphone_address_as_string(addr);
		linphone_core_set_primary_contact(this->lc, contact);
	}

	delete(un);
	delete(dn);
}

void Linphone::Core::LinphoneCore::SetUseSipInfoForDTMFs(Platform::Boolean use) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_use_info_for_dtmf(this->lc, use);
}

void Linphone::Core::LinphoneCore::SetUseRFC2833ForDTMFs(Platform::Boolean use) 
{
	std::lock_guard<std::recursive_mutex> lock(g_apiLock);
	linphone_core_set_use_rfc2833_for_dtmf(this->lc, use);
}

Linphone::Core::LpConfig^ Linphone::Core::LinphoneCore::GetConfig()
{
	::LpConfig *config = linphone_core_get_config(this->lc);
	return (Linphone::Core::LpConfig^)Linphone::Core::Utils::CreateLpConfig(config);
}

Linphone::Core::LinphoneCoreListener^ Linphone::Core::LinphoneCore::CoreListener::get()
{
	return this->listener;
}

void Linphone::Core::LinphoneCore::CoreListener::set(LinphoneCoreListener^ listener)
{
	this->listener = listener;
}

void call_state_changed(::LinphoneCore *lc, ::LinphoneCall *call, ::LinphoneCallState cstate, const char *msg) 
{	
	Linphone::Core::LinphoneCallState state = (Linphone::Core::LinphoneCallState) cstate;
	Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *proxy = reinterpret_cast< Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneCall^> *>(linphone_call_get_user_pointer(call));
	Linphone::Core::LinphoneCall^ lCall = (proxy) ? proxy->Ref() : nullptr;
	if (lCall == nullptr) {
		lCall = (Linphone::Core::LinphoneCall^)Linphone::Core::Utils::CreateLinphoneCall(call);
	}
		
	Linphone::Core::CallController^ callController = Linphone::Core::Globals::Instance->CallController;
	if (state == Linphone::Core::LinphoneCallState::IncomingReceived) {
		Windows::Phone::Networking::Voip::VoipPhoneCall^ platformCall = callController->OnIncomingCallReceived(lCall, lCall->GetRemoteAddress()->GetDisplayName(), lCall->GetRemoteAddress()->AsStringUriOnly(), callController->IncomingCallViewDismissed);
		lCall->CallContext = platformCall;
	} 
	else if (state == Linphone::Core::LinphoneCallState::OutgoingProgress) {
		Windows::Phone::Networking::Voip::VoipPhoneCall^ platformCall = callController->NewOutgoingCall(lCall->GetRemoteAddress()->AsStringUriOnly());
		lCall->CallContext = platformCall;
	}
	else if (state == Linphone::Core::LinphoneCallState::CallEnd || state == Linphone::Core::LinphoneCallState::Error) {
		Windows::Phone::Networking::Voip::VoipPhoneCall^ platformCall = (Windows::Phone::Networking::Voip::VoipPhoneCall^) lCall->CallContext;
		platformCall->NotifyCallEnded();

		if (callController->IncomingCallViewDismissed != nullptr) {
			// When we receive a call with PN, call the callback to kill the agent process in case the caller stops the call before user accepts/denies it
			callController->IncomingCallViewDismissed();
		}
	}
	else if (state == Linphone::Core::LinphoneCallState::Paused || state == Linphone::Core::LinphoneCallState::PausedByRemote) {
		Windows::Phone::Networking::Voip::VoipPhoneCall^ platformCall = (Windows::Phone::Networking::Voip::VoipPhoneCall^) lCall->CallContext;
		platformCall->NotifyCallHeld();
	}
	else if (state == Linphone::Core::LinphoneCallState::StreamsRunning) {
		Windows::Phone::Networking::Voip::VoipPhoneCall^ platformCall = (Windows::Phone::Networking::Voip::VoipPhoneCall^) lCall->CallContext;
		platformCall->NotifyCallActive();
	}
	
	Linphone::Core::LinphoneCoreListener^ listener = Linphone::Core::Globals::Instance->LinphoneCore->CoreListener;
	if (listener != nullptr)
	{
		listener->CallState(lCall, state);
	}
}

void registration_state_changed(::LinphoneCore *lc, ::LinphoneProxyConfig *cfg, ::LinphoneRegistrationState cstate, const char *msg)
{
	Linphone::Core::LinphoneCoreListener^ listener = Linphone::Core::Globals::Instance->LinphoneCore->CoreListener;
	if (listener != nullptr)
	{
		Linphone::Core::RegistrationState state = (Linphone::Core::RegistrationState) cstate;
		Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneProxyConfig^> *proxy = reinterpret_cast< Linphone::Core::RefToPtrProxy<Linphone::Core::LinphoneProxyConfig^> *>(linphone_proxy_config_get_user_data(cfg));
		Linphone::Core::LinphoneProxyConfig^ config = (proxy) ? proxy->Ref() : nullptr;
		listener->RegistrationState(config, state, Linphone::Core::Utils::cctops(msg));
	}
}

void global_state_changed(::LinphoneCore *lc, ::LinphoneGlobalState gstate, const char *msg)
{
	Linphone::Core::LinphoneCoreListener^ listener = Linphone::Core::Globals::Instance->LinphoneCore->CoreListener;
	if (listener != nullptr)
	{
		Linphone::Core::GlobalState state = (Linphone::Core::GlobalState) gstate;
		listener->GlobalState(state, Linphone::Core::Utils::cctops(msg));
	}
}

void auth_info_requested(LinphoneCore *lc, const char *realm, const char *username) 
{
	Linphone::Core::LinphoneCoreListener^ listener = Linphone::Core::Globals::Instance->LinphoneCore->CoreListener;
	if (listener != nullptr)
	{
		listener->AuthInfoRequested(Linphone::Core::Utils::cctops(realm), Linphone::Core::Utils::cctops(username));
	}
}

Linphone::Core::LinphoneCore::LinphoneCore(LinphoneCoreListener^ coreListener) :
	lc(nullptr),
	listener(coreListener)
{

}

Linphone::Core::LinphoneCore::LinphoneCore(LinphoneCoreListener^ coreListener, LpConfig^ config) :
	lc(nullptr),
	listener(coreListener),
	config(config)
{
}

void Linphone::Core::LinphoneCore::Init()
{
	LinphoneCoreVTable *vtable = (LinphoneCoreVTable*) malloc(sizeof(LinphoneCoreVTable));
	memset (vtable, 0, sizeof(LinphoneCoreVTable));
	vtable->global_state_changed = global_state_changed;
	vtable->registration_state_changed = registration_state_changed;
	vtable->call_state_changed = call_state_changed;
	vtable->auth_info_requested = auth_info_requested;

	this->lc = linphone_core_new_with_config(vtable, config ? config->config : NULL, NULL);
	
	// Launch iterate timer
	TimeSpan period;
	period.Duration = 20 * 10000;
	IterateTimer = ThreadPoolTimer::CreatePeriodicTimer(
		ref new TimerElapsedHandler([this](ThreadPoolTimer^ source)
		{
			if (source == IterateTimer) {
				std::lock_guard<std::recursive_mutex> lock(g_apiLock);
				linphone_core_iterate(this->lc);
			}
		}), period);
}

Linphone::Core::LinphoneCore::~LinphoneCore()
{
	
}

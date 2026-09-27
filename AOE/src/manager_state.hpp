#pragma once
#include "profile.hpp"
#include "settings.hpp"

namespace aoe {
enum class ManagerTab { ResearchRecovery=0,Profiles=1,Play=2 };
constexpr int ManagerTabCount=3;
const wchar_t* ManagerTabName(ManagerTab tab);
enum class SessionLifecycle { WaitingForProcess,ProcessFound,VerifyingTarget,ProfileLoading,ProfileValidating,Attaching,Attached,Arming,Armed,Running,SessionEnded,Invalidating };
const char* SessionLifecycleName(SessionLifecycle phase);
struct ManagerSession {
    uint64_t generation=0,processHandleValue=0;
    SessionLifecycle lifecycle=SessionLifecycle::WaitingForProcess;
    bool active=false,profileValid=false,debuggerAttached=false,runtimeArmed=false,breakpointsInstalled=false,debugRegisterBookkeeping=false;
    uint32_t pid=0;
    uint64_t creationTime=0,moduleBase=0;
    std::string sha256,lastEndReason;
    RuntimeLayout runtime;
    std::vector<ResolvedAnchor> anchors;
    std::vector<uint32_t> threadIds;
    Initial2xExperiment experiment;
};
bool BeginManagerSession(const BuildProfile& profile,const TargetInfo& target,ManagerSession& session,std::string& error);
bool ManagerSessionMatches(const ManagerSession& session,const TargetInfo& target);
void InvalidateManagerSession(ManagerSession& session,const std::string& reason={});
void ApplyDebuggerSnapshot(ManagerSession& session,uint64_t processHandleValue,const std::vector<uint32_t>& threadIds,bool breakpointsInstalled,bool debugRegisterBookkeeping);
bool MarkRuntimeArmed(ManagerSession& session,const Initial2xExperiment& experiment,std::string& error);
bool CanDisplayAutoArmed(const ManagerSession& session);
enum class AutoAction { None,Attach,Arm };
AutoAction NextAutoAction(const ManagerSettings& settings,bool processPresent,bool knownProfile,bool profileValid,bool attached,bool armed);
}

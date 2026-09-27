#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace aoe {
struct DiagnosticAddress { std::string name;uint64_t rva=0,absolute=0; };
struct DiagnosticBreakpoint { uint32_t threadId=0;uint64_t rva=0,absolute=0;unsigned drSlot=0;bool validation=false,replay=false,producer=false,visual=false,inspector=false; };
struct DiagnosticsSnapshot {
    uint64_t sessionGeneration=0,processCreationTime=0,moduleBase=0,processHandle=0;
    uint32_t pid=0,initialCalls=0,observedInitialCalls=0,breakpointThreads=0,breakpointSlots=0;
    std::string lifecycle,targetSha256,profileStatus,profileName,lastSessionEndReason,lastAutoArmError,lastReattachError,configurationDetails,lastExperimentDetails,nextExperimentDetails,liveValidationDetails,experimentDetails,runtimeMessage,runtimeError;
    bool debuggerAttached=false,breakpointsInstalled=false,runtimeArmed=false,configuredAutoArm=false;
    uint64_t initial0209Count=0,repeated020ACount=0,sharedCallSiteHits=0,aoeLifecycleCallSiteHits=0;
    uint32_t requestedRepeatCount=0,completedRepeatCount=0;
    std::vector<uint32_t> threadIds;
    std::vector<DiagnosticAddress> addresses;
    std::vector<DiagnosticBreakpoint> breakpointCoverage;
};
std::string RenderDiagnosticsSnapshot(const DiagnosticsSnapshot& snapshot);
class DiagnosticsRenderCache {
public:
    bool update(const std::string& snapshot,bool userInteracting);
    bool setFrozen(bool frozen,std::string& latestToRender);
    bool frozen()const{return frozen_;}
    const std::string& latest()const{return latest_;}
    const std::string& rendered()const{return rendered_;}
private:
    bool frozen_=false;
    std::string latest_,rendered_;
};
}

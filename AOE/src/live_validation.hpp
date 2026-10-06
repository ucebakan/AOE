#pragma once
#include "model.hpp"
#include "operation_observer.hpp"
#include "profile.hpp"

namespace aoe {
inline constexpr uint64_t RequiredPeriodic020AForLiveValidation=9;

struct LiveValidationState {
    AoeSkillFamily skillFamily=AoeSkillFamily::Unknown;
    uint32_t pid=0;
    uint64_t creationTime=0,moduleBase=0;
    std::string targetSha256,profileIdentity;
    bool profileFingerprintsPassed=false;
    bool seenInitial0209=false,seenPeriodic020A=false;
    bool seenPrepAnchor=false,seenCallAnchor=false,seenReturnAnchor=false;
    bool registerSemanticsValid=false,replayCriticalValidationPassed=false;
    bool validationCastInProgress=false,validationCastComplete=false;
    bool awaitingReturn=false,pendingRegisterValidation=false;
    uint32_t pendingThreadId=0;
    OperationClassification pendingClassification=OperationClassification::Unreadable;
    uint64_t initial0209Count=0,periodic020ACount=0,otherOperationCount=0,unreadableOperationCount=0;
    uint64_t periodic020AReturnCount=0;
    uint64_t producer0209Count=0,producer020ACount=0,producerOtherCount=0,producerUnreadableCount=0;
    uint64_t prepHitCount=0,callHitCount=0,returnHitCount=0;
    InitialCallSnapshot validationInitialCall;
    InitialCallSnapshot pendingCall;
    std::string blockingReason,registerFailureReason;
};

std::string LiveValidationProfileIdentity(const BuildProfile& profile);
bool LiveValidationSessionMatches(const LiveValidationState& state,const TargetInfo& target,const std::string& profileIdentity);
bool EnsureLiveValidationSession(LiveValidationState& state,const TargetInfo& target,const std::string& profileIdentity);
void MarkLiveProfileFingerprintsPassed(LiveValidationState& state);
bool BindLiveSkillFamily(LiveValidationState& state,AoeSkillFamily family);
void ObserveLivePrep(LiveValidationState& state,uint32_t threadId,int64_t qpc);
void ObserveLiveCall(LiveValidationState& state,const OperationObservation& operation,const InitialCallSnapshot* registers);
void ObserveLiveReturn(LiveValidationState& state,uint32_t threadId,uint64_t rsp,uint64_t r12,uint64_t r13,uint64_t r14,uint64_t r15,uint64_t rsi,uint64_t rdi,uint64_t rbp=0);
void ObserveLiveProducer(LiveValidationState& state,const OperationObservation& operation);
void RecomputeLiveValidation(LiveValidationState& state);
bool CanArmInitialNxFromValidation(const LiveValidationState& state,const TargetInfo& target,const std::string& profileIdentity);
bool ShouldAutoArmAfterValidation(const LiveValidationState& state,bool configuredAutoArm,bool alreadyArmed,bool validationRequired);
std::string LiveValidationDetails(const LiveValidationState& state,bool configuredAutoArm,bool runtimeArmed,bool validationRequired);
}

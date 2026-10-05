#pragma once
#include "model.hpp"
#include <functional>

namespace aoe {
enum class VisualRuntimePhase { NotLocated, LocatorReady, WaitingForAoeCast, TSkillResolved, VisualHidden, VisualNormal, RestoreFailed, SessionChanged };
const char* VisualRuntimePhaseName(VisualRuntimePhase phase);

struct VisualRuntimeState {
    VisualRuntimePhase phase=VisualRuntimePhase::NotLocated;
    bool locatorReady=false,captureBreakpointActive=false,readerCoverageActive=false,resolved=false,suppressionRequested=false,downstreamOriginalActive=false,restoreRequired=false;
    uint32_t pid=0,pendingThreadId=0,downstreamThreadId=0;
    uint64_t creationTime=0,moduleBase=0,readerRva=0,lookupRva=0,fieldOffset=0;
    X64RegisterId baseRegister=X64RegisterId::Invalid;
    std::string targetSha256,error;
    uint64_t pendingTSkill=0,pendingVisualField=0,tSkill=0,visualField=0;
    uint32_t pendingOriginalValue=0,originalValue=0;
    bool pendingSeenPrep=false;
};
void InitializeVisualRuntime(VisualRuntimeState& state,const TargetInfo& target);
bool ObserveVisualReader(VisualRuntimeState& state,uint32_t threadId,uint64_t tSkill,uint32_t currentValue,std::string& error);
void ObserveVisualPreparation(VisualRuntimeState& state,uint32_t threadId);
bool ConfirmVisualAoeCall(VisualRuntimeState& state,uint32_t threadId,bool operationReadable,uint16_t operationWord);
void InvalidateVisualRuntime(VisualRuntimeState& state,VisualRuntimePhase phase,const std::string& reason);

struct VisualSessionIdentity {uint32_t pid=0;uint64_t creationTime=0,moduleBase=0,generation=0;std::string targetSha256;};
struct VisualSessionCache {
    bool valid=false,hidden=false;
    VisualSessionIdentity identity;
    uint64_t tSkill=0,visualField=0,fieldOffset=0;
    uint32_t originalValue=0;
    std::string error;
};
bool SameVisualSession(const VisualSessionIdentity& a,const VisualSessionIdentity& b);
bool BindVisualSessionCache(VisualSessionCache& cache,const VisualRuntimeState& runtime,const VisualSessionIdentity& identity,std::string& error);
void InvalidateVisualSessionCache(VisualSessionCache& cache,const std::string& reason={});
struct VisualMemoryIo {
    std::function<bool(uint64_t,uint32_t&,std::string&)> read;
    std::function<bool(uint64_t,uint32_t,std::string&)> write;
};
struct InitialVisualBreakpointPlan {
    uint64_t fourthRva=0;
    size_t requiredSlots=3;
    bool keepAfterInitialTerminal=false;
    bool observesProducer=true;
};
InitialVisualBreakpointPlan PlanInitialVisualBreakpoints(const VisualRuntimeState& visual,const RuntimeLayout& layout);
// Called by the debugger worker while the target's threads are stopped.
bool ApplyVisualSuppression(VisualRuntimeState& visual,const VisualMemoryIo& io,std::string& error);
bool BeginVisualDamageWindow(VisualRuntimeState& visual,uint32_t threadId,const VisualMemoryIo& io,std::string& error);
bool EndVisualDamageWindow(VisualRuntimeState& visual,uint32_t threadId,bool redirect,const VisualMemoryIo& io,std::string& error);
bool SetVisualHidden(VisualSessionCache& cache,const VisualSessionIdentity& current,bool hidden,const VisualMemoryIo& io,std::string& error);
bool SetVisualHiddenExternal(VisualSessionCache& cache,const VisualSessionIdentity& current,bool hidden,std::string& error);
}

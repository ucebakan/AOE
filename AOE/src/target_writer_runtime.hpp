#pragma once
#include "target_inspector.hpp"
#include <filesystem>

namespace aoe {
constexpr size_t TargetWriterEventLimit=100;
constexpr size_t TargetWriterBreakpointCount=1;
enum class TargetWriterPhase { Selection, NormalAttack, AoeCast, Other };
enum class TargetWriterState { Stopped, Running, Complete, Error };
enum class TargetWriterConclusionState { WriterNotObserved,WriterObservedContextUnknown,WriterObservedNonMonsterValue,WriterObservedMonsterValue,GlobalObjectMatched,GlobalObjectMismatched,SelectionWriterCandidate,CombatTargetWriterCandidate,InsufficientData };

struct TargetWriterRegisters { uint64_t rip=0,rbx=0,r8=0,rdx=0,rcx=0,r9=0,rsp=0; };
struct TargetWriterEvent {
    uint64_t sequence=0,timestampMs=0;
    uint32_t threadId=0;
    TargetWriterPhase phase=TargetWriterPhase::Selection;
    TargetWriterRegisters registers;
    uint64_t effectiveDestination=0,valueBefore=0,expectedStoredValue=0,observedPostStoreValue=0;
    bool destinationReadable=false,postStoreObserved=false;
    uint32_t destinationReadError=0;
    uint64_t globalStorageAddress=0,globalRawValue=0;
    bool globalReadable=false,globalPointerValid=false,rbxMatchesGlobalValue=false;
    uint32_t globalReadError=0;
    bool r8Null=false,r8ActorValid=false,resolverMatched=false,eligibilityReadable=false,statusReadable=false;
    uint8_t eligibility=0;
    uint32_t status=0;
    ActorSpatialSnapshot actor;
};
struct TargetWriterSummary {
    size_t hits=0,monsterValueHits=0,rbxGlobalMatches=0,rbxGlobalMismatches=0;
    std::vector<TargetWriterConclusionState> classifications;
    std::string reason;
};
struct TargetWriterRuntimeSession {
    TargetWriterState state=TargetWriterState::Stopped;
    TargetWriterPhase currentPhase=TargetWriterPhase::Selection;
    uint32_t pid=0;
    uint64_t creationTime=0,moduleBase=0,startQpc=0,frequency=0;
    std::string targetSha256,error;
    RuntimeLayout runtime;
    bool bufferFull=false;
    std::vector<TargetWriterEvent> events;
};

const char* TargetWriterPhaseName(TargetWriterPhase value);
const char* TargetWriterStateName(TargetWriterState value);
const char* TargetWriterConclusionName(TargetWriterConclusionState value);
TargetWriterEvent CaptureTargetWriterEvent(const InspectorRead& read,const RuntimeLayout& layout,uint64_t moduleBase,TargetWriterPhase phase,uint64_t sequence,uint64_t timestampMs,uint32_t threadId,const TargetWriterRegisters& registers);
bool RecordTargetWriterEvent(TargetWriterRuntimeSession& session,TargetWriterEvent event);
TargetWriterSummary AnalyzeTargetWriterRuntime(const TargetWriterRuntimeSession& session);
void ClearTargetWriterEvents(TargetWriterRuntimeSession& session);
bool ExportTargetWriterRuntimeJson(const std::filesystem::path& path,const TargetWriterRuntimeSession& session,std::string& error);
}

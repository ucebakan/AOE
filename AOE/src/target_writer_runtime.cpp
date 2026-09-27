#include "target_writer_runtime.hpp"
#include <algorithm>
#include <fstream>
#include <iomanip>
#include <locale>
#include <set>
#include <sstream>

namespace aoe {
namespace {
bool Add(uint64_t base,uint64_t offset,uint64_t& out){if(!base||base>UINT64_MAX-offset)return false;out=base+offset;return true;}
template<class T> bool Read(const InspectorRead& read,uint64_t address,T& value,uint32_t& error){return address&&read(address,&value,sizeof(value),error);}
std::string Json(const std::string& value){std::ostringstream out;out<<'"';for(unsigned char c:value){if(c=='"'||c=='\\')out<<'\\'<<char(c);else if(c=='\n')out<<"\\n";else if(c=='\r')out<<"\\r";else if(c<32)out<<"\\u"<<std::hex<<std::setw(4)<<std::setfill('0')<<unsigned(c)<<std::dec;else out<<char(c);}out<<'"';return out.str();}
const char* Bool(bool value){return value?"true":"false";}
}

const char* TargetWriterPhaseName(TargetWriterPhase value){switch(value){case TargetWriterPhase::NormalAttack:return "NORMAL_ATTACK";case TargetWriterPhase::AoeCast:return "AOE_CAST";case TargetWriterPhase::Other:return "OTHER";default:return "SELECTION";}}
const char* TargetWriterStateName(TargetWriterState value){switch(value){case TargetWriterState::Running:return "RUNNING";case TargetWriterState::Complete:return "COMPLETE";case TargetWriterState::Error:return "ERROR";default:return "STOPPED";}}
const char* TargetWriterConclusionName(TargetWriterConclusionState value){switch(value){case TargetWriterConclusionState::WriterNotObserved:return "WRITER_NOT_OBSERVED";case TargetWriterConclusionState::WriterObservedContextUnknown:return "WRITER_OBSERVED_CONTEXT_UNKNOWN";case TargetWriterConclusionState::WriterObservedNonMonsterValue:return "WRITER_OBSERVED_NON_MONSTER_VALUE";case TargetWriterConclusionState::WriterObservedMonsterValue:return "WRITER_OBSERVED_MONSTER_VALUE";case TargetWriterConclusionState::GlobalObjectMatched:return "GLOBAL_OBJECT_MATCHED";case TargetWriterConclusionState::GlobalObjectMismatched:return "GLOBAL_OBJECT_MISMATCHED";case TargetWriterConclusionState::SelectionWriterCandidate:return "SELECTION_WRITER_CANDIDATE";case TargetWriterConclusionState::CombatTargetWriterCandidate:return "COMBAT_TARGET_WRITER_CANDIDATE";default:return "INSUFFICIENT_DATA";}}

TargetWriterEvent CaptureTargetWriterEvent(const InspectorRead& read,const RuntimeLayout& layout,uint64_t moduleBase,TargetWriterPhase phase,uint64_t sequence,uint64_t timestampMs,uint32_t threadId,const TargetWriterRegisters& registers){
    TargetWriterEvent event;event.sequence=sequence;event.timestampMs=timestampMs;event.threadId=threadId;event.phase=phase;event.registers=registers;event.expectedStoredValue=registers.r8;event.r8Null=registers.r8==0;
    if(Add(registers.rbx,layout.selectedTargetOffset,event.effectiveDestination))event.destinationReadable=Read(read,event.effectiveDestination,event.valueBefore,event.destinationReadError);
    if(Add(moduleBase,layout.gameContextPointerRva,event.globalStorageAddress))event.globalReadable=Read(read,event.globalStorageAddress,event.globalRawValue,event.globalReadError);
    uint8_t globalProbe=0;uint32_t globalProbeError=0;event.globalPointerValid=event.globalReadable&&IsCanonicalUserPointer(event.globalRawValue)&&Read(read,event.globalRawValue,globalProbe,globalProbeError);event.rbxMatchesGlobalValue=event.globalPointerValid&&registers.rbx==event.globalRawValue;
    if(!event.r8Null){
        event.actor=CaptureActorSpatial(read,layout,registers.r8,"writer R8 actor candidate","runtime register value at pre-store breakpoint");event.r8ActorValid=event.actor.identityReadable;
        if(event.r8ActorValid&&event.globalPointerValid){const auto resolved=ResolveTargetActor(read,layout,event.globalRawValue,event.actor.actorId,event.actor.category);event.resolverMatched=resolved.status==TargetActorLookupStatus::Resolved&&resolved.actorPointer==registers.r8;}
        uint32_t fieldError=0;uint64_t at=0;if(Add(registers.r8,layout.actorEligibilityOffset,at))event.eligibilityReadable=Read(read,at,event.eligibility,fieldError);if(Add(registers.r8,layout.actorStatusOffset,at))event.statusReadable=Read(read,at,event.status,fieldError);
    }
    return event;
}

bool RecordTargetWriterEvent(TargetWriterRuntimeSession& session,TargetWriterEvent event){if(session.state!=TargetWriterState::Running||session.bufferFull)return false;if(session.events.size()>=TargetWriterEventLimit){session.bufferFull=true;return false;}session.events.push_back(std::move(event));if(session.events.size()>=TargetWriterEventLimit)session.bufferFull=true;return true;}
void ClearTargetWriterEvents(TargetWriterRuntimeSession& session){session.events.clear();session.bufferFull=false;}

TargetWriterSummary AnalyzeTargetWriterRuntime(const TargetWriterRuntimeSession& session){
    TargetWriterSummary out;out.hits=session.events.size();if(!out.hits){out.classifications={TargetWriterConclusionState::WriterNotObserved,TargetWriterConclusionState::InsufficientData};out.reason="No +7F6411 writer hit has been observed.";return out;}
    size_t readableGlobals=0,selectionHits=0,attackHits=0;std::set<uint64_t> selectionMonsters;
    for(const auto& e:session.events){if(e.actor.identityReadable&&e.actor.category==2){++out.monsterValueHits;if(e.phase==TargetWriterPhase::Selection)selectionMonsters.insert(e.registers.r8);}if(e.globalPointerValid){++readableGlobals;if(e.rbxMatchesGlobalValue)++out.rbxGlobalMatches;else ++out.rbxGlobalMismatches;}if(e.phase==TargetWriterPhase::Selection)++selectionHits;if(e.phase==TargetWriterPhase::NormalAttack)++attackHits;}
    out.classifications.push_back(out.monsterValueHits?TargetWriterConclusionState::WriterObservedMonsterValue:TargetWriterConclusionState::WriterObservedNonMonsterValue);
    if(readableGlobals&&out.rbxGlobalMatches==readableGlobals)out.classifications.push_back(TargetWriterConclusionState::GlobalObjectMatched);else if(readableGlobals&&out.rbxGlobalMismatches==readableGlobals)out.classifications.push_back(TargetWriterConclusionState::GlobalObjectMismatched);else out.classifications.push_back(TargetWriterConclusionState::WriterObservedContextUnknown);
    if(selectionMonsters.size()>=2){out.classifications.push_back(TargetWriterConclusionState::SelectionWriterCandidate);out.reason="Multiple SELECTION hits carried distinct category-2 actor pointers; selection-writer semantics are a candidate, not proven.";}else if(attackHits&&!selectionHits){out.classifications.push_back(TargetWriterConclusionState::CombatTargetWriterCandidate);out.reason="Hits were observed during NORMAL_ATTACK but not SELECTION; combat-target semantics are a candidate.";}else{out.classifications.push_back(TargetWriterConclusionState::InsufficientData);out.reason="More phase-labelled events are required to distinguish selection, combat, and other target state.";}return out;
}

bool ExportTargetWriterRuntimeJson(const std::filesystem::path& path,const TargetWriterRuntimeSession& session,std::string& error){
    try{
        if(!path.parent_path().empty())std::filesystem::create_directories(path.parent_path());std::ofstream out(path,std::ios::binary|std::ios::trunc);if(!out){error="Cannot create Target Writer Runtime JSON.";return false;}out.imbue(std::locale::classic());const auto summary=AnalyzeTargetWriterRuntime(session);
        out<<"{\n  \"targetWriterRuntime\": {\n    \"readOnly\":true,\n    \"writerRva\":\"0x7F6411\",\n    \"instruction\":\"mov qword ptr [rbx+2318h], r8\",\n    \"breakpointsPerThread\":1,\n    \"postStoreMethod\":\"expected only; execution breakpoint observes pre-store state\",\n    \"bufferFull\":"<<Bool(session.bufferFull)<<",\n    \"events\":[";
        for(size_t i=0;i<session.events.size();++i){
            if(i)out<<',';const auto& e=session.events[i];
            out<<"\n      {\"sequence\":"<<e.sequence<<",\"phase\":"<<Json(TargetWriterPhaseName(e.phase))<<",\"timestampMs\":"<<e.timestampMs<<",\"threadId\":"<<e.threadId
               <<",\"registers\":{\"rip\":"<<Json(Hex(e.registers.rip))<<",\"rbx\":"<<Json(Hex(e.registers.rbx))<<",\"r8\":"<<Json(Hex(e.registers.r8))<<",\"rdx\":"<<Json(Hex(e.registers.rdx))<<",\"rcx\":"<<Json(Hex(e.registers.rcx))<<",\"r9\":"<<Json(Hex(e.registers.r9))<<",\"rsp\":"<<Json(Hex(e.registers.rsp))<<"}"
               <<",\"destination\":{\"address\":"<<Json(Hex(e.effectiveDestination))<<",\"offset\":\"0x2318\",\"valueBefore\":"<<Json(Hex(e.valueBefore))<<",\"readable\":"<<Bool(e.destinationReadable)<<",\"expectedStoredValue\":"<<Json(Hex(e.expectedStoredValue))<<",\"observedPostStoreValue\":"<<Json(Hex(e.observedPostStoreValue))<<",\"postStoreObserved\":"<<Bool(e.postStoreObserved)<<"}"
               <<",\"globalCandidate\":{\"storageAddress\":"<<Json(Hex(e.globalStorageAddress))<<",\"value\":"<<Json(Hex(e.globalRawValue))<<",\"readable\":"<<Bool(e.globalReadable)<<",\"pointerValid\":"<<Bool(e.globalPointerValid)<<",\"rbxMatchesGlobal\":"<<Bool(e.rbxMatchesGlobalValue)<<"}"
               <<",\"r8Actor\":{\"valid\":"<<Bool(e.r8ActorValid)<<",\"pointer\":"<<Json(Hex(e.registers.r8))<<",\"actorId\":"<<e.actor.actorId<<",\"category\":"<<unsigned(e.actor.category)<<",\"categoryName\":"<<Json(TargetActorTypeName(e.actor.category))<<",\"eligibility\":"<<unsigned(e.eligibility)<<",\"status\":"<<e.status<<",\"typedResolverMatched\":"<<Bool(e.resolverMatched)<<",\"positionAvailable\":"<<Bool(e.actor.positionAvailable)<<",\"xyz\":{\"x\":";
            if(e.actor.positionAvailable)out<<e.actor.position.value[0];else out<<"null";out<<",\"y\":";if(e.actor.positionAvailable)out<<e.actor.position.value[1];else out<<"null";out<<",\"z\":";if(e.actor.positionAvailable)out<<e.actor.position.value[2];else out<<"null";out<<"}}}";
        }
        out<<"\n    ],\n    \"summary\":{\"hits\":"<<summary.hits<<",\"monsterValueHits\":"<<summary.monsterValueHits<<",\"rbxGlobalMatches\":"<<summary.rbxGlobalMatches<<",\"rbxGlobalMismatches\":"<<summary.rbxGlobalMismatches<<",\"classifications\":[";for(size_t i=0;i<summary.classifications.size();++i){if(i)out<<',';out<<Json(TargetWriterConclusionName(summary.classifications[i]));}out<<"],\"reason\":"<<Json(summary.reason)<<"}\n  }\n}\n";
        if(!out){error="Failed while writing Target Writer Runtime JSON.";return false;}error.clear();return true;
    }catch(const std::exception& ex){error=ex.what();return false;}
}
}

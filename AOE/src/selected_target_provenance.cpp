#include "selected_target_provenance.hpp"
#include <algorithm>
#include <cmath>
#include <fstream>
#include <iomanip>
#include <locale>
#include <set>
#include <sstream>

namespace aoe {
namespace {
template<class T> bool Read(const InspectorRead& read,uint64_t address,T& value,uint32_t& error){return address&&read(address,&value,sizeof(value),error);}
bool Add(uint64_t base,uint64_t offset,uint64_t& out){if(!base||base>UINT64_MAX-offset)return false;out=base+offset;return true;}
std::string Quote(const std::string& value){std::ostringstream out;out<<'"';for(unsigned char c:value){if(c=='"'||c=='\\')out<<'\\'<<char(c);else if(c=='\n')out<<"\\n";else if(c=='\r')out<<"\\r";else if(c<32)out<<"\\u"<<std::hex<<std::setw(4)<<std::setfill('0')<<unsigned(c)<<std::dec;else out<<char(c);}out<<'"';return out.str();}
const char* Bool(bool value){return value?"true":"false";}
const SelectedTargetCapture* Find(const SelectedTargetProvenanceSession& session,const char* label){for(auto it=session.captures.rbegin();it!=session.captures.rend();++it)if(it->label==label)return &*it;return nullptr;}
}

SelectedTargetCapture CaptureSelectedTarget(const InspectorRead& read,const RuntimeLayout& layout,uint64_t moduleBase,const std::string& label,uint64_t timestampMs){
    SelectedTargetCapture c;c.label=label;c.utc=UtcNow();c.timestampMs=timestampMs;
    uint64_t global=0;if(!Add(moduleBase,layout.gameContextPointerRva,global)||!Read(read,global,c.gameContext,c.error)||!IsCanonicalUserPointer(c.gameContext)){c.validation="game context pointer is unavailable or invalid";return c;}
    if(!Add(c.gameContext,layout.selectedTargetOffset,c.storageAddress)||!Read(read,c.storageAddress,c.rawValue,c.error)){c.validation="selected-target storage is unreadable";return c;}c.storageReadable=true;
    if(!c.rawValue){c.nullSelection=true;c.validation="selected-target storage is null";return c;}
    if(!IsCanonicalUserPointer(c.rawValue)){c.validation="stored value is not a canonical user pointer";return c;}c.actorPointer=c.rawValue;
    uint64_t at=0;if(!Add(c.actorPointer,layout.actorIdOffset,at)||!Read(read,at,c.actorId,c.error)||!Add(c.actorPointer,layout.actorTypeOffset,at)||!Read(read,at,c.actorType,c.error)){c.validation="stored pointer does not expose readable actor identity";return c;}
    c.identityReadable=true;c.category=c.actorType;c.categoryMonster=c.category==2;
    const auto resolved=ResolveTargetActor(read,layout,c.gameContext,c.actorId,c.actorType);c.resolverMatched=resolved.status==TargetActorLookupStatus::Resolved&&resolved.actorPointer==c.actorPointer;
    if(c.categoryMonster){auto actor=CaptureActorSpatial(read,layout,c.actorPointer,"unproven target-state candidate game+0x2318","candidate pointer with category-2 identity");c.position=actor.position;c.positionAvailable=actor.positionAvailable;}
    c.validation=!c.categoryMonster?"rejected: selected object category is not CTClientMonster":!c.resolverMatched?"candidate: actor identity is readable but typed tree resolution did not return the same pointer":"validated: direct pointer, category 2, and typed resolver-equivalent tree lookup agree";
    return c;
}

bool RecordSelectedTargetCapture(SelectedTargetProvenanceSession& session,SelectedTargetCapture capture,std::string& error){
    if(session.phase!=SelectedTargetPhase::Running&&session.phase!=SelectedTargetPhase::Captured){error="Selected Target Provenance Inspector is not running.";return false;}
    if(capture.label!="A"&&capture.label!="B"&&capture.label!="C"&&capture.label!="NONE"){error="Unknown selected-target capture label.";return false;}
    session.captures.erase(std::remove_if(session.captures.begin(),session.captures.end(),[&](const auto& item){return item.label==capture.label;}),session.captures.end());session.captures.push_back(std::move(capture));
    session.phase=SelectedTargetPhase::Captured;error.clear();return true;
}

SelectedTargetConclusion AnalyzeSelectedTargetProvenance(const SelectedTargetProvenanceSession& session){
    SelectedTargetConclusion out;const auto* a=Find(session,"A"),*b=Find(session,"B"),*c=Find(session,"C");if(!a||!b||!c)return out;
    const SelectedTargetCapture* captures[]={a,b,c};for(auto* item:captures)if(!item->storageReadable||!item->identityReadable||!item->categoryMonster||!item->resolverMatched){out.classification=SelectedTargetClassification::CandidateOnly;out.confidence="low";out.reason="A/B/C do not all validate as category-2 actors through the typed actor tree.";return out;}
    std::set<uint64_t> pointers{a->actorPointer,b->actorPointer,c->actorPointer};std::set<uint32_t> ids{a->actorId,b->actorId,c->actorId};if(pointers.size()!=3||ids.size()!=3){out.classification=SelectedTargetClassification::CandidateOnly;out.confidence="medium";out.reason="Repeated actor identity cannot prove target switching across A/B/C.";return out;}
    if(const auto* none=Find(session,"NONE");none&&none->storageReadable&&!none->nullSelection){out.classification=SelectedTargetClassification::CandidateOnly;out.confidence="medium";out.reason="A/B/C change correctly, but CAPTURE NONE did not clear selected-target storage.";return out;}
    out.classification=SelectedTargetClassification::ProvenDirectPointer;out.confidence="high";out.reason="A/B/C are distinct direct pointers; every actor is category 2 and resolves back to the same pointer through the profiled typed actor tree.";return out;
}

const char* SelectedTargetPhaseName(SelectedTargetPhase value){switch(value){case SelectedTargetPhase::Running:return "RUNNING";case SelectedTargetPhase::Captured:return "CAPTURED";case SelectedTargetPhase::Error:return "ERROR";default:return "STOPPED";}}
const char* SelectedTargetClassificationName(SelectedTargetClassification value){switch(value){case SelectedTargetClassification::ProvenDirectPointer:return "PROVEN_DIRECT_POINTER";case SelectedTargetClassification::ProvenIdTypePair:return "PROVEN_ID_TYPE_PAIR";case SelectedTargetClassification::ProvenWrapperReference:return "PROVEN_WRAPPER_REFERENCE";case SelectedTargetClassification::CandidateOnly:return "CANDIDATE_ONLY";default:return "INSUFFICIENT_DATA";}}
std::string FormatHardwareBreakpointDiagnostic(uint32_t threadId,const std::array<uint64_t,4>& dr,uint64_t dr7,unsigned required,unsigned available){std::ostringstream out;out<<"thread ID="<<threadId<<" DR0="<<Hex(dr[0])<<" DR1="<<Hex(dr[1])<<" DR2="<<Hex(dr[2])<<" DR3="<<Hex(dr[3])<<" DR7="<<Hex(dr7)<<" required slots="<<required<<" available slots="<<available;return out.str();}

bool ExportSelectedTargetProvenanceJson(const std::filesystem::path& path,const SelectedTargetProvenanceSession& session,std::string& error){try{if(session.captures.empty()){error="No selected-target provenance captures are available.";return false;}if(!path.parent_path().empty())std::filesystem::create_directories(path.parent_path());std::ofstream out(path,std::ios::binary|std::ios::trunc);if(!out){error="Cannot create selected-target provenance JSON.";return false;}out.imbue(std::locale::classic());const auto conclusion=AnalyzeSelectedTargetProvenance(session);out<<"{\n  \"selectedTargetProvenance\": {\n    \"storage\": {\"kind\":\"unproven target-state actor-pointer candidate\",\"owner\":\"profiled global context candidate\",\"gameContextPointerRva\":"<<Quote(Hex(session.runtime.gameContextPointerRva))<<",\"offset\":"<<Quote(Hex(session.runtime.selectedTargetOffset))<<",\"writerRva\":"<<Quote(Hex(session.runtime.selectedTargetWriterRva))<<",\"readerRvas\":[";for(size_t i=0;i<session.runtime.selectedTargetReaderRvas.size();++i){if(i)out<<',';out<<Quote(Hex(session.runtime.selectedTargetReaderRvas[i]));}out<<"]},\n    \"captures\": [";for(size_t i=0;i<session.captures.size();++i){if(i)out<<',';const auto& c=session.captures[i];out<<"\n      {\"label\":"<<Quote(c.label)<<",\"timestampMs\":"<<c.timestampMs<<",\"storageAddress\":"<<Quote(Hex(c.storageAddress))<<",\"rawValue\":"<<Quote(Hex(c.rawValue))<<",\"actorPointer\":"<<Quote(Hex(c.actorPointer))<<",\"actorId\":"<<c.actorId<<",\"actorType\":"<<unsigned(c.actorType)<<",\"category\":"<<unsigned(c.category)<<",\"categoryName\":"<<Quote(TargetActorTypeName(c.category))<<",\"xyz\":{";if(c.positionAvailable)out<<"\"x\":"<<c.position.value[0]<<",\"y\":"<<c.position.value[1]<<",\"z\":"<<c.position.value[2];else out<<"\"x\":null,\"y\":null,\"z\":null";out<<"},\"validation\":{\"storageReadable\":"<<Bool(c.storageReadable)<<",\"identityReadable\":"<<Bool(c.identityReadable)<<",\"category2\":"<<Bool(c.categoryMonster)<<",\"typedResolverMatched\":"<<Bool(c.resolverMatched)<<",\"nullSelection\":"<<Bool(c.nullSelection)<<",\"reason\":"<<Quote(c.validation)<<"}}";}out<<"\n    ],\n    \"conclusion\": {\"classification\":"<<Quote(SelectedTargetClassificationName(conclusion.classification))<<",\"confidence\":"<<Quote(conclusion.confidence)<<",\"reason\":"<<Quote(conclusion.reason)<<"}\n  }\n}\n";if(!out){error="Failed while writing selected-target provenance JSON.";return false;}error.clear();return true;}catch(const std::exception& ex){error=ex.what();return false;}}
}

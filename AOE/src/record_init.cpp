#include "record_init.hpp"
#include "analysis.hpp"
#include <cstring>
#include <iomanip>
#include <set>
#include <sstream>

namespace aoe {
namespace {
template<class T> RecordRead Read(HANDLE process,uint64_t address){
    RecordRead result;result.width=sizeof(T);T value{};result.readable=SafeRead(process,address,&value,sizeof(value),result.error);
    if(result.readable){result.value=static_cast<uint64_t>(value);result.raw.resize(sizeof(value));memcpy(result.raw.data(),&value,sizeof(value));}
    return result;
}
std::string Quote(const std::string& text){std::ostringstream out;out<<'"';for(unsigned char c:text){switch(c){case '"':out<<"\\\"";break;case '\\':out<<"\\\\";break;case '\b':out<<"\\b";break;case '\f':out<<"\\f";break;case '\n':out<<"\\n";break;case '\r':out<<"\\r";break;case '\t':out<<"\\t";break;default:if(c<0x20)out<<"\\u"<<std::hex<<std::setw(4)<<std::setfill('0')<<unsigned(c)<<std::dec;else out<<char(c);}}out<<'"';return out.str();}
const char* Bool(bool value){return value?"true":"false";}
void WriteRead(std::ostream& out,const RecordRead& value){out<<"{\"readable\":"<<Bool(value.readable)<<",\"error\":"<<value.error<<",\"width\":"<<value.width<<",\"rawBytes\":"<<Quote(BytesHex(value.raw,value.raw.size()))<<",\"hex\":"<<(value.readable?Quote(Hex(value.value)):"null")<<",\"value\":"<<(value.readable?std::to_string(value.value):"null")<<'}';}
std::string Value(const RecordRead& value){return value.readable?std::to_string(value.value)+" / "+Hex(value.value):"unreadable (Win32 "+std::to_string(value.error)+")";}
}

void ClassifyRecordInit(RecordInitSnapshot& snapshot){
    snapshot.linked020A=snapshot.linkedWord0.readable&&snapshot.linkedWord0.value==0x020A;
    snapshot.primary0209=snapshot.primaryWord0.readable&&snapshot.primaryWord0.value==0x0209;
    snapshot.primary0209Linked020A=snapshot.primary0209&&snapshot.linked020A;
}

void SnapshotRecordInit(HANDLE process,Event& event){
    auto& snapshot=event.recordInit;snapshot={};snapshot.captured=true;snapshot.recordPointer=event.rbx;snapshot.ecxNew34=uint32_t(event.rcx);
    snapshot.primaryPointer=Read<uint64_t>(process,event.rbx+0x20);
    snapshot.old34=Read<uint32_t>(process,event.rbx+0x34);
    snapshot.record64=Read<uint32_t>(process,event.rbx+0x64);
    snapshot.linkedPointer=Read<uint64_t>(process,event.rbx+0x80);
    snapshot.record90=Read<uint32_t>(process,event.rbx+0x90);
    snapshot.record98=Read<uint8_t>(process,event.rbx+0x98);
    snapshot.rbpMinus7C=event.rbp>=0x7c?Read<uint16_t>(process,event.rbp-0x7c):RecordRead{false,ERROR_INVALID_ADDRESS,2};
    if(snapshot.primaryPointer.readable){snapshot.primaryWord0=Read<uint16_t>(process,snapshot.primaryPointer.value);snapshot.primaryField24=Read<uint32_t>(process,snapshot.primaryPointer.value+0x24);}
    else{snapshot.primaryWord0.error=snapshot.primaryField24.error=ERROR_INVALID_ADDRESS;snapshot.primaryWord0.width=2;snapshot.primaryField24.width=4;}
    if(snapshot.linkedPointer.readable){snapshot.linkedWord0=Read<uint16_t>(process,snapshot.linkedPointer.value);snapshot.linkedField6C=Read<uint32_t>(process,snapshot.linkedPointer.value+0x6c);}
    else{snapshot.linkedWord0.error=snapshot.linkedField6C.error=ERROR_INVALID_ADDRESS;snapshot.linkedWord0.width=2;snapshot.linkedField6C.width=4;}
    ClassifyRecordInit(snapshot);
}

void WriteRecordInitJson(std::ostream& out,const Capture& capture,const Event& event){
    const auto& s=event.recordInit;if(!s.captured){out<<"null";return;}const auto marker=RelativeToMarkers(capture,event.ms);
    out<<"{\"instruction\":\"mov dword ptr [rbx+34h], ecx\",\"timestamp\":{\"qpc\":"<<Quote(std::to_string(event.qpc))<<",\"ms\":"<<std::fixed<<std::setprecision(6)<<event.ms<<"},\"markerCorrelation\":{\"nearestMarkerId\":"<<(marker.available?std::to_string(marker.nearestId):"null")<<",\"msFromNearestMarker\":"<<(marker.available?std::to_string(marker.nearestRelativeMs):"null")<<"},\"registers\":{\"rip\":"<<Quote(Hex(event.rip))<<",\"rbx\":"<<Quote(Hex(event.rbx))<<",\"ecx\":"<<s.ecxNew34<<",\"ecxHex\":"<<Quote(Hex(s.ecxNew34))<<",\"rbp\":"<<Quote(Hex(event.rbp))<<"},\"recordPointer\":"<<Quote(Hex(s.recordPointer))<<",\"fields\":{\"old34\":";WriteRead(out,s.old34);out<<",\"record64\":";WriteRead(out,s.record64);out<<",\"record90\":";WriteRead(out,s.record90);out<<",\"record98\":";WriteRead(out,s.record98);out<<",\"rbpMinus7C\":";WriteRead(out,s.rbpMinus7C);out<<",\"primaryPointer\":";WriteRead(out,s.primaryPointer);out<<",\"primaryWord0\":";WriteRead(out,s.primaryWord0);out<<",\"primaryField24\":";WriteRead(out,s.primaryField24);out<<",\"linkedPointer\":";WriteRead(out,s.linkedPointer);out<<",\"linkedWord0\":";WriteRead(out,s.linkedWord0);out<<",\"linkedField6C\":";WriteRead(out,s.linkedField6C);out<<"},\"classification\":{\"linked020A\":"<<Bool(s.linked020A)<<",\"primary0209\":"<<Bool(s.primary0209)<<",\"primary0209Linked020A\":"<<Bool(s.primary0209Linked020A)<<"}}";
}

void WriteRecordInitSummaryJson(std::ostream& out,const Capture& capture){
    std::vector<const Event*> hits;std::set<uint32_t> distinct;for(const auto& event:capture.events)if(event.recordInit.linked020A){hits.push_back(&event);distinct.insert(event.recordInit.ecxNew34);}
    out<<"{\"count\":"<<hits.size()<<",\"ecxValues\":[";for(size_t i=0;i<hits.size();++i){if(i)out<<',';out<<"{\"decimal\":"<<hits[i]->recordInit.ecxNew34<<",\"hex\":"<<Quote(Hex(hits[i]->recordInit.ecxNew34))<<'}';}out<<"],\"distinctEcxValues\":[";bool first=true;for(auto value:distinct){if(!first)out<<',';first=false;out<<"{\"decimal\":"<<value<<",\"hex\":"<<Quote(Hex(value))<<'}';}out<<"],\"old34Values\":[";for(size_t i=0;i<hits.size();++i){if(i)out<<',';if(hits[i]->recordInit.old34.readable)out<<hits[i]->recordInit.old34.value;else out<<"null";}out<<"],\"recordPointers\":[";for(size_t i=0;i<hits.size();++i){if(i)out<<',';out<<Quote(Hex(hits[i]->recordInit.recordPointer));}out<<"],\"markerRelativeTimestampsMs\":[";for(size_t i=0;i<hits.size();++i){if(i)out<<',';auto marker=RelativeToMarkers(capture,hits[i]->ms);if(marker.available)out<<std::fixed<<std::setprecision(6)<<marker.nearestRelativeMs;else out<<"null";}out<<"],\"limitation\":"<<Quote(RecordInitLimitation)<<'}';
}

std::string RecordInitDetails(const Capture& capture,const Event& event){const auto& s=event.recordInit;const auto marker=RelativeToMarkers(capture,event.ms);std::ostringstream out;out<<"RIP "<<Hex(event.rip)<<"  RBX "<<Hex(event.rbx)<<"  ECX "<<s.ecxNew34<<" / "<<Hex(s.ecxNew34)<<"  RBP "<<Hex(event.rbp)<<"\r\n"<<"QPC "<<event.qpc<<"  time "<<event.ms<<" ms  nearest marker delta "<<(marker.available?std::to_string(marker.nearestRelativeMs):"unavailable")<<" ms  thread "<<event.threadId<<"\r\n"<<"old +34 "<<Value(s.old34)<<"  +64 "<<Value(s.record64)<<"  +90 "<<Value(s.record90)<<"  +98 "<<Value(s.record98)<<"  [RBP-7C] "<<Value(s.rbpMinus7C)<<"\r\n"<<"primary ptr "<<Value(s.primaryPointer)<<"  primaryWord0 "<<Value(s.primaryWord0)<<"  primaryField24 "<<Value(s.primaryField24)<<"\r\n"<<"linked ptr "<<Value(s.linkedPointer)<<"  linkedWord0 "<<Value(s.linkedWord0)<<"  linkedField6C "<<Value(s.linkedField6C)<<"\r\n"<<"classification: linked020A="<<Bool(s.linked020A)<<" primary0209="<<Bool(s.primary0209)<<" primary0209Linked020A="<<Bool(s.primary0209Linked020A)<<"\r\n"<<RecordInitLimitation;return out.str();}

std::string RecordInitSummary(const Capture& capture){size_t count=0;std::set<uint32_t> distinct;for(const auto& event:capture.events)if(event.recordInit.linked020A){++count;distinct.insert(event.recordInit.ecxNew34);}std::ostringstream out;out<<capture.events.size()<<" Record Init hits; linked 0x020A initialization hits: "<<count<<"; distinct ECX values: ";if(distinct.empty())out<<"none";else{bool first=true;for(auto value:distinct){if(!first)out<<", ";first=false;out<<value<<" / "<<Hex(value);}}out<<". "<<RecordInitLimitation;return out.str();}
}

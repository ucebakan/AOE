#include "analysis.hpp"
#include "record_init.hpp"
#include <array>
#include <cstring>
#include <fstream>
#include <functional>
#include <iostream>
#include <iterator>
#include <stdexcept>

namespace {
void Require(bool condition,const std::string& message){if(!condition)throw std::runtime_error(message);}
template<class T> void Put(void* base,size_t offset,T value){memcpy(static_cast<unsigned char*>(base)+offset,&value,sizeof(value));}
}

unsigned RecordInitTests(const std::filesystem::path& artifacts){
    unsigned passed=0;auto check=[&](const char* name,const std::function<void()>& test){test();++passed;std::cout<<"[PASS] "<<name<<'\n';};
    check("record init mode resolves exact 0x57E35 RVA",[]{Require(aoe::RecordInitRva==0x57E35,"constant");Require(aoe::TraceRva(aoe::TraceMode::RecordInit)==0x57E35,"mode mapping");Require(std::string(aoe::ModeName(aoe::TraceMode::RecordInit))=="Record Init 57E35","mode name");Require(aoe::DefaultCaptureSecondsForMode(aoe::TraceMode::RecordInit)==10,"default duration");});
    check("record init preserves ECX and decodes primary linked and record fields",[]{
        std::array<unsigned char,0xA0> record{};std::array<unsigned char,0x80> primary{},linked{};std::array<unsigned char,0x100> frame{};
        const uint64_t primaryPointer=reinterpret_cast<uint64_t>(primary.data()),linkedPointer=reinterpret_cast<uint64_t>(linked.data());
        Put(record.data(),0x20,primaryPointer);Put(record.data(),0x34,uint32_t(77));Put(record.data(),0x64,uint32_t(245));Put(record.data(),0x80,linkedPointer);Put(record.data(),0x90,uint32_t(999));Put(record.data(),0x98,uint8_t(1));
        Put(primary.data(),0,uint16_t(0x0209));Put(primary.data(),0x24,uint32_t(0x11223344));Put(linked.data(),0,uint16_t(0x020A));Put(linked.data(),0x6c,uint32_t(0x55667788));Put(frame.data(),0,uint16_t(0xBEEF));
        aoe::Event event;event.rbx=reinterpret_cast<uint64_t>(record.data());event.rbp=reinterpret_cast<uint64_t>(frame.data()+0x7c);event.rcx=0x12345678DEADBEEFull;aoe::SnapshotRecordInit(GetCurrentProcess(),event);const auto& s=event.recordInit;
        Require(s.ecxNew34==0xDEADBEEF,"ECX low 32 bits exact");Require(s.old34.readable&&s.old34.value==77&&s.record64.value==245&&s.record90.value==999&&s.record98.value==1,"record fields");Require(s.rbpMinus7C.readable&&s.rbpMinus7C.value==0xBEEF&&s.rbpMinus7C.raw.size()==2,"RBP raw word");Require(s.primaryWord0.value==0x0209&&s.primaryField24.value==0x11223344,"primary decoding");Require(s.linkedWord0.value==0x020A&&s.linkedField6C.value==0x55667788,"linked decoding");Require(s.linked020A&&s.primary0209&&s.primary0209Linked020A,"combined classification");
    });
    check("record init pointer read failures remain diagnostic",[]{aoe::Event event;event.rbx=1;event.rbp=1;event.rcx=42;aoe::SnapshotRecordInit(GetCurrentProcess(),event);const auto& s=event.recordInit;Require(s.captured&&s.ecxNew34==42,"register survives invalid pointers");Require(!s.old34.readable&&s.old34.error!=0&&!s.primaryPointer.readable&&!s.linkedPointer.readable,"record reads fail safely");Require(!s.primaryWord0.readable&&!s.linkedWord0.readable&&!s.rbpMinus7C.readable,"dependent reads fail safely");Require(!s.linked020A&&!s.primary0209&&!s.primary0209Linked020A,"invalid reads never classify");});
    check("record init classifications are independent",[]{aoe::RecordInitSnapshot s;s.primaryWord0={true,0,2,0x0209};aoe::ClassifyRecordInit(s);Require(s.primary0209&&!s.linked020A&&!s.primary0209Linked020A,"primary only");s.linkedWord0={true,0,2,0x020A};aoe::ClassifyRecordInit(s);Require(s.primary0209&&s.linked020A&&s.primary0209Linked020A,"combined");s.primaryWord0.readable=false;aoe::ClassifyRecordInit(s);Require(!s.primary0209&&s.linked020A&&!s.primary0209Linked020A,"linked only");});
    check("record init JSON includes values validity classifications and summary",[&]{
        aoe::Capture capture;capture.id=1300;capture.label=aoe::Label::Aoe;capture.utc="2026-09-17T00:00:00Z";capture.synthetic=true;capture.complete=true;capture.frequency=1000000;capture.startQpc=1000000;capture.endQpc=11000000;capture.requestedSeconds=10;capture.durationMs=10000;capture.target.mode=aoe::TraceMode::RecordInit;capture.target.traceRva=aoe::RecordInitRva;capture.target.base=0x140000000;capture.target.traceAddress=capture.target.base+aoe::RecordInitRva;capture.target.pid=1;capture.target.creationTime=2;capture.target.image.sha256=aoe::ApprovedHash;capture.markers.push_back({1,"AOE Cast 1",3000000,2000});
        aoe::Event event;event.sequence=1;event.qpc=3500000;event.ms=2500;event.threadId=7;event.rip=capture.target.traceAddress;event.rbx=0x1111;event.rbp=0x2222;event.rcx=8000;event.recordInit.captured=true;event.recordInit.recordPointer=event.rbx;event.recordInit.ecxNew34=8000;event.recordInit.old34={true,0,4,0};event.recordInit.primaryPointer={true,0,8,0x3333};event.recordInit.primaryWord0={true,0,2,0x0209};event.recordInit.linkedPointer={true,0,8,0x4444};event.recordInit.linkedWord0={true,0,2,0x020A};aoe::ClassifyRecordInit(event.recordInit);capture.events.push_back(event);
        auto analysis=aoe::Analyze(capture);std::string error;auto path=artifacts/L"record_init_capture.json";Require(aoe::ExportJson(path,capture,analysis,error),"export: "+error);std::ifstream in(path);std::string json((std::istreambuf_iterator<char>(in)),{});for(auto token:{"\"traceMode\":\"Record Init 57E35\"","\"ecx\":8000","\"ecxHex\":\"0x1F40\"","\"readable\":true","\"primaryWord0\"","\"linkedWord0\"","\"linked020A\":true","\"primary0209Linked020A\":true","\"recordInitSummary\"","\"distinctEcxValues\"",aoe::RecordInitLimitation})Require(json.find(token)!=std::string::npos,token);
    });
    return passed;
}

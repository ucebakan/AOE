#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "analysis.hpp"
#include "tracer.hpp"
#include <Windows.h>
#include <algorithm>
#include <array>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iostream>
#include <iterator>
#include <map>
#include <set>
#include <sstream>
#include <stdexcept>
#include <string>

unsigned ProducerTests(const std::filesystem::path& artifacts);
unsigned RecordInitTests(const std::filesystem::path& artifacts);
unsigned BudgetWriteTests(const std::filesystem::path& artifacts);
unsigned TickPatchTests(const std::filesystem::path& artifacts);
unsigned Initial2xTests(const std::filesystem::path& artifacts);
unsigned InitialNxLifecycleTests(const std::filesystem::path& artifacts);
unsigned ManagerTests(const std::filesystem::path& artifacts);
unsigned CallerTraceTests(const std::filesystem::path& artifacts);
unsigned TimingTests(const std::filesystem::path& artifacts);
unsigned TargetInspectorTests(const std::filesystem::path& artifacts);
unsigned SelectedTargetProvenanceTests(const std::filesystem::path& artifacts);
unsigned TargetWriterRuntimeTests(const std::filesystem::path& artifacts);
unsigned LiveValidationTests(const std::filesystem::path& artifacts);
unsigned VisualSuppressionTests(const std::filesystem::path& artifacts);
namespace {
unsigned passed=0;
void Require(bool result,const std::string& description) {
    if(!result) throw std::runtime_error(description);
}
void Check(const std::string& name,const std::function<void()>& test) {
    test(); ++passed; std::cout<<"[PASS] "<<name<<'\n';
}
bool Until(const std::function<bool()>& predicate,DWORD milliseconds=10000) {
    const ULONGLONG end=GetTickCount64()+milliseconds;
    do { if(predicate()) return true; Sleep(10); } while(GetTickCount64()<end);
    return predicate();
}
struct Handle {
    HANDLE value=nullptr;
    Handle()=default;
    explicit Handle(HANDLE h):value(h){}
    ~Handle(){if(value&&value!=INVALID_HANDLE_VALUE) CloseHandle(value);}
    Handle(const Handle&)=delete;
    Handle& operator=(const Handle&)=delete;
};

std::filesystem::path ExePath() {
    std::wstring buffer(32768,L'\0');
    const DWORD count=GetModuleFileNameW(nullptr,buffer.data(),static_cast<DWORD>(buffer.size()));
    Require(count>0&&count<buffer.size(),"test executable path");
    buffer.resize(count); return buffer;
}

struct Fixture {
    std::filesystem::path image,report;
    std::wstring prefix;
    Handle ready,start,done,finish,process,primary;
    std::map<std::string,uint64_t> metadata;
    Fixture(const std::filesystem::path& executable,const std::filesystem::path& artifacts,bool occupied=false)
      : image(executable) {
        static unsigned sequence=0;
        const auto identifier=std::to_wstring(GetCurrentProcessId())+L"_"+std::to_wstring(++sequence);
        prefix=L"Local\\4UnityAOETracerFixture_"+identifier;
        report=artifacts/(L"fixture_"+identifier+L".txt");
        ready.value=CreateEventW(nullptr,TRUE,FALSE,(prefix+L"_Ready").c_str());
        start.value=CreateEventW(nullptr,TRUE,FALSE,(prefix+L"_Start").c_str());
        done.value=CreateEventW(nullptr,TRUE,FALSE,(prefix+L"_Done").c_str());
        finish.value=CreateEventW(nullptr,TRUE,FALSE,(prefix+L"_Finish").c_str());
        Require(ready.value&&start.value&&done.value&&finish.value,"fixture named events");
        std::wstring command=L"\""+image.wstring()+L"\" --prefix \""+prefix+L"\" --report \""+report.wstring()+L"\"";
        if(occupied) command+=L" --occupied";
        STARTUPINFOW startup{}; startup.cb=sizeof(startup);
        PROCESS_INFORMATION child{};
        Require(CreateProcessW(image.c_str(),command.data(),nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,
            image.parent_path().c_str(),&startup,&child)!=FALSE,"CreateProcess fixture: "+std::to_string(GetLastError()));
        process.value=child.hProcess; primary.value=child.hThread;
        Require(WaitForSingleObject(ready.value,5000)==WAIT_OBJECT_0,"fixture readiness");
        ReadMetadata();
        Require(metadata.at("pid")==child.dwProcessId,"fixture child PID identity");
    }
    ~Fixture() {
        if(process.value) {
            SetEvent(finish.value);
            if(WaitForSingleObject(process.value,2000)!=WAIT_OBJECT_0) {
                // Only this harness-created sacrificial child can be terminated here.
                TerminateProcess(process.value,99);
                WaitForSingleObject(process.value,2000);
            }
        }
    }
    void ReadMetadata() {
        std::ifstream input(report);
        std::string key; uint64_t value=0;
        while(input>>key>>value) metadata[key]=value;
        Require(metadata.count("pid")&&metadata.count("trace_rva"),"fixture metadata fields");
    }
    void Finish() {
        SetEvent(finish.value);
        Require(WaitForSingleObject(process.value,5000)==WAIT_OBJECT_0,"fixture exits after Finish");
        DWORD result=0; Require(GetExitCodeProcess(process.value,&result)&&result==0,"fixture exits normally: "+std::to_string(result));
    }
};

struct DebugState {
    uint64_t dr0=0,dr1=0,dr2=0,dr3=0,dr7=0;
    bool operator==(const DebugState& other) const {
        return dr0==other.dr0&&dr1==other.dr1&&dr2==other.dr2&&dr3==other.dr3&&dr7==other.dr7;
    }
};
DebugState ReadDebugState(DWORD id) {
    Handle thread(OpenThread(THREAD_GET_CONTEXT|THREAD_SUSPEND_RESUME,FALSE,id));
    Require(thread.value!=nullptr,"OpenThread fixture snapshot");
    Require(SuspendThread(thread.value)!=static_cast<DWORD>(-1),"SuspendThread fixture snapshot");
    CONTEXT context{}; context.ContextFlags=CONTEXT_DEBUG_REGISTERS;
    const BOOL read=GetThreadContext(thread.value,&context);
    const DWORD resumed=ResumeThread(thread.value);
    Require(read!=FALSE,"GetThreadContext fixture snapshot");
    Require(resumed!=static_cast<DWORD>(-1),"ResumeThread fixture snapshot");
    return {context.Dr0,context.Dr1,context.Dr2,context.Dr3,context.Dr7};
}
std::vector<uint8_t> ReadCode(const Fixture& fixture) {
    std::vector<uint8_t> bytes(32);
    uint32_t error=0;
    Require(aoe::SafeRead(fixture.process.value,fixture.metadata.at("base")+fixture.metadata.at("trace_rva"),
        bytes.data(),bytes.size(),error),"read fixture code: "+std::to_string(error));
    return bytes;
}
void Attach(aoe::Tracer& tracer,const Fixture& fixture) {
    std::string error;
    Require(tracer.attachFixture(static_cast<uint32_t>(fixture.metadata.at("pid")),fixture.image,
        fixture.metadata.at("trace_rva"),error),"request fixture attachment: "+error);
    Require(Until([&]{const auto s=tracer.status();return s.phase==aoe::Phase::Attached||s.phase==aoe::Phase::Failed||s.phase==aoe::Phase::CleanupBlocked;}),"attachment timeout");
    const auto status=tracer.status();
    Require(status.phase==aoe::Phase::Attached,"fixture attach: "+status.message+" "+status.lastError);
    Require(status.target.base==fixture.metadata.at("base"),"live module base matches fixture");
    Require(status.target.traceAddress==status.target.base+fixture.metadata.at("trace_rva"),"live base plus RVA");
    Require(status.target.verified,"fixture runtime identity verified");
}
void Detach(aoe::Tracer& tracer) {
    Require(!tracer.detachComplete(), "attached tracer worker must not report complete");
    tracer.detach();
    Require(Until([&]{return tracer.detachComplete();}), "detach completion waits for worker cleanup/resources");
    Require(Until([&]{const auto p=tracer.status().phase;return p==aoe::Phase::Detached||p==aoe::Phase::Failed||p==aoe::Phase::CleanupBlocked;}),"detach timeout");
    const auto status=tracer.status();
    Require(status.phase==aoe::Phase::Detached,"detach failed: "+status.message+" "+status.lastError);
}

const aoe::Group* FindGroup(const aoe::Analysis& result,const std::string& kind,uint32_t length,uint64_t count) {
    for(const auto& group:result.groups) if(group.kind==kind&&group.packetSize==length&&group.count==count) return &group;
    return nullptr;
}
void AnalyzerTests(const std::filesystem::path& artifacts) {
    Check("packet signatures match SHA-256 standard vectors",[&] {
        Require(aoe::Sha256(nullptr,0)=="E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855","empty SHA-256");
        const uint8_t abc[]{'a','b','c'};
        Require(aoe::Sha256(abc,3)=="BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD","abc SHA-256");
    });
    Check("SafeRead validates inaccessible and cross-region ranges",[&] {
        SYSTEM_INFO info{}; GetSystemInfo(&info);
        auto* allocation=static_cast<uint8_t*>(VirtualAlloc(nullptr,info.dwPageSize*3,MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE));
        Require(allocation!=nullptr,"test memory allocation");
        struct Allocation { void* value; ~Allocation(){VirtualFree(value,0,MEM_RELEASE);} } release{allocation};
        allocation[info.dwPageSize-1]=0xAB; allocation[info.dwPageSize]=0xCD;
        std::array<uint8_t,2> bytes{}; uint32_t error=0; DWORD old=0;
        Require(VirtualProtect(allocation+info.dwPageSize,info.dwPageSize,PAGE_READONLY,&old)!=FALSE,"test read-only page");
        Require(aoe::SafeRead(GetCurrentProcess(),reinterpret_cast<uint64_t>(allocation+info.dwPageSize-1),bytes.data(),2,error),"read across readable region boundary");
        Require(bytes[0]==0xAB&&bytes[1]==0xCD,"cross-region bytes correct");
        Require(VirtualProtect(allocation+info.dwPageSize,info.dwPageSize,PAGE_NOACCESS,&old)!=FALSE,"test inaccessible page");
        Require(!aoe::SafeRead(GetCurrentProcess(),reinterpret_cast<uint64_t>(allocation+info.dwPageSize-1),bytes.data(),2,error)&&error!=0,"range entering inaccessible page rejected");
        Require(VirtualProtect(allocation+2*info.dwPageSize,info.dwPageSize,PAGE_READWRITE|PAGE_GUARD,&old)!=FALSE,"test guard page");
        Require(!aoe::SafeRead(GetCurrentProcess(),reinterpret_cast<uint64_t>(allocation+2*info.dwPageSize),bytes.data(),1,error),"guarded region rejected");
        MEMORY_BASIC_INFORMATION state{}; VirtualQuery(allocation+2*info.dwPageSize,&state,sizeof(state));
        Require((state.Protect&PAGE_GUARD)!=0,"validation does not consume guard state");
        Require(!aoe::SafeRead(GetCurrentProcess(),UINT64_MAX,bytes.data(),bytes.size(),error),"overflowing pointer rejected");
    });
    const auto captures=aoe::SyntheticCaptures();
    Require(captures.size()>=3,"three synthetic captures available");
    Check("synthetic capture labels and identity",[&] {
        Require(captures[0].label==aoe::Label::Idle&&captures[1].label==aoe::Label::Aoe,"Idle/AOE fixtures");
        Require(captures[0].synthetic&&captures[1].synthetic,"synthetic provenance marked");
        std::string reason; Require(aoe::Compatible(captures[1],captures[0],reason),"matching fixture sessions: "+reason);
    });
    const auto result=aoe::Analyze(captures[1],&captures[0]);
    Check("nine periodic sends require caller, pattern and timing",[&] {
        const auto* target=FindGroup(result,"caller_size_pattern",24,9);
        Require(target!=nullptr,"dynamic packet target group found");
        Require(target->periodicNear1s&&target->nearNine,"target timing/count flags");
        Require(target->callerKnown&&target->callerRva==0xA12B1E,"target stable caller");
        Require(std::abs(target->meanDeltaMs-1000)<0.001&&std::abs(target->medianDeltaMs-1000)<0.001,"target mean and median delta");
        Require(target->idleCount==0&&target->timestampsMs.size()==9&&target->deltasMs.size()==8,"target event/idle evidence");
        Require(!target->stablePositions.empty()&&target->stablePositions.size()==target->stableValues.size(),"stable byte positions explicit");
        Require(!target->reasons.empty(),"rank explains evidence");
    });
    Check("nine-slot burst is not a one-second sequence",[&] {
        const auto* target=FindGroup(result,"caller_size_pattern",24,9);
        const auto* burst=FindGroup(result,"caller_size_pattern",32,9);
        Require(target&&burst,"target and burst groups exist");
        Require(!burst->periodicNear1s,"five-millisecond burst rejected as periodic");
        Require(target->score>burst->score,"timed candidate outranks nine-slot burst");
    });
    Check("idle comparison suppresses shared one-second keepalive",[&] {
        Require(result.idleCompatible,"compatible baseline recorded");
        const auto* target=FindGroup(result,"caller_size_pattern",24,9);
        const auto* shared=FindGroup(result,"caller_size_pattern",16,10);
        Require(target&&shared,"shared/target groups exist");
        Require(shared->idleCount==10,"keepalive idle count");
        Require(target->score>shared->score,"AOE-only group outranks shared keepalive");
        const auto longer=aoe::Analyze(captures[1],&captures[2]);
        const auto* normalized=FindGroup(longer,"caller_size_pattern",16,10);
        Require(normalized!=nullptr,"normalized baseline group exists");
        Require(std::abs(shared->idleRate-normalized->idleRate)<1e-9,"idle rate normalized for baseline duration");
    });
    Check("incompatible target session is excluded",[&] {
        auto unrelated=captures[0]; ++unrelated.target.creationTime;
        std::string reason;
        Require(!aoe::Compatible(captures[1],unrelated,reason)&&!reason.empty(),"PID session reuse rejected");
        Require(!aoe::Analyze(captures[1],&unrelated).idleCompatible,"incompatible idle not used");
        unrelated=captures[0]; unrelated.target.image.sha256="different-image";
        Require(!aoe::Compatible(captures[1],unrelated,reason),"different image hash rejected");
    });
    Check("equal captured prefixes do not imply equal full packets",[&] {
        Require(FindGroup(result,"exact_bytes",160,1)!=nullptr,"160-byte packet prefix group");
        Require(FindGroup(result,"exact_bytes",192,1)!=nullptr,"192-byte packet prefix remains distinct");
        Require(FindGroup(result,"exact_bytes",128,1)!=nullptr,"complete 128-byte packet remains distinct");
        for(const auto& group:result.groups) if(group.packetSize==48) {
            Require(group.score==0,"invalid packet buffers do not earn signature evidence");
        }
        auto collision=captures[1]; collision.events={captures[1].events.front(),captures[1].events.front()};
        collision.events[1].bytes[0]^=0xFF;
        collision.events[1].ms+=1000;
        const auto separated=aoe::Analyze(collision,nullptr);
        unsigned exactGroups=0;
        for(const auto& group:separated.groups) if(group.kind=="exact_bytes") {
            ++exactGroups; Require(group.count==1,"unequal bytes remain separate despite equal supplied hash");
        }
        Require(exactGroups==2,"exact grouping verifies bytes, not only hash text");
    });
    Check("empty and singleton input remains finite",[&] {
        auto empty=captures[1]; empty.events.clear();
        Require(aoe::Analyze(empty,nullptr).groups.empty(),"empty capture has no groups");
        empty.events.push_back(captures[1].events.front());
        for(const auto& group:aoe::Analyze(empty,nullptr).groups) {
            Require(std::isfinite(group.score)&&std::isfinite(group.meanDeltaMs)&&std::isfinite(group.coefficientVariation),"singleton calculations finite");
            Require(!group.periodicNear1s,"singleton is not periodic");
        }
    });
    Check("JSON exports raw and grouped research evidence",[&] {
        std::string error;
        const auto destination=artifacts/L"synthetic_capture.json";
        Require(aoe::ExportJson(destination,captures[1],result,error),"ExportJson: "+error);
        std::ifstream stream(destination); const std::string json((std::istreambuf_iterator<char>(stream)),{});
        Require(json.size()>1000&&json.find("A12B1E")!=std::string::npos,"caller evidence in JSON");
        Require(json.find("caller_size_pattern")!=std::string::npos,"group kind in JSON");
        Require(json.find("synthetic")!=std::string::npos,"synthetic provenance in JSON");
    });
}

void TracerTests(const std::filesystem::path& fixtureImage,const std::filesystem::path& artifacts) {
    Check("duration bounds manual stop and multiple cast markers on the actual engine",[&] {
        Fixture fixture(fixtureImage,artifacts);aoe::Tracer tracer;Attach(tracer,fixture);std::string error;aoe::Marker one,two;
        Require(!tracer.markAoeCast(one,error),"marker outside capture rejected");
        for(double invalid:{4.0,5.5,31.0})Require(!tracer.startCapture(aoe::Label::Aoe,invalid,error),"engine duration validation");
        Require(tracer.startCapture(aoe::Label::Aoe,30,error),"30 second maximum accepted");
        Require(tracer.markAoeCast(one,error),"first cast marker");Sleep(25);Require(tracer.markAoeCast(two,error),"second cast marker");
        Require(one.id==1&&two.id==2&&two.ms>one.ms&&two.qpc>one.qpc,"sequential timestamp markers");
        tracer.stopCapture();auto done=tracer.takeCompleted();Require(done.size()==1&&done[0]->requestedSeconds==30&&done[0]->durationMs<1000&&done[0]->stopReason=="Stopped by operator"&&!done[0]->complete&&done[0]->markers.size()==2,"manual stop retains actual duration reason and markers");
        Require(!tracer.markAoeCast(one,error),"marker after stop rejected");
        Require(tracer.startCapture(aoe::Label::Idle,5,error),"5 second minimum accepted");tracer.stopCapture();tracer.takeCompleted();
        Detach(tracer);fixture.Finish();
    });
    Check("13 second timed capture reports elapsed remaining and exact requested end",[&] {
        Fixture fixture(fixtureImage,artifacts);aoe::Tracer tracer;Attach(tracer,fixture);std::string error;
        Require(tracer.startCapture(aoe::Label::Idle,13,error),"13 second capture starts");auto state=tracer.status();
        Require(state.requestedSeconds==13&&state.remainingMs>12000&&state.remainingMs<=13000,"remaining countdown starts from chosen duration");
        Require(Until([&]{return !tracer.status().capturing;},14500),"13 second timer ends capture");
        auto done=tracer.takeCompleted();Require(done.size()==1&&done[0]->complete&&done[0]->requestedSeconds==13&&std::abs(done[0]->durationMs-13000)<0.001&&done[0]->stopReason=="Requested capture duration completed","13 second actual duration and stop reason");
        Require(tracer.status().remainingMs==0,"remaining is zero after timeout");Detach(tracer);fixture.Finish();
    });
    Check("producer mode uses the hardware entry breakpoint and preserves cleanup",[&] {
        Fixture fixture(fixtureImage,artifacts);auto code=ReadCode(fixture);aoe::Tracer tracer;std::string error;
        Require(tracer.attachFixture(uint32_t(fixture.metadata.at("pid")),fixture.image,fixture.metadata.at("trace_rva"),error,aoe::TraceMode::Producer),"producer fixture attach");
        Require(Until([&]{return tracer.status().phase==aoe::Phase::Attached;}),"producer attached");
        Require(tracer.startCapture(aoe::Label::Aoe,5,error),"producer capture");SetEvent(fixture.start.value);
        Require(WaitForSingleObject(fixture.done.value,10000)==WAIT_OBJECT_0,"producer fixture calls finish");tracer.stopCapture();auto done=tracer.takeCompleted();
        Require(done.size()==1&&done[0]->events.size()==60&&done[0]->target.mode==aoe::TraceMode::Producer,"producer all 60 hits");
        for(auto& e:done[0]->events){Require(e.rip==done[0]->target.traceAddress&&e.producer.captured&&e.producer.stackReadable&&e.producer.rawStack.size()==0xA0,"entry RIP and stack snapshot");Require(e.producer.arguments.size()==18&&e.producer.arguments[0].bits==e.rcx&&e.producer.arguments[2].bits==uint32_t(e.r8),"entry raw registers decoded");Require(e.bytes.empty()&&e.packetLength==0,"producer RDX not treated as packet");}
        Detach(tracer);Require(ReadCode(fixture)==code,"producer code unchanged");fixture.Finish();
    });
    Check("hardware trace covers existing and newly created threads",[&] {
        Fixture fixture(fixtureImage,artifacts);
        const DWORD first=static_cast<DWORD>(fixture.metadata.at("thread1")),second=static_cast<DWORD>(fixture.metadata.at("thread2"));
        const auto originalFirst=ReadDebugState(first),originalSecond=ReadDebugState(second);
        Require((originalFirst.dr7&0xFF)==0,"fixture has no enabled breakpoint before attach: DR3="+aoe::Hex(originalFirst.dr3)+" DR7="+aoe::Hex(originalFirst.dr7));
        const auto code=ReadCode(fixture);
        aoe::Tracer tracer; Attach(tracer,fixture);
        std::string error;
        Require(tracer.startCapture(aoe::Label::Aoe,10,error),"fixture capture starts: "+error);
        SetEvent(fixture.start.value);
        Require(WaitForSingleObject(fixture.done.value,10000)==WAIT_OBJECT_0,"fixture completes sixty calls without RF loop");
        fixture.ReadMetadata();
        tracer.stopCapture();
        std::vector<std::shared_ptr<aoe::Capture>> completed;
        Require(Until([&]{completed=tracer.takeCompleted();return !completed.empty();}),"capture stop completed");
        const auto& capture=*completed.back();
        Require(capture.events.size()==60,"exact sixty captured hits, got "+std::to_string(capture.events.size()));
        Require(tracer.status().totalHits==60,"total hit count sixty");
        std::map<DWORD,unsigned> counts;
        std::set<std::pair<unsigned,unsigned>> seen;
        for(const auto& event:capture.events) {
            ++counts[event.threadId];
            const unsigned worker=static_cast<unsigned>(event.rcx&0xFF),iteration=static_cast<unsigned>(event.r9&0xFF);
            Require(worker>=1&&worker<=3&&iteration<20,"RCX and R9 values captured at function entry");
            Require(seen.insert({worker,iteration}).second,"each function invocation seen once");
            Require((event.r8&0xFFFFFFFF00000000ull)==0xABCDEF0100000000ull,"R8 upper bits preserved");
            Require(event.packetLength==static_cast<uint32_t>(event.r8),"R8D low32 interpretation");
            Require(event.returnReadable&&event.returnInModule&&event.returnAddress==capture.target.base+event.returnRva,"entry return address points into fixture");
            Require(event.rsp!=0&&event.qpc>=capture.startQpc&&event.ms>=0&&event.deltaMs>=0,"stack and high-resolution timestamp");
            if(iteration==1) Require(event.packetLength==48&&!event.bufferReadable&&event.bytes.empty()&&event.bufferReadError!=0,"invalid RDX is safe and diagnostic");
            else if(iteration==2) Require(event.packetLength==0&&event.bytes.empty(),"zero packet length never reads pointer");
            else {
                const auto length=iteration==3 ? 128u:32u;
                Require(event.bufferReadable&&event.bytes.size()==length,"buffer capture bounded to min(length,128)");
                Require(event.bytes[0]==worker&&event.bytes[1]==iteration,"packet snapshot from correct call");
                Require(event.fullPacket==(iteration!=3),"captured prefix is not labelled full packet");
                Require(event.signature==aoe::Sha256(event.bytes.data(),event.bytes.size()),"captured-byte signature verified");
            }
        }
        Require(counts.size()==3&&counts[first]==20&&counts[second]==20&&counts[static_cast<DWORD>(fixture.metadata.at("thread3"))]==20,"all three threads fully covered");
        Detach(tracer);
        Require(ReadDebugState(first)==originalFirst&&ReadDebugState(second)==originalSecond,"original inactive debug register state restored");
        const auto newThread=ReadDebugState(static_cast<DWORD>(fixture.metadata.at("thread3")));
        Require((newThread.dr7&0xFF)==0,"new thread has no leftover enabled breakpoint");
        Require(ReadCode(fixture)==code,"code bytes unchanged across trace and detach");
        aoe::ImageInfo disk; Require(aoe::InspectImage(fixture.image,fixture.metadata.at("trace_rva"),disk,error),"fixture image reinspection");
        Require(disk.sha256==capture.target.image.sha256,"fixture executable hash unchanged");
        Require(aoe::ExportJson(artifacts/L"hardware_fixture_capture.json",capture,aoe::Analyze(capture,nullptr),error),"fixture capture export");
        fixture.Finish();
    });
    Check("idle detach and repeated attach preserve target execution",[&] {
        Fixture fixture(fixtureImage,artifacts);
        const DWORD first=static_cast<DWORD>(fixture.metadata.at("thread1"));
        const auto original=ReadDebugState(first),code=ReadDebugState(static_cast<DWORD>(fixture.metadata.at("thread2")));
        aoe::Tracer tracer;
        for(unsigned round=0;round<2;++round) {
            Attach(tracer,fixture); Detach(tracer);
            Require(ReadDebugState(first)==original,"idle detach restores original state");
            Require(ReadDebugState(static_cast<DWORD>(fixture.metadata.at("thread2")))==code,"repeat detach preserves second thread");
        }
        fixture.Finish();
    });
    Check("destructor performs normal cleanup while capture is active",[&] {
        Fixture fixture(fixtureImage,artifacts);
        const DWORD first=static_cast<DWORD>(fixture.metadata.at("thread1"));
        const auto original=ReadDebugState(first);
        {
            aoe::Tracer tracer; Attach(tracer,fixture); std::string error;
            Require(tracer.startCapture(aoe::Label::Idle,10,error),"idle capture before destructor");
        }
        Require(ReadDebugState(first)==original,"destructor restores hardware slot");
        BOOL debugged=TRUE;
        Require(CheckRemoteDebuggerPresent(fixture.process.value,&debugged)&&!debugged,"destructor detaches debugger");
        fixture.Finish();
    });
    Check("detach during concurrent sends drains owned exceptions",[&] {
        Fixture fixture(fixtureImage,artifacts);
        const DWORD first=static_cast<DWORD>(fixture.metadata.at("thread1")); const auto original=ReadDebugState(first);
        const auto code=ReadCode(fixture);
        aoe::Tracer tracer; Attach(tracer,fixture); std::string error;
        Require(tracer.startCapture(aoe::Label::Aoe,10,error),"burst capture starts");
        SetEvent(fixture.start.value);
        Require(Until([&]{return tracer.status().totalHits>=3;}),"burst reaches hardware breakpoint");
        Detach(tracer);
        Require(WaitForSingleObject(fixture.done.value,10000)==WAIT_OBJECT_0,"remaining calls run normally after detach");
        Require(ReadDebugState(first)==original,"burst detach restores debug register state");
        Require(ReadCode(fixture)==code,"burst detach leaves code untouched");
        fixture.Finish();
    });
    Check("normal target exit ends capture and debugger ownership",[&] {
        Fixture fixture(fixtureImage,artifacts);
        aoe::Tracer tracer; Attach(tracer,fixture); std::string error;
        Require(tracer.startCapture(aoe::Label::Idle,10,error),"capture starts before target exit");
        SetEvent(fixture.finish.value);
        Require(Until([&]{return tracer.status().phase==aoe::Phase::Detached;}),"target exit clears attachment");
        Require(!tracer.status().capturing,"target exit ends active capture");
        const auto completed=tracer.takeCompleted();
        Require(!completed.empty()&&completed.back()->stopReason=="Target exited","capture records target-exit stop reason");
        fixture.Finish();
    });
    Check("occupied hardware slots fail safely without overwriting state",[&] {
        Fixture fixture(fixtureImage,artifacts,true);
        const DWORD first=static_cast<DWORD>(fixture.metadata.at("thread1"));
        const auto original=ReadDebugState(first);
        const DWORD primaryId=GetThreadId(fixture.primary.value);
        const auto primaryState=ReadDebugState(primaryId);
        Require((original.dr7&0xFF)==0x55,"occupied fixture slots set");
        aoe::Tracer tracer; std::string error;
        const bool requested=tracer.attachFixture(static_cast<uint32_t>(fixture.metadata.at("pid")),fixture.image,fixture.metadata.at("trace_rva"),error);
        if(requested) Require(Until([&]{const auto p=tracer.status().phase;return p==aoe::Phase::Failed||p==aoe::Phase::Detached||p==aoe::Phase::CleanupBlocked;}),"occupied slots failure completion");
        const auto status=tracer.status();
        Require(status.phase!=aoe::Phase::Attached&&status.phase!=aoe::Phase::CleanupBlocked,"occupied slots safely rejected: "+status.lastError);
        Require(!error.empty()||!status.lastError.empty(),"occupied slot rejection contains diagnostic");
        Require(ReadDebugState(first)==original,"occupied slots preserved after partial attach rollback");
        Require(ReadDebugState(primaryId)==primaryState,"previously armed main thread rolled back");
        BOOL debugged=TRUE;
        Require(CheckRemoteDebuggerPresent(fixture.process.value,&debugged)&&!debugged,"failed attachment released debugger");
        fixture.Finish();
    });
    Check("immediate attachment cancellation leaves target running",[&] {
        Fixture fixture(fixtureImage,artifacts);
        const DWORD first=static_cast<DWORD>(fixture.metadata.at("thread1")); const auto original=ReadDebugState(first);
        aoe::Tracer tracer; std::string error;
        Require(tracer.attachFixture(static_cast<uint32_t>(fixture.metadata.at("pid")),fixture.image,fixture.metadata.at("trace_rva"),error),"immediate cancellation attach request");
        Detach(tracer);
        Require(ReadDebugState(first)==original,"immediate cancellation restores thread state");
        fixture.Finish();
    });
}
}

int wmain(int argc,wchar_t** argv) {
    try {
        const auto executable=ExePath();
        const auto artifacts=executable.parent_path()/L"test-artifacts";
        std::filesystem::create_directories(artifacts);
        aoe::InitLog(artifacts);
        AnalyzerTests(artifacts);
        passed+=TimingTests(artifacts);passed+=ProducerTests(artifacts);passed+=RecordInitTests(artifacts);passed+=BudgetWriteTests(artifacts);passed+=TickPatchTests(artifacts);passed+=Initial2xTests(artifacts);passed+=InitialNxLifecycleTests(artifacts);passed+=CallerTraceTests(artifacts);passed+=TargetInspectorTests(artifacts);passed+=SelectedTargetProvenanceTests(artifacts);passed+=TargetWriterRuntimeTests(artifacts);passed+=LiveValidationTests(artifacts);passed+=VisualSuppressionTests(artifacts);passed+=ManagerTests(artifacts);
        const bool analysisOnly=argc>1&&std::wstring(argv[1])==L"--analysis-only";
        if(!analysisOnly) TracerTests(executable.parent_path()/L"trace_fixture.exe",artifacts);
        std::cout<<passed<<" test groups passed. No TClient attachment was performed.\n";
        return 0;
    } catch(const std::exception& error) {
        std::cerr<<"[FAIL] "<<error.what()<<" ("<<passed<<" groups passed before failure)\n";
        return 1;
    }
}

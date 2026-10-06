#include "aoe_locator.hpp"
#include "tracer.hpp"
#include <iostream>
#include <atomic>
#include <mutex>

namespace {
std::atomic<bool> stop{false};std::mutex hostMutex;aoe::Tracer* active=nullptr;
struct ActiveTracer {
    explicit ActiveTracer(aoe::Tracer& tracer){std::lock_guard lock(hostMutex);active=&tracer;}
    ~ActiveTracer(){std::lock_guard lock(hostMutex);active=nullptr;}
};
BOOL WINAPI Stop(DWORD event){
    stop=true;
    if(event==CTRL_CLOSE_EVENT||event==CTRL_LOGOFF_EVENT||event==CTRL_SHUTDOWN_EVENT){
        // Windows allows only a short cleanup window on console close.
        // Request restore here rather than waiting for the polling loop.
        std::lock_guard lock(hostMutex);
        if(active){active->detach();const auto until=GetTickCount64()+4000;
            while(!active->detachComplete()&&GetTickCount64()<until)Sleep(10);}
    }
    return TRUE;
}
int Fail(const std::string& reason,int code){
    aoe::Log("class_probe_host_error",reason);
    MessageBoxW(nullptr,aoe::Wide(reason).c_str(),L"AOE tanı kaydı başlatılamadı",MB_OK|MB_ICONERROR);
    return code;
}
}
// Separate, read-only investigation host. It never arms replay or visual writes.
int wmain(int argc,wchar_t** argv){
    using namespace aoe;
    if(argc!=3){std::wcerr<<L"Usage: AoeClassProbe <profile.json> <output.json>\n";return 2;}
    SetConsoleCtrlHandler(Stop,TRUE);
    SetConsoleTitleW(L"AOE Class Probe - READ ONLY");
    InitLog(std::filesystem::path(argv[2]).parent_path());
    BuildProfile profile;std::string error;AoeLocatorResult located;
    if(!LoadBuildProfile(argv[1],profile,error)||!LocateAoeImage(TargetPath,located,error,&profile)||!ValidateAoeProfile(profile,located,error)){
        return Fail(error,3);
    }
    Tracer tracer;
    ActiveTracer guard(tracer);
#ifdef AOE_CLASS_LOOKUP_PROBE
    const auto* lookup=FindProfileAnchor(profile,"templateLookupRva");
    if(!lookup||!lookup->rva){std::cerr<<"No verified template lookup anchor\n";return 7;}
    profile.runtime.classProbeLookupRva=lookup->rva;
#endif
#ifdef AOE_CLASS_ROUTE_PROBE
    AoeLocatorResult fresh;
    if(!LocateAoeImage(TargetPath,fresh,error,&profile)||!ResolveAlternateInitialRoute(fresh.image,profile.runtime,error))return Fail(error,7);
    profile.runtime.classProbeWorkerEntry=true;
#endif
    if(!tracer.attach(profile,error,TraceMode::TargetInspector))return Fail(error,4);
    const auto started=GetTickCount64();bool marked=false;size_t inputs=0;uint64_t hits=0;
    std::cout<<"4Unity AOE: READ ONLY class trace. Initial Nx / Hide are OFF.\n";
    while(!stop&&GetTickCount64()-started<180000&&!std::filesystem::exists(std::filesystem::path(argv[2]).parent_path()/L"stop.request")){
        auto s=tracer.status();
        if(s.phase==Phase::Failed)return Fail(s.lastError,5);
        if(s.phase==Phase::Attached&&!marked){
            Marker marker;
            if(!tracer.markTargetInspector(marker,error))return Fail(error,6);
            marked=true;std::cout<<"READY: cast one normal Rain of Arrows, then wait.\n";
            Log("class_probe_ready","Capture started; replay and gameplay writes disabled.");
        }
        if(s.totalHits!=hits){hits=s.totalHits;std::cout<<"Observed hits: "<<hits<<" parsed inputs: "<<s.targetInspector.parsedInputs.size()<<'\n';}
        if(s.targetInspector.parsedInputs.size()!=inputs){
            inputs=s.targetInspector.parsedInputs.size();
            for(const auto& p:s.targetInspector.parsedInputs)std::cout<<"Input skill="<<p.operationWord<<" owner="<<Hex(p.ownerPointer)<<" records="<<unsigned(p.recordCount)<<'\n';
            if(!ExportTargetInspectorJson(argv[2],s.targetInspector,error)){std::cerr<<error<<'\n';stop=true;}
        }
        Sleep(100);
    }
    auto last=tracer.status();
    Log("class_probe_finished","stop="+std::to_string(stop.load())+" hits="+std::to_string(last.totalHits)+" inputs="+std::to_string(last.targetInspector.parsedInputs.size()));
    if(marked&&!ExportTargetInspectorJson(argv[2],last.targetInspector,error))std::cerr<<error<<'\n';
    tracer.detach();
    std::cout<<"Trace finished; restoring debugger state.\n";
    return 0;
}

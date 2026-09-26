#include "aoe_diagnostic.hpp"
#include "aoe_locator.hpp"
#include "tracer.hpp"
#include <algorithm>
#include <fstream>
#include <iterator>
#include <sstream>
#include <stdexcept>

namespace aoe {
bool VerifyTarget(uint32_t,const std::filesystem::path&,uint64_t,bool,bool,const BuildProfile*,TargetInfo&,HANDLE&,std::string&);
namespace {
struct ReadHandle {
    HANDLE value=nullptr;
    explicit ReadHandle(HANDLE handle=nullptr):value(handle){}
    ~ReadHandle(){if(value&&value!=INVALID_HANDLE_VALUE)CloseHandle(value);}
    ReadHandle(const ReadHandle&)=delete;
    ReadHandle& operator=(const ReadHandle&)=delete;
};
bool FindExactTargetPid(uint32_t& pid,std::string& error){
    pid=0;
    const auto candidates=FindProcesses();
    const DWORD enumerationError=GetLastError();
    if(enumerationError){error=WinError("AOE diagnostic process enumeration",enumerationError);return false;}
    unsigned matches=0;
    for(const auto candidate:candidates){
        ReadHandle query(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,candidate));
        if(!query.value){error=WinError("AOE diagnostic exact-path query PID="+std::to_string(candidate),GetLastError());return false;}
        wchar_t path[32768]{};DWORD length=DWORD(std::size(path));
        if(!QueryFullProcessImageNameW(query.value,0,path,&length)){error=WinError("AOE diagnostic process image path",GetLastError());return false;}
        if(CompareStringOrdinal(path,int(length),TargetPath,-1,TRUE)==CSTR_EQUAL){pid=candidate;++matches;}
    }
    if(matches!=1){error=matches?"Exact-path TClient oturumu belirsiz; birden fazla eslesme.":"Exact-path TClient calismiyor; canli dogrulama yapilmadi.";pid=0;return false;}
    error.clear();return true;
}
bool SessionStillAlive(HANDLE process,const TargetInfo& target,std::string& error){
    FILETIME creation{},exit{},kernel{},user{};DWORD exitCode=0;
    if(!GetProcessTimes(process,&creation,&exit,&kernel,&user)){error=WinError("AOE diagnostic final session times",GetLastError());return false;}
    if(!GetExitCodeProcess(process,&exitCode)){error=WinError("AOE diagnostic final process status",GetLastError());return false;}
    const uint64_t creationTime=(uint64_t(creation.dwHighDateTime)<<32)|creation.dwLowDateTime;
    if(creationTime!=target.creationTime||exit.dwLowDateTime||exit.dwHighDateTime||exitCode!=STILL_ACTIVE){error="TClient oturumu dogrulama sirasinda sona erdi veya degisti.";return false;}
    return true;
}
void WriteStdout(const std::string& text){
    const HANDLE output=GetStdHandle(STD_OUTPUT_HANDLE);
    if(!output||output==INVALID_HANDLE_VALUE)return;
    size_t offset=0;
    while(offset<text.size()){
        const DWORD count=DWORD((std::min)(text.size()-offset,size_t(0x10000)));DWORD written=0;
        if(!WriteFile(output,text.data()+offset,count,&written,nullptr)||!written)break;
        offset+=written;
    }
}
}
int RunAoeDiagnostic(const std::filesystem::path& projectRoot,bool offline){
    AoeLocatorResult located;BuildProfile profile;TargetInfo target;std::filesystem::path selectedProfile;
    std::string error;bool staticPassed=false,livePassed=false;int exitCode=2;
    try{
        if(LocateAoeImage(TargetPath,located,error)&&located.ready&&
           SelectBuildProfile(projectRoot/L"profiles",located.image.sha256,profile,selectedProfile,error)&&
           ValidateAoeProfile(profile,located,error)&&RuntimeLayoutSupportsInitialNx(profile.runtime,error)){
            staticPassed=true;
            if(offline)exitCode=0;
            else{
                uint32_t pid=0;
                if(FindExactTargetPid(pid,error)){
                    HANDLE raw=nullptr;
                    const bool verified=VerifyTarget(pid,TargetPath,profile.runtime.initialCallRva,false,false,&profile,target,raw,error);
                    ReadHandle process(raw);
                    std::vector<ResolvedAnchor> anchors;
                    if(verified&&ResolveAndValidateProfile(profile,target,anchors,error)&&
                       ValidateAoeLive(process.value,target,located,error)&&SessionStillAlive(process.value,target,error)){
                        livePassed=true;exitCode=0;
                    }
                }
            }
        }
        if(!staticPassed&&error.empty())error=located.error.empty()?"AOE statik locator/profil kapisi gecilemedi.":located.error;
    }catch(const std::exception& exception){error=std::string("UTILITY_EXCEPTION_CPP: ")+exception.what();exitCode=2;}
    catch(...){error="UTILITY_EXCEPTION_CPP: bilinmeyen istisna";exitCode=2;}
    std::ostringstream out;
    out<<"[BUILD]\nPath="<<Utf8(TargetPath)<<"\nSHA256="<<located.image.sha256
       <<"\nBase="<<Hex(target.base)<<"\nSizeOfImage="<<Hex(located.image.imageSize)
       <<"\nFileSize="<<located.image.fileSize<<"\nPETimestamp="<<Hex(located.image.timestamp)
       <<"\nArchitecture="<<(located.image.amd64?"AMD64":"UNVERIFIED")
       <<"\nPreferredImageBase="<<Hex(located.preferredBase)<<"\nPID="<<target.pid
       <<"\nCreationFILETIME="<<target.creationTime<<"\nProfile="<<Utf8(selectedProfile.wstring())
       <<"\nMode="<<(offline?"OFFLINE":"READ_ONLY_LIVE")<<"\nTimestamp="<<UtcNow()<<"\n\n[AOE SIGNATURES]\n";
    for(const auto& signature:located.signatures){
        out<<signature.name<<"\nRVA="<<Hex(signature.rva)<<"\nMatches="<<signature.matches.size()
           <<"\nRawMatches="<<signature.matches.size()<<"\nValidMatches="<<(signature.valid?1:0)
           <<"\nValidation="<<(signature.valid?"PASS":"FAIL")<<"\nPattern="<<signature.pattern
           <<"\nEvidence="<<signature.evidence<<"\n\n";
    }
    out<<"[AOE READY]\n"<<(staticPassed&&livePassed?"YES":"NO")
       <<"\nStaticProfileValidation="<<(staticPassed?"PASS":"FAIL")
       <<"\nLiveSignatureValidation="<<(offline?"NOT_REQUESTED":livePassed?"PASS":"FAIL")
       <<"\nRuntimeArmed=NO\nReplayLiveValidation="<<(profile.runtime.liveValidationRequired?"REQUIRED_NOT_PERFORMED":"EXISTING_PROFILE_POLICY")
       <<"\nDiagnosticOnly=YES\nDebuggerAttached=NO\nHardwareBreakpoints=0\nGameplayWrites=0"
       <<"\nNote=READY yalniz locator/profil uygunlugudur; mevcut ARM ve canli replay dogrulamasi atlanmaz."
       <<"\n\n[COUNTERS]\nInitial0209Count=UNAVAILABLE\nRepeated020ACount=UNAVAILABLE\nLinked020BCount=UNAVAILABLE"
       <<"\nSharedOperationHits=UNAVAILABLE\nAoeLifecycleHits=UNAVAILABLE\nRequestedRepeatCount=UNAVAILABLE\nCompletedRepeatCount=UNAVAILABLE"
       <<"\nCounterScope=Locator tani modu calisma olaylarini gozlemlemez; sifir sonucu varsayilmaz."
       <<"\nError="<<(error.empty()?"NONE":error)<<"\nExitCode="<<exitCode<<'\n';
    const std::string report=out.str();WriteStdout(report);
    try{
        const auto path=projectRoot/L"logs"/L"aoe-diagnostic.txt";
        std::filesystem::create_directories(path.parent_path());
        std::ofstream log(path,std::ios::binary|std::ios::trunc);
        if(!log)throw std::runtime_error("Tani raporu acilamadi.");
        log<<report;log.flush();
        if(!log)throw std::runtime_error("Tani raporu yazilamadi.");
    }catch(const std::exception& exception){WriteStdout(std::string("DIAGNOSTIC_LOG_ERROR=")+exception.what()+"\n");return 3;}
    return exitCode;
}
}

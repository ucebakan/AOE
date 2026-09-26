#include "tick_patch.hpp"
#include <algorithm>
#include <array>
#include <fstream>
#include <iomanip>
#include <locale>
#include <sstream>

namespace aoe {
namespace {
struct Spec {uint64_t rva;std::vector<uint8_t> original,patched;size_t immediateOffset;};
const std::array<Spec,3>& Specs(){static const std::array<Spec,3> specs{{
    {TickThresholdRva,{0x81,0xF9,0xE8,0x03,0x00,0x00},{0x81,0xF9,0x64,0x00,0x00,0x00},2},
    {TickSubtractRva,{0x41,0x81,0x85,0x90,0x00,0x00,0x00,0x18,0xFC,0xFF,0xFF},{0x41,0x81,0x85,0x90,0x00,0x00,0x00,0x9C,0xFF,0xFF,0xFF},7},
    {TickRepeatRva,{0x41,0x81,0xBD,0x90,0x00,0x00,0x00,0xE8,0x03,0x00,0x00},{0x41,0x81,0xBD,0x90,0x00,0x00,0x00,0x64,0x00,0x00,0x00},7}
}};return specs;}
std::string Quote(const std::string& value){std::ostringstream out;out<<'"';for(unsigned char c:value){if(c=='"'||c=='\\')out<<'\\'<<char(c);else if(c=='\n')out<<"\\n";else if(c=='\r')out<<"\\r";else if(c=='\t')out<<"\\t";else if(c<0x20)out<<"\\u"<<std::hex<<std::setw(4)<<std::setfill('0')<<unsigned(c)<<std::dec;else out<<char(c);}out<<'"';return out.str();}
const char* Bool(bool value){return value?"true":"false";}
std::string Number(double value){std::ostringstream out;out.imbue(std::locale::classic());out<<std::fixed<<std::setprecision(6)<<value;return out.str();}
bool ReadBytes(const TickPatchIo& io,uint64_t address,size_t size,std::vector<uint8_t>& bytes,std::string& error){bytes.assign(size,0);if(!io.read(address,bytes.data(),bytes.size(),error)){bytes.clear();return false;}return true;}
size_t PatchVerifiedCount(const TickPatchExperimentState& experiment){return std::count_if(experiment.targets.begin(),experiment.targets.end(),[](const auto& target){return target.patchReadbackVerified;});}
size_t RestoreVerifiedCount(const TickPatchExperimentState& experiment){return std::count_if(experiment.targets.begin(),experiment.targets.end(),[](const auto& target){return target.restoreReadbackVerified;});}
void SetRestoreTime(TickPatchExperimentState& experiment){experiment.restoreTimestamp=UtcNow();experiment.restoreQpc=QpcNow();if(experiment.frequency>0&&experiment.armQpc>0)experiment.durationMs=double(std::max<int64_t>(experiment.armQpc,experiment.restoreQpc)-experiment.armQpc)*1000.0/double(experiment.frequency);}
bool RestoreTargets(const TickPatchIo& io,std::vector<TickPatchTarget>& targets,std::string& error){
    bool writesOk=true;
    for(size_t n=targets.size();n>0;--n){std::string current;if(!io.write(targets[n-1].resolvedAddress,targets[n-1].originalBytes.data(),targets[n-1].originalBytes.size(),current)){writesOk=false;if(error.empty())error="Restore write failed at TClient.exe+"+Hex(targets[n-1].rva)+": "+current;}}
    bool readbacksOk=true;
    for(auto& target:targets){std::string current;const bool read=ReadBytes(io,target.resolvedAddress,target.originalBytes.size(),target.actualRestoredReadback,current);target.restoreReadbackVerified=read&&target.actualRestoredReadback==target.originalBytes;if(!target.restoreReadbackVerified){readbacksOk=false;if(error.empty())error="Restore readback failed at TClient.exe+"+Hex(target.rva)+(current.empty()?"":": "+current);}}
    return writesOk&&readbacksOk;
}
}

TickPatchIo ProcessTickPatchIo(HANDLE process){TickPatchIo io;
    io.executable=[process](uint64_t address,size_t size,std::string& error){MEMORY_BASIC_INFORMATION m{};if(!size||address>UINT64_MAX-size){error="Tick patch target range overflows the address space.";return false;}if(!VirtualQueryEx(process,reinterpret_cast<void*>(address),&m,sizeof(m))){error=WinError("VirtualQueryEx(tick patch)",GetLastError());return false;}const DWORD p=m.Protect&0xff;const bool executable=p==PAGE_EXECUTE||p==PAGE_EXECUTE_READ||p==PAGE_EXECUTE_READWRITE||p==PAGE_EXECUTE_WRITECOPY;const uint64_t base=reinterpret_cast<uint64_t>(m.BaseAddress);if(m.State!=MEM_COMMIT||(m.Protect&PAGE_GUARD)||!executable||base>address||!m.RegionSize||base>UINT64_MAX-m.RegionSize||address+size>base+m.RegionSize){error="Tick patch target is not a complete committed executable range.";return false;}error.clear();return true;};
    io.read=[process](uint64_t address,void* data,size_t size,std::string& error){uint32_t code=0;if(!SafeRead(process,address,data,size,code)){error=WinError("ReadProcessMemory(tick patch)",code);return false;}error.clear();return true;};
    io.write=[process](uint64_t address,const void* data,size_t size,std::string& error){DWORD old=0;if(!VirtualProtectEx(process,reinterpret_cast<void*>(address),size,PAGE_EXECUTE_READWRITE,&old)){error=WinError("VirtualProtectEx(enable tick patch)",GetLastError());return false;}SetLastError(ERROR_SUCCESS);SIZE_T written=0;const BOOL result=WriteProcessMemory(process,reinterpret_cast<void*>(address),data,size,&written);DWORD writeCode=result&&written==size?ERROR_SUCCESS:GetLastError();if(!writeCode&&(!result||written!=size))writeCode=ERROR_WRITE_FAULT;const BOOL flushed=FlushInstructionCache(process,reinterpret_cast<void*>(address),size);const DWORD flushCode=flushed?ERROR_SUCCESS:GetLastError();DWORD ignored=0;const BOOL restored=VirtualProtectEx(process,reinterpret_cast<void*>(address),size,old,&ignored);const DWORD restoreCode=restored?ERROR_SUCCESS:GetLastError();if(writeCode){error=WinError("WriteProcessMemory(tick immediate/original)",writeCode);return false;}if(flushCode){error=WinError("FlushInstructionCache(tick patch)",flushCode);return false;}if(restoreCode){error=WinError("VirtualProtectEx(restore tick protection)",restoreCode);return false;}error.clear();return true;};return io;
}

TickPatchExperimentState CreateTickPatchExperimentState(const TargetInfo& target,bool observationAvailable){TickPatchExperimentState experiment;experiment.armed=true;experiment.pid=target.pid;experiment.creationTime=target.creationTime;experiment.moduleBase=target.base;experiment.imageSha256=target.image.sha256;experiment.armTimestamp=UtcNow();experiment.armQpc=QpcNow();experiment.frequency=QpcFrequency();experiment.producerObservationAvailable=observationAvailable;return experiment;}
bool TickSessionMatches(const TickExperiment& experiment,const TargetInfo& target){return experiment.armed&&target.verified&&target.pid&&experiment.pid==target.pid&&experiment.creationTime==target.creationTime&&experiment.moduleBase==target.base&&experiment.imageSha256==target.image.sha256&&target.image.sha256==ApprovedHash;}

bool ApplyTick100Patch(const TickPatchIo& io,const TargetInfo& target,TickExperiment& experiment,std::string& error){
    experiment.targets.clear();experiment.validationPassed=false;experiment.patchApplied=false;experiment.patchRestored=false;experiment.active=false;experiment.restoreRequired=false;experiment.explicitRestoreAttempted=false;experiment.failureReason.clear();
    if(!TickSessionMatches(experiment,target)||target.mode!=TraceMode::Tick100){error="Verified armed Tick100 process/session identity is required.";experiment.failureReason=error;return false;}
    for(const auto& spec:Specs()){
        TickPatchTarget patch;patch.rva=spec.rva;patch.intendedPatchedBytes=spec.patched;
        if(!ResolveTraceAddress(target.base,target.size,spec.rva,patch.resolvedAddress)){error="Tick patch RVA is outside the live module: TClient.exe+"+Hex(spec.rva);experiment.failureReason=error;return false;}
        if(!io.executable(patch.resolvedAddress,spec.original.size(),error)){error="TClient.exe+"+Hex(spec.rva)+": "+error;experiment.failureReason=error;return false;}
        if(!ReadBytes(io,patch.resolvedAddress,spec.original.size(),patch.originalBytes,error)){error="TClient.exe+"+Hex(spec.rva)+": "+error;experiment.failureReason=error;return false;}
        patch.validationPassed=patch.originalBytes==spec.original;experiment.targets.push_back(std::move(patch));
    }
    experiment.validationPassed=experiment.targets.size()==3&&std::all_of(experiment.targets.begin(),experiment.targets.end(),[](const auto& target){return target.validationPassed;});
    if(!experiment.validationPassed){auto failed=std::find_if(experiment.targets.begin(),experiment.targets.end(),[](const auto& target){return !target.validationPassed;});error="Original instruction validation failed at TClient.exe+"+(failed==experiment.targets.end()?std::string("unknown"):Hex(failed->rva))+"; zero writes performed.";experiment.failureReason=error;return false;}
    for(size_t n=0;n<experiment.targets.size();++n){const auto& spec=Specs()[n];auto& patch=experiment.targets[n];if(!io.write(patch.resolvedAddress+spec.immediateOffset,patch.intendedPatchedBytes.data()+spec.immediateOffset,4,error)){const std::string writeFailure="Patch write failed at TClient.exe+"+Hex(patch.rva)+": "+error;std::string rollbackError;const bool restored=RestoreTargets(io,experiment.targets,rollbackError);SetRestoreTime(experiment);experiment.patchRestored=restored;experiment.restoreRequired=!restored;error=writeFailure+(rollbackError.empty()?"":" Rollback: "+rollbackError);experiment.failureReason=error;return false;}}
    std::string firstFailure;
    for(auto& patch:experiment.targets){std::string current;const bool read=ReadBytes(io,patch.resolvedAddress,patch.intendedPatchedBytes.size(),patch.actualPatchedReadback,current);patch.patchReadbackVerified=read&&patch.actualPatchedReadback==patch.intendedPatchedBytes;if(!patch.patchReadbackVerified&&firstFailure.empty())firstFailure="Patch readback failed at TClient.exe+"+Hex(patch.rva)+(current.empty()?"":": "+current);}
    if(PatchVerifiedCount(experiment)!=3){std::string rollbackError;const bool restored=RestoreTargets(io,experiment.targets,rollbackError);SetRestoreTime(experiment);experiment.patchRestored=restored;experiment.restoreRequired=!restored;error=firstFailure+(rollbackError.empty()?"":" Rollback: "+rollbackError);experiment.failureReason=error;return false;}
    experiment.patchApplied=true;experiment.active=true;experiment.failureReason.clear();error.clear();return true;
}

bool RestoreTick100Patch(const TickPatchIo& io,const TargetInfo& target,TickExperiment& experiment,std::string& error){
    experiment.explicitRestoreAttempted=true;
    if(!TickSessionMatches(experiment,target)){InvalidateTickSession(experiment,"Process/session changed; saved runtime addresses discarded.");error=experiment.failureReason;return false;}
    if(experiment.targets.size()!=3){error="Saved tick patch set is incomplete; refusing reconstructed restoration.";experiment.failureReason=error;return false;}
    error.clear();const bool ok=RestoreTargets(io,experiment.targets,error);SetRestoreTime(experiment);experiment.active=false;experiment.patchRestored=ok;experiment.restoreRequired=!ok;
    if(ok){experiment.failureReason.clear();error.clear();}else experiment.failureReason=error;return ok;
}

void InvalidateTickSession(TickExperiment& experiment,const std::string& reason){experiment.armed=false;experiment.active=false;experiment.restoreRequired=false;experiment.patchApplied=false;experiment.patchRestored=false;experiment.targets.clear();experiment.failureReason=reason;experiment.moduleBase=0;experiment.pid=0;experiment.creationTime=0;experiment.imageSha256.clear();}

void RecordTickProducer(TickPatchExperimentState& experiment,bool readable,uint64_t argument9,int64_t qpc){if(!experiment.active||!experiment.producerObservationAvailable)return;if(!readable){++experiment.producerOtherCount;return;}if(argument9==0x0209){++experiment.producer0209Count;return;}if(argument9!=0x020A){++experiment.producerOtherCount;return;}++experiment.producer020ACount;const double timestamp=experiment.frequency>0?double(qpc-experiment.armQpc)*1000.0/double(experiment.frequency):0;if(!experiment.producer020ATimestampsMs.empty())experiment.producer020ADeltasMs.push_back(timestamp-experiment.producer020ATimestampsMs.back());experiment.producer020ATimestampsMs.push_back(timestamp);}

void WriteTickExperimentJson(std::ostream& out,const TickExperiment& e){out<<"{\"mode\":\"AOE Tick 100ms\",\"pid\":"<<e.pid<<",\"creationTime\":"<<Quote(std::to_string(e.creationTime))<<",\"imageSha256\":"<<Quote(e.imageSha256)<<",\"moduleBase\":"<<Quote(Hex(e.moduleBase))<<",\"armTimestamp\":"<<Quote(e.armTimestamp)<<",\"restoreTimestamp\":"<<Quote(e.restoreTimestamp)<<",\"experimentDurationMs\":"<<Number(e.durationMs)<<",\"originalInterval\":"<<e.originalInterval<<",\"patchedInterval\":"<<e.patchedInterval<<",\"armed\":"<<Bool(e.armed)<<",\"patchApplied\":"<<Bool(e.patchApplied)<<",\"patchRestored\":"<<Bool(e.patchRestored)<<",\"validationPassed\":"<<Bool(e.validationPassed)<<",\"failureReason\":"<<Quote(e.failureReason)<<",\"targets\":[";for(size_t i=0;i<e.targets.size();++i){if(i)out<<',';const auto& t=e.targets[i];out<<"{\"rva\":"<<Quote(Hex(t.rva))<<",\"address\":"<<Quote(Hex(t.resolvedAddress))<<",\"originalBytes\":"<<Quote(BytesHex(t.originalBytes,t.originalBytes.size()))<<",\"intendedPatchedBytes\":"<<Quote(BytesHex(t.intendedPatchedBytes,t.intendedPatchedBytes.size()))<<",\"actualPatchedReadback\":"<<Quote(BytesHex(t.actualPatchedReadback,t.actualPatchedReadback.size()))<<",\"patchReadbackVerified\":"<<Bool(t.patchReadbackVerified)<<",\"actualRestoredReadback\":"<<Quote(BytesHex(t.actualRestoredReadback,t.actualRestoredReadback.size()))<<",\"restoreReadbackVerified\":"<<Bool(t.restoreReadbackVerified)<<'}';}out<<"],\"producerObservationAvailable\":"<<Bool(e.producerObservationAvailable)<<",\"producer0209Count\":"<<e.producer0209Count<<",\"producer020ACount\":"<<e.producer020ACount<<",\"producerOtherCount\":"<<e.producerOtherCount<<",\"producer020ATimestampsMs\":[";for(size_t i=0;i<e.producer020ATimestampsMs.size();++i){if(i)out<<',';out<<Number(e.producer020ATimestampsMs[i]);}out<<"],\"producer020ADeltasMs\":[";for(size_t i=0;i<e.producer020ADeltasMs.size();++i){if(i)out<<',';out<<Number(e.producer020ADeltasMs[i]);}out<<"],\"limitation\":"<<Quote(TickPatchLimitation)<<'}';}

bool ExportTickExperimentJson(const std::filesystem::path& path,const TickPatchExperimentState& experiment,std::string& error){try{if(!experiment.armed){error="No armed AOE Tick experiment is available for export.";return false;}if(!path.parent_path().empty())std::filesystem::create_directories(path.parent_path());std::ofstream output(path,std::ios::binary|std::ios::trunc);if(!output){error="Cannot open JSON destination: "+path.string();return false;}output.imbue(std::locale::classic());WriteTickExperimentJson(output,experiment);output<<'\n';if(!output){error="Failed while writing tick experiment JSON.";return false;}error.clear();return true;}catch(const std::exception& exception){error=std::string("Tick experiment JSON export failed: ")+exception.what();return false;}}

std::string TickExperimentDetails(const TickExperiment& e){
    std::ostringstream out;const size_t patchVerified=PatchVerifiedCount(e),restoreVerified=RestoreVerifiedCount(e);
    if(e.active)out<<"AOE TICK TEST ACTIVE\r\n1000 -> 100\r\nPATCH READBACK: VERIFIED "<<patchVerified<<"/3\r\n";
    else if(e.explicitRestoreAttempted&&e.patchRestored)out<<"RESTORED - VERIFIED\r\nRESTORE READBACK: VERIFIED "<<restoreVerified<<"/3\r\n";
    else if(e.explicitRestoreAttempted)out<<"RESTORE VERIFICATION FAILED\r\nREADBACK VERIFIED "<<restoreVerified<<"/3\r\n";
    else if(e.armed&&!e.failureReason.empty())out<<"PATCH FAILED\r\nREADBACK VERIFIED "<<patchVerified<<"/3\r\n";
    else if(e.armed)out<<"NOT ACTIVE\r\nPatch validation/readback is pending.\r\n";
    else out<<"NOT ARMED\r\n";
    for(const auto& target:e.targets){const bool verified=e.explicitRestoreAttempted?target.restoreReadbackVerified:target.patchReadbackVerified;out<<"TClient.exe+"<<std::hex<<std::uppercase<<target.rva<<" | "<<Hex(target.resolvedAddress)<<"\r\n+"<<target.rva<<"  "<<(verified?"VERIFIED":"FAILED")<<"\r\n";}
    out<<std::dec<<"Duration budget: UNCHANGED\r\nProducer observation: "<<(e.producerObservationAvailable?"AVAILABLE":"UNAVAILABLE")<<"\r\n0x0209: "<<e.producer0209Count<<"  0x020A: "<<e.producer020ACount<<"  other: "<<e.producerOtherCount;
    if(!e.failureReason.empty())out<<"\r\n"<<e.failureReason;out<<"\r\n"<<TickPatchLimitation;return out.str();
}
}

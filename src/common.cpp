#include "tracer.hpp"
#include "aoe_locator.hpp"
#include <TlHelp32.h>
#include <bcrypt.h>
#include <algorithm>
#include <array>
#include <climits>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <iomanip>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <cmath>

namespace aoe {
bool ValidCaptureDuration(double seconds){return std::isfinite(seconds)&&seconds>=MinCaptureSeconds&&seconds<=MaxCaptureSeconds&&std::floor(seconds)==seconds;}
bool ParseCaptureDuration(const std::wstring& text,int& seconds){
    if(text.empty()||text.size()>2)return false;int value=0;
    for(wchar_t c:text){if(c<L'0'||c>L'9')return false;value=value*10+(c-L'0');}
    if(!ValidCaptureDuration(value))return false;seconds=value;return true;
}
namespace {
std::mutex logMutex;
std::filesystem::path logFile;
std::string Escape(const std::string& s){std::ostringstream o;for(unsigned char c:s){switch(c){case '"':o<<"\\\"";break;case '\\':o<<"\\\\";break;case '\n':o<<"\\n";break;case '\r':o<<"\\r";break;case '\t':o<<"\\t";break;default:if(c<32)o<<"\\u"<<std::hex<<std::setw(4)<<std::setfill('0')<<unsigned(c)<<std::dec;else o<<c;}}return o.str();}
struct Algorithm { BCRYPT_ALG_HANDLE h=nullptr;Algorithm(){const NTSTATUS status=BCryptOpenAlgorithmProvider(&h,BCRYPT_SHA256_ALGORITHM,nullptr,0);if(status<0)throw std::runtime_error("BCryptOpenAlgorithmProvider(SHA256) failed; NTSTATUS="+Hex(static_cast<uint32_t>(status)));}~Algorithm(){BCryptCloseAlgorithmProvider(h,0);} };
template<class T> T At(const std::vector<uint8_t>& data,size_t offset){if(offset>data.size()||sizeof(T)>data.size()-offset)throw std::runtime_error("Truncated PE structure");T value;memcpy(&value,data.data()+offset,sizeof(T));return value;}
bool SamePath(const std::wstring& a,const std::wstring& b){return CompareStringOrdinal(a.c_str(),-1,b.c_str(),-1,TRUE)==CSTR_EQUAL;}
uint64_t FileTime(const FILETIME& t){return(uint64_t(t.dwHighDateTime)<<32)|t.dwLowDateTime;}
}
std::string Hex(uint64_t n){std::ostringstream o;o<<"0x"<<std::uppercase<<std::hex<<n;return o.str();}
std::string BytesHex(const std::vector<uint8_t>& data,size_t maxBytes){std::ostringstream o;o<<std::hex<<std::uppercase<<std::setfill('0');for(size_t i=0;i<std::min(data.size(),maxBytes);++i){if(i)o<<' ';o<<std::setw(2)<<unsigned(data[i]);}return o.str();}
std::string Utf8(const std::wstring& s){if(s.empty())return{};int n=WideCharToMultiByte(CP_UTF8,0,s.data(),int(s.size()),nullptr,0,nullptr,nullptr);std::string out(size_t(n),'\0');if(n)WideCharToMultiByte(CP_UTF8,0,s.data(),int(s.size()),out.data(),n,nullptr,nullptr);return out;}
std::wstring Wide(const std::string& s){if(s.empty())return{};int n=MultiByteToWideChar(CP_UTF8,0,s.data(),int(s.size()),nullptr,0);std::wstring out(size_t(n),L'\0');if(n)MultiByteToWideChar(CP_UTF8,0,s.data(),int(s.size()),out.data(),n);return out;}
std::string UtcNow(){SYSTEMTIME t;GetSystemTime(&t);char b[48];snprintf(b,sizeof(b),"%04u-%02u-%02uT%02u:%02u:%02u.%03uZ",t.wYear,t.wMonth,t.wDay,t.wHour,t.wMinute,t.wSecond,t.wMilliseconds);return b;}
std::string LabelName(Label l){return l==Label::Aoe?"AOE":"IDLE";}
int64_t QpcNow(){LARGE_INTEGER n;QueryPerformanceCounter(&n);return n.QuadPart;}
int64_t QpcFrequency(){static int64_t f=[](){LARGE_INTEGER n;QueryPerformanceFrequency(&n);return n.QuadPart;}();return f;}
std::string Sha256(const uint8_t* data,size_t size){static Algorithm a;if(size>ULONG_MAX)throw std::runtime_error("Hash input exceeds bound");if(size&&!data)throw std::runtime_error("Hash input is null with nonzero length");uint8_t empty=0;std::array<uint8_t,32> digest{};const NTSTATUS status=BCryptHash(a.h,nullptr,0,size?const_cast<PUCHAR>(data):&empty,ULONG(size),digest.data(),ULONG(digest.size()));if(status<0)throw std::runtime_error("BCryptHash failed; NTSTATUS="+Hex(static_cast<uint32_t>(status)));std::ostringstream o;o<<std::hex<<std::uppercase<<std::setfill('0');for(auto b:digest)o<<std::setw(2)<<unsigned(b);return o.str();}
void InitLog(const std::filesystem::path& root){std::lock_guard l(logMutex);std::filesystem::create_directories(root/"logs");logFile=root/"logs"/"aoe-tracer.jsonl";}
void Log(const std::string& event,const std::string& detail){std::lock_guard l(logMutex);if(logFile.empty())return;std::ofstream f(logFile,std::ios::app|std::ios::binary);if(f)f<<"{\"utc\":\""<<UtcNow()<<"\",\"event\":\""<<Escape(event)<<"\",\"detail\":\""<<Escape(detail)<<"\"}\n";}
std::string WinError(const std::string& operation,uint32_t code){wchar_t* p=nullptr;DWORD n=FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER|FORMAT_MESSAGE_FROM_SYSTEM|FORMAT_MESSAGE_IGNORE_INSERTS,nullptr,code,0,reinterpret_cast<wchar_t*>(&p),0,nullptr);std::string s=n?Utf8(std::wstring(p,n)):"Unknown error";if(p)LocalFree(p);while(!s.empty()&&(s.back()=='\r'||s.back()=='\n'||s.back()==' '))s.pop_back();return operation+" failed; Win32="+std::to_string(code)+" ("+s+")";}

bool InspectImage(const std::filesystem::path& path,uint64_t rva,ImageInfo& out,std::string& error){
    out={};
    try{ImageInfo info;info.path=path.wstring();info.fileSize=std::filesystem::file_size(path);if(info.fileSize<sizeof(IMAGE_DOS_HEADER)||info.fileSize>512ull*1024*1024)throw std::runtime_error("PE file size is outside the supported bounds");std::ifstream f(path,std::ios::binary);std::vector<uint8_t> data(size_t(info.fileSize));if(!f.read(reinterpret_cast<char*>(data.data()),std::streamsize(data.size())))throw std::runtime_error("Cannot read target image file");info.sha256=Sha256(data.data(),data.size());
        auto dos=At<IMAGE_DOS_HEADER>(data,0);if(dos.e_magic!=IMAGE_DOS_SIGNATURE||dos.e_lfanew<0)throw std::runtime_error("Invalid PE DOS header");size_t ntpos=size_t(dos.e_lfanew);auto nt=At<IMAGE_NT_HEADERS64>(data,ntpos);if(nt.Signature!=IMAGE_NT_SIGNATURE||nt.OptionalHeader.Magic!=IMAGE_NT_OPTIONAL_HDR64_MAGIC||nt.FileHeader.SizeOfOptionalHeader<sizeof(IMAGE_OPTIONAL_HEADER64))throw std::runtime_error("Image is not PE32+ (x64)");info.amd64=nt.FileHeader.Machine==IMAGE_FILE_MACHINE_AMD64;info.timestamp=nt.FileHeader.TimeDateStamp;info.imageSize=nt.OptionalHeader.SizeOfImage;if(!info.amd64||rva>=info.imageSize||nt.FileHeader.NumberOfSections>128)throw std::runtime_error("Invalid AMD64 PE/RVA/section count");
        std::vector<IMAGE_SECTION_HEADER> sections;size_t table=ntpos+4+sizeof(IMAGE_FILE_HEADER)+nt.FileHeader.SizeOfOptionalHeader;for(unsigned i=0;i<nt.FileHeader.NumberOfSections;++i){auto s=At<IMAGE_SECTION_HEADER>(data,table+i*sizeof(IMAGE_SECTION_HEADER));sections.push_back(s);if(s.Characteristics&IMAGE_SCN_MEM_EXECUTE){uint32_t size=std::max(s.Misc.VirtualSize,s.SizeOfRawData);if(uint64_t(s.VirtualAddress)+size>info.imageSize)throw std::runtime_error("Executable section exceeds SizeOfImage");info.executableRanges.push_back({s.VirtualAddress,size});if(rva>=s.VirtualAddress&&rva-s.VirtualAddress<size)info.traceExecutable=true;}}
        auto fileOffset=[&](uint64_t address,size_t size)->size_t{for(auto& s:sections)if(address>=s.VirtualAddress&&address-s.VirtualAddress<=s.SizeOfRawData&&size<=s.SizeOfRawData-(address-s.VirtualAddress)){uint64_t offset=uint64_t(s.PointerToRawData)+address-s.VirtualAddress;if(offset>data.size()||size>data.size()-offset)break;return size_t(offset);}throw std::runtime_error("RVA does not map to complete file-backed bytes");};
        if(!info.traceExecutable)throw std::runtime_error("Requested RVA is not in an executable section");size_t entry=fileOffset(rva,32);info.entryBytes.assign(data.begin()+entry,data.begin()+entry+32);
        if(nt.OptionalHeader.NumberOfRvaAndSizes>IMAGE_DIRECTORY_ENTRY_EXCEPTION){auto dir=nt.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION];if(dir.Size&&dir.Size<=16*1024*1024){size_t offset=fileOffset(dir.VirtualAddress,dir.Size);for(size_t i=0;i+sizeof(RUNTIME_FUNCTION)<=dir.Size;i+=sizeof(RUNTIME_FUNCTION)){auto rf=At<RUNTIME_FUNCTION>(data,offset+i);if(rf.BeginAddress==rva&&rf.EndAddress>rva&&rf.EndAddress<=info.imageSize){info.functionEntry=true;break;}}}}
        out=std::move(info);error.clear();return true;
    }catch(const std::exception& e){error=e.what();return false;}
}
std::vector<uint32_t> FindProcesses(const std::wstring& name){
    std::vector<uint32_t> pids;HANDLE snap=CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS,0);if(snap==INVALID_HANDLE_VALUE)return pids;
    PROCESSENTRY32W e{};e.dwSize=sizeof(e);DWORD enumerationError=ERROR_SUCCESS;
    if(Process32FirstW(snap,&e)){
        do{if(SamePath(e.szExeFile,name))pids.push_back(e.th32ProcessID);}while(Process32NextW(snap,&e));
        const DWORD code=GetLastError();if(code!=ERROR_NO_MORE_FILES)enumerationError=code?code:ERROR_GEN_FAILURE;
    }else{const DWORD code=GetLastError();if(code!=ERROR_NO_MORE_FILES)enumerationError=code?code:ERROR_GEN_FAILURE;}
    CloseHandle(snap);if(enumerationError)pids.clear();SetLastError(enumerationError);return pids;
}
bool SafeRead(HANDLE process,uint64_t address,void* destination,size_t size,uint32_t& error){
    error=0;if(size==0)return true;if(!process||!destination||address<0x10000||address>0x00007FFFFFFFFFFFull||size>0x0000800000000000ull-address){error=ERROR_INVALID_ADDRESS;return false;}uint64_t end=address+size,cursor=address;
    while(cursor<end){MEMORY_BASIC_INFORMATION m{};const SIZE_T queried=VirtualQueryEx(process,reinterpret_cast<void*>(cursor),&m,sizeof(m));if(queried!=sizeof(m)){error=queried?ERROR_BAD_LENGTH:GetLastError();if(!error)error=ERROR_INVALID_ADDRESS;return false;}DWORD p=m.Protect&0xff;bool readable=p==PAGE_READONLY||p==PAGE_READWRITE||p==PAGE_WRITECOPY||p==PAGE_EXECUTE_READ||p==PAGE_EXECUTE_READWRITE||p==PAGE_EXECUTE_WRITECOPY;if(m.State!=MEM_COMMIT||(m.Protect&PAGE_GUARD)||!readable){error=ERROR_NOACCESS;return false;}uint64_t base=reinterpret_cast<uint64_t>(m.BaseAddress);if(!m.RegionSize||base>cursor||base>UINT64_MAX-m.RegionSize||base+m.RegionSize<=cursor){error=ERROR_INVALID_ADDRESS;return false;}cursor=std::min(end,base+m.RegionSize);}
    SIZE_T n=0;const BOOL read=ReadProcessMemory(process,reinterpret_cast<const void*>(address),destination,size,&n);if(!read){error=GetLastError();if(!error)error=ERROR_PARTIAL_COPY;return false;}if(n!=size){error=ERROR_PARTIAL_COPY;return false;}return true;
}

// Shared production/fixture verification. Query/read permissions only; debugging
// access is granted independently by DebugActiveProcess after the UI request.
bool VerifyTarget(uint32_t pid,const std::filesystem::path& path,uint64_t rva,bool fixture,bool allowWrite,const BuildProfile* profile,TargetInfo& target,HANDLE& process,std::string& error){
    process=nullptr;target={};TargetInfo info;info.pid=pid;info.traceRva=rva;if(!InspectImage(path,rva,info.image,error)){target=info;return false;}
    if(!fixture&&(!profile||profile->targetSha256!=info.image.sha256||ProfileTraceRva(*profile,TraceMode::Initial2x)==0)){target=info;error="Target SHA-256 has no matching validated runtime profile. Attachment refused.";return false;}
    AoeLocatorResult aoeEvidence;
    if(!fixture&&profile->runtime.aoeLocatorRequired&&(!LocateAoeImage(path,aoeEvidence,error)||!ValidateAoeProfile(*profile,aoeEvidence,error))){target=info;return false;}
    DWORD access=PROCESS_QUERY_INFORMATION|PROCESS_VM_READ;if(allowWrite)access|=PROCESS_VM_WRITE|PROCESS_VM_OPERATION;
    HANDLE h=OpenProcess(access,FALSE,pid);if(!h){error=WinError(allowWrite?"OpenProcess(query/read/write) PID "+std::to_string(pid):"OpenProcess(query/read) PID "+std::to_string(pid),GetLastError());target=info;return false;}
    auto fail=[&](const std::string& message){error=message;target=info;CloseHandle(h);return false;};
    wchar_t image[32768];DWORD length=32768;if(!QueryFullProcessImageNameW(h,0,image,&length))return fail(WinError("QueryFullProcessImageNameW",GetLastError()));if(!SamePath(std::wstring(image,length),path.wstring()))return fail("Running process image path does not match the expected target.");
    USHORT machine=0,native=0;if(!IsWow64Process2(h,&machine,&native))return fail(WinError("IsWow64Process2",GetLastError()));if(machine!=IMAGE_FILE_MACHINE_UNKNOWN||native!=IMAGE_FILE_MACHINE_AMD64)return fail("Target is not a verified native AMD64 process.");FILETIME created{},exited{},kernel{},user{};if(!GetProcessTimes(h,&created,&exited,&kernel,&user))return fail(WinError("GetProcessTimes",GetLastError()));info.creationTime=FileTime(created);
    HANDLE snap=INVALID_HANDLE_VALUE;DWORD snapshotError=ERROR_SUCCESS;for(int i=0;i<3;++i){snap=CreateToolhelp32Snapshot(TH32CS_SNAPMODULE|TH32CS_SNAPMODULE32,pid);if(snap!=INVALID_HANDLE_VALUE)break;snapshotError=GetLastError();if(snapshotError!=ERROR_BAD_LENGTH)break;}if(snap==INVALID_HANDLE_VALUE)return fail(WinError("CreateToolhelp32Snapshot(modules)",snapshotError));
    MODULEENTRY32W me{};me.dwSize=sizeof(me);unsigned matches=0;DWORD enumerationError=ERROR_SUCCESS;
    if(Module32FirstW(snap,&me)){
        do{if(SamePath(me.szExePath,path.wstring())){++matches;info.base=reinterpret_cast<uint64_t>(me.modBaseAddr);info.size=me.modBaseSize;}}while(Module32NextW(snap,&me));
        const DWORD code=GetLastError();if(code!=ERROR_NO_MORE_FILES)enumerationError=code?code:ERROR_GEN_FAILURE;
    }else enumerationError=GetLastError();
    CloseHandle(snap);if(enumerationError)return fail(WinError("Enumerate live modules",enumerationError));if(matches!=1||info.size!=info.image.imageSize||info.base>UINT64_MAX-info.size)return fail("Live main module does not match disk PE SizeOfImage/path.");
    uint32_t readError=0;IMAGE_DOS_HEADER dos{};IMAGE_NT_HEADERS64 nt{};if(!SafeRead(h,info.base,&dos,sizeof(dos),readError)||dos.e_magic!=IMAGE_DOS_SIGNATURE||dos.e_lfanew<0||uint64_t(dos.e_lfanew)+sizeof(nt)>info.size||!SafeRead(h,info.base+dos.e_lfanew,&nt,sizeof(nt),readError))return fail(WinError("Read live PE headers",readError?readError:ERROR_BAD_EXE_FORMAT));if(nt.Signature!=IMAGE_NT_SIGNATURE||nt.FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64||nt.OptionalHeader.Magic!=IMAGE_NT_OPTIONAL_HDR64_MAGIC||nt.FileHeader.SizeOfOptionalHeader<sizeof(IMAGE_OPTIONAL_HEADER64)||nt.FileHeader.TimeDateStamp!=info.image.timestamp||nt.OptionalHeader.SizeOfImage!=info.image.imageSize)return fail("Live PE identity differs from disk.");
    if(!ResolveTraceAddress(info.base,info.size,rva,info.traceAddress))return fail("Resolved trace RVA is outside the live module.");std::vector<uint8_t> bytes(info.image.entryBytes.size());if(!SafeRead(h,info.traceAddress,bytes.data(),bytes.size(),readError))return fail(WinError("Read live trace entry",readError));if(bytes!=info.image.entryBytes)return fail("Live trace entry differs from the verified disk bytes (possible existing breakpoint/patch). Attachment refused.");MEMORY_BASIC_INFORMATION m{};if(!VirtualQueryEx(h,reinterpret_cast<void*>(info.traceAddress),&m,sizeof(m)))return fail(WinError("VirtualQueryEx(trace entry)",GetLastError()));DWORD p=m.Protect&0xff;if(m.State!=MEM_COMMIT||(m.Protect&PAGE_GUARD)||(p!=PAGE_EXECUTE&&p!=PAGE_EXECUTE_READ&&p!=PAGE_EXECUTE_READWRITE&&p!=PAGE_EXECUTE_WRITECOPY))return fail("Live trace entry is not committed executable memory.");
    if(profile)info.runtime=profile->runtime;info.verified=true;
    if(!fixture&&profile->runtime.aoeLocatorRequired&&!ValidateAoeLive(h,info,aoeEvidence,error))return fail(error);
    target=std::move(info);process=h;error.clear();return true;
}
bool DiscoverTarget(TargetInfo& info,std::string& error){
    info={};auto pids=FindProcesses();const DWORD enumerationError=GetLastError();if(enumerationError){error=WinError("Enumerate processes",enumerationError);return false;}if(pids.empty()){std::string imageError;InspectImage(TargetPath,SendRva,info.image,imageError);error="TClient.exe is not running.";if(!imageError.empty())error+=" Disk inspection: "+imageError;return false;}error="DiscoverTarget requires SHA-selected profile validation.";return false;
}
}

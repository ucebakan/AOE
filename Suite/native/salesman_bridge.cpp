#include <Windows.h>
#include <bcrypt.h>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <initializer_list>

// Same POD layout as SalesmanRequest.cs. No pointers are persisted as profiles.
struct Request {
    uint32_t version,status,error,pid;
    uint64_t created,base,owner,player,session,shop,deadline;
    uint32_t root,ownerVtable,playerVtable,shopVtable,playerOffset,sessionOffset,shopOffset,cashOffset;
    uint32_t sender,senderSize,finder,finderSize,npcTypeOffset;
    unsigned char senderBytes[128],finderBytes[128];
};
static_assert(sizeof(Request)==384);
static UINT Message(){return RegisterWindowMessageW(L"4UnityTools.Salesman.Open.v1");}
static void MapName(wchar_t* name,DWORD pid,DWORD tid,uint64_t token){swprintf_s(name,128,L"Local\\4UnitySalesman_%lu_%lu_%016llx",pid,tid,token);}
static uint64_t Created(HANDLE p){FILETIME c{},e{},k{},u{};if(!GetProcessTimes(p,&c,&e,&k,&u))return 0;return uint64_t(c.dwHighDateTime)<<32|c.dwLowDateTime;}
static thread_local bool fixture=false;
static thread_local uint32_t fixtureCalls=0,fixtureNpc=0,fixtureThread=0;
alignas(4096) static unsigned char fixturePage[4096]{};
static constexpr unsigned char fixtureOriginal[]={0x45,0x33,0xc0,0xb2,0x0b,0x48,0x8b,0xcb,0xe8,0xb0,0,0,0,0x45,0x33,0xc0,0xb2,0x0c,0x48,0x8b,0xcb,0xe8,0xa3,0,0,0};
extern "C" __declspec(dllexport) int SalesmanFixtureLayout(uint64_t* base,uint32_t* rva){
    if(!base || !rva)return 0;HMODULE module=nullptr;
    if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,reinterpret_cast<LPCWSTR>(&SalesmanFixtureLayout),&module))return 0;
    *base=uint64_t(module);*rva=uint32_t(uint64_t(fixturePage+128)-*base);return 1;
}
static bool InitFixturePage(){memcpy(fixturePage+128,fixtureOriginal,sizeof(fixtureOriginal));DWORD old=0;return VirtualProtect(fixturePage,sizeof(fixturePage),PAGE_EXECUTE_READ,&old)!=0;}
extern "C" __declspec(dllexport) int VerifySalesmanFixture(){MEMORY_BASIC_INFORMATION p{};return memcmp(fixturePage+128,fixtureOriginal,sizeof(fixtureOriginal))==0 && VirtualQuery(fixturePage,&p,sizeof(p)) && p.Protect==PAGE_EXECUTE_READ?1:0;}
static LRESULT CALLBACK Hook(int code,WPARAM w,LPARAM l);
static void Invoke(Request* r){
    __try {
        if(r->version!=1 || r->pid!=GetCurrentProcessId() || r->created!=Created(GetCurrentProcess()) || r->deadline<GetTickCount64()) {r->error=1;return;}
        if(fixture){fixtureCalls++;fixtureNpc=22631;fixtureThread=GetCurrentThreadId();r->status=1;return;}
        auto base=reinterpret_cast<unsigned char*>(GetModuleHandleW(nullptr));
        if(uint64_t(base)!=r->base || r->senderSize<40 || r->senderSize>128 || r->finderSize<40 || r->finderSize>128){r->error=2;return;}
        auto dos=reinterpret_cast<IMAGE_DOS_HEADER*>(base);auto nt=reinterpret_cast<IMAGE_NT_HEADERS64*>(base+dos->e_lfanew);
        if(dos->e_magic!=IMAGE_DOS_SIGNATURE || nt->Signature!=IMAGE_NT_SIGNATURE || nt->FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64){r->error=3;return;}
        for(auto rva:{r->root,r->ownerVtable,r->playerVtable,r->shopVtable,r->sender,r->finder})if(rva>=nt->OptionalHeader.SizeOfImage-128){r->error=4;return;}
        if(memcmp(base+r->sender,r->senderBytes,r->senderSize) || memcmp(base+r->finder,r->finderBytes,r->finderSize)){r->error=5;return;}
        auto owner=*reinterpret_cast<uint64_t*>(base+r->root);
        if(owner!=r->owner || *reinterpret_cast<uint64_t*>(owner)!=r->base+r->ownerVtable ||
           *reinterpret_cast<uint64_t*>(owner+r->playerOffset)!=r->player || *reinterpret_cast<uint64_t*>(r->player)!=r->base+r->playerVtable ||
           *reinterpret_cast<uint64_t*>(owner+r->sessionOffset)!=r->session || *reinterpret_cast<uint64_t*>(owner+r->shopOffset)!=r->shop ||
           *reinterpret_cast<uint64_t*>(r->shop)!=r->base+r->shopVtable || *reinterpret_cast<uint64_t*>(r->shop+r->cashOffset)!=0){r->error=6;return;}
        using Find=void*(__fastcall*)(uint16_t);using Send=void(__fastcall*)(void*,uint16_t);
        auto npc=static_cast<unsigned char*>(reinterpret_cast<Find>(base+r->finder)(22631));
        if(!npc || r->npcTypeOffset>0x10000 || npc[r->npcTypeOffset]!=2){r->error=7;return;}
        // Windows may remove the hook while a timed-out callback is still running.
        // Pin this tiny bridge before executing game code; it owns no worker/thread.
        HMODULE pin=nullptr;if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(&Hook),&pin)){r->error=8;return;}
        reinterpret_cast<Send>(base+r->sender)(reinterpret_cast<void*>(r->session),22631);
        r->status=1;
    } __except(EXCEPTION_EXECUTE_HANDLER){r->error=GetExceptionCode();r->status=2;}
}
static LRESULT CALLBACK Hook(int code,WPARAM w,LPARAM l){
    if(code>=0 && l){
        auto msg=reinterpret_cast<CWPSTRUCT*>(l);
        if(msg->message==Message()){
            wchar_t name[128]{};MapName(name,GetCurrentProcessId(),GetCurrentThreadId(),uint64_t(msg->wParam));
            HANDLE mapping=OpenFileMappingW(FILE_MAP_READ|FILE_MAP_WRITE,FALSE,name);
            if(mapping){auto r=static_cast<Request*>(MapViewOfFile(mapping,FILE_MAP_READ|FILE_MAP_WRITE,0,0,sizeof(Request)));
                if(r){if(r->status==0)Invoke(r);UnmapViewOfFile(r);}CloseHandle(mapping);}
        }
    }
    return CallNextHookEx(nullptr,code,w,l);
}
extern "C" __declspec(dllexport) int OpenSalesman(HWND window,const Request* input,uint32_t* error){
    if(error)*error=0;DWORD pid=0,tid=GetWindowThreadProcessId(window,&pid);
    if(!input || !error || !tid || pid!=input->pid || input->version!=1){if(error)*error=ERROR_INVALID_PARAMETER;return 0;}
    uint64_t token=0;if(BCryptGenRandom(nullptr,reinterpret_cast<PUCHAR>(&token),sizeof(token),BCRYPT_USE_SYSTEM_PREFERRED_RNG)<0){*error=ERROR_GEN_FAILURE;return 0;}
    wchar_t name[128]{};MapName(name,pid,tid,token);
    HANDLE mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(Request),name);
    if(!mapping){*error=GetLastError();return 0;}
    auto request=static_cast<Request*>(MapViewOfFile(mapping,FILE_MAP_READ|FILE_MAP_WRITE,0,0,sizeof(Request)));
    if(!request){*error=GetLastError();CloseHandle(mapping);return 0;}
    *request=*input;request->status=request->error=0;
    HMODULE module=nullptr;GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,reinterpret_cast<LPCWSTR>(&Hook),&module);
    HHOOK hook=SetWindowsHookExW(WH_CALLWNDPROC,Hook,module,tid);
    int ok=0;
    if(!hook)*error=GetLastError();
    else {
        DWORD_PTR reply=0;
        if(!SendMessageTimeoutW(window,Message(),WPARAM(token),0,SMTO_ABORTIFHUNG|SMTO_BLOCK,3000,&reply))*error=GetLastError()?GetLastError():ERROR_TIMEOUT;
        else {ok=request->status==1;*error=request->error;if(!ok && !*error)*error=ERROR_INVALID_FUNCTION;}
        if(!UnhookWindowsHookEx(hook)){ok=0;*error=GetLastError();}
    }
    UnmapViewOfFile(request);CloseHandle(mapping);return ok;
}
static LRESULT CALLBACK TestWnd(HWND h,UINT m,WPARAM w,LPARAM l){if(m==WM_CLOSE){DestroyWindow(h);PostQuitMessage(0);return 0;}return DefWindowProcW(h,m,w,l);}
extern "C" __declspec(dllexport) HWND BeginSalesmanFixture(){
    if(!InitFixturePage())return nullptr;
    WNDCLASSW c{};c.lpfnWndProc=TestWnd;c.hInstance=GetModuleHandleW(nullptr);c.lpszClassName=L"4UnitySalesmanBridgeFixture";RegisterClassW(&c);
    fixture=true;fixtureCalls=fixtureNpc=fixtureThread=0;
    return CreateWindowExW(0,c.lpszClassName,L"",0,0,0,0,0,HWND_MESSAGE,nullptr,c.hInstance,nullptr);
}
extern "C" __declspec(dllexport) int ReadSalesmanFixture(uint32_t* calls,uint32_t* npc,uint32_t* thread){
    if(!calls || !npc || !thread)return 0;*calls=fixtureCalls;*npc=fixtureNpc;*thread=fixtureThread;return 1;
}
extern "C" __declspec(dllexport) int TestSalesmanBridge(){
    WNDCLASSW c{};c.lpfnWndProc=TestWnd;c.hInstance=GetModuleHandleW(nullptr);c.lpszClassName=L"4UnitySalesmanBridgeFixture";
    RegisterClassW(&c);HWND h=CreateWindowExW(0,c.lpszClassName,L"",0,0,0,0,0,HWND_MESSAGE,nullptr,c.hInstance,nullptr);if(!h)return 0;
    fixture=true;fixtureCalls=fixtureNpc=fixtureThread=0;Request r{};r.version=1;r.pid=GetCurrentProcessId();r.created=Created(GetCurrentProcess());r.deadline=GetTickCount64()+5000;uint32_t error=0;
    bool ok=OpenSalesman(h,&r,&error)==1 && fixtureCalls==1 && fixtureNpc==22631 && fixtureThread==GetCurrentThreadId();
    r.created++;ok=ok && OpenSalesman(h,&r,&error)==0 && fixtureCalls==1;
    r.created--;r.deadline=1;ok=ok && OpenSalesman(h,&r,&error)==0 && fixtureCalls==1;
    r.deadline=GetTickCount64()+5000;r.pid++;ok=ok && OpenSalesman(h,&r,&error)==0 && fixtureCalls==1;
    fixture=false;DestroyWindow(h);return ok?1:0;
}

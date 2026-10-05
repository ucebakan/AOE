#include "collection_shared.hpp"
#include <bcrypt.h>
#include <initializer_list>
#include <vector>
static thread_local bool fixture=false;
static thread_local unsigned fixtureCalls=0,fixtureCancelAt=0;
static LRESULT CALLBACK Hook(int,WPARAM,LPARAM);
static SRWLOCK stateLock=SRWLOCK_INIT;
static Batch* active=nullptr;
static HANDLE mapping=nullptr,targetProcess=nullptr;
static HHOOK installed=nullptr;
static bool busy=false;
static bool cancelled=false;
static uint32_t Guard(const Request& r,const Layout& l){
    try{return Validate(r,l,[](uint64_t a,void* b,size_t n){SIZE_T got=0;return ReadProcessMemory(GetCurrentProcess(),reinterpret_cast<void*>(a),b,n,&got)&&got==n;});}catch(...){return 24;}
}
static void Invoke(Request* r,const Layout* layout){
    __try {
        if(InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&r->status),3,0)!=0)return;
        if(r->version!=1||r->pid!=GetCurrentProcessId()||r->created!=Created(GetCurrentProcess())||r->deadline<GetTickCount64()){r->error=1;r->status=2;return;}
        if(fixture){if(r->targetId>=UINT32_MAX-1){r->error=r->targetId==UINT32_MAX?14:12;r->status=2;return;}fixtureCalls++;r->status=1;return;}
        if(r->base!=uint64_t(GetModuleHandleW(nullptr))){r->error=2;r->status=2;return;}
        uint32_t error=Guard(*r,*layout);if(error){r->error=error;r->status=2;return;}
        using Send=void(__fastcall*)(void*,void*);
        reinterpret_cast<Send>(r->base+layout->sender.rva)(reinterpret_cast<void*>(r->owner),reinterpret_cast<void*>(r->target));
        r->status=1;
    }__except(EXCEPTION_EXECUTE_HANDLER){r->error=GetExceptionCode();r->status=2;}
}
static void InvokeBatch(Batch* batch){
    if(InterlockedCompareExchange(reinterpret_cast<volatile LONG*>(&batch->status),3,0)!=0)return;
    if(batch->version!=1||!batch->count||batch->count>MaxBatch||batch->reserved){batch->error=10;batch->status=2;return;}
    const auto& first=batch->requests[0];
    for(uint32_t i=0;i<batch->count;i++){
        const auto& r=batch->requests[i];
        if(r.version!=1||r.mode!=1||r.status||r.error||r.pid!=first.pid||r.created!=first.created||r.base!=first.base||r.owner!=first.owner||r.player!=first.player||r.session!=first.session||r.head!=first.head){batch->error=12;batch->status=2;return;}
        for(uint32_t j=0;j<i;j++)if(r.targetId==batch->requests[j].targetId){batch->error=13;batch->status=2;return;}
    }
    HANDLE host=OpenProcess(SYNCHRONIZE|PROCESS_QUERY_LIMITED_INFORMATION,FALSE,batch->hostPid);
    if(!host||Created(host)!=batch->hostCreated){if(host)CloseHandle(host);batch->error=26;batch->status=2;return;}
    for(uint32_t i=0;i<batch->count;i++){
        if(batch->cancel||WaitForSingleObject(host,0)!=WAIT_TIMEOUT){batch->status=5;CloseHandle(host);return;}
        auto& r=batch->requests[i];Invoke(&r,&batch->layout);batch->completed=i+1;
        if(fixture&&fixtureCancelAt&&fixtureCalls>=fixtureCancelAt)InterlockedExchange(reinterpret_cast<volatile LONG*>(&batch->cancel),1);
        if(r.status==1)continue;
        if(r.status==2&&(r.error==13||r.error==14||r.error==24||r.error==25))continue;
        batch->error=r.error;batch->status=2;CloseHandle(host);return;
    }
    CloseHandle(host);batch->status=1;
}
static LRESULT CALLBACK Hook(int code,WPARAM w,LPARAM l){
    if(code>=0&&l){auto msg=reinterpret_cast<CWPSTRUCT*>(l);if(msg->message==BatchMessage()){
        HMODULE pin=nullptr;
        if(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(&Hook),&pin)){
            wchar_t name[128]{};BatchMapName(name,GetCurrentProcessId(),GetCurrentThreadId(),uint64_t(msg->wParam));
            HANDLE map=OpenFileMappingW(FILE_MAP_READ|FILE_MAP_WRITE,FALSE,name);
            if(map){auto r=static_cast<Batch*>(MapViewOfFile(map,FILE_MAP_READ|FILE_MAP_WRITE,0,0,sizeof(Batch)));if(r){InvokeBatch(r);UnmapViewOfFile(r);}CloseHandle(map);}
        }
    }}return CallNextHookEx(nullptr,code,w,l);
}
// Caller must hold stateLock. A running callback retains shared memory and the
// automation mutex in managed code until this returns true.
static bool CleanLocked(){
    if(!active)return true;
    bool gone=targetProcess&&WaitForSingleObject(targetProcess,0)==WAIT_OBJECT_0;
    if(active->status==3&&!gone)return false;
    if(installed){if(!gone&&!UnhookWindowsHookEx(installed))return false;installed=nullptr;}
    UnmapViewOfFile(active);active=nullptr;if(mapping)CloseHandle(mapping);mapping=nullptr;
    if(targetProcess)CloseHandle(targetProcess);targetProcess=nullptr;return true;
}
extern "C" __declspec(dllexport) int CancelCollection(){
    AcquireSRWLockExclusive(&stateLock);cancelled=true;if(active)InterlockedExchange(reinterpret_cast<volatile LONG*>(&active->cancel),1);ReleaseSRWLockExclusive(&stateLock);return 1;
}
extern "C" __declspec(dllexport) int BeginCollection(){
    AcquireSRWLockExclusive(&stateLock);bool clean=!busy&&CleanLocked();if(clean)cancelled=false;ReleaseSRWLockExclusive(&stateLock);return clean?1:0;
}
extern "C" __declspec(dllexport) int CleanupCollection(){
    AcquireSRWLockExclusive(&stateLock);bool clean=!busy&&CleanLocked();ReleaseSRWLockExclusive(&stateLock);return clean?1:0;
}
extern "C" __declspec(dllexport) int CollectBatch(HWND window,Batch* batch){
    DWORD pid=0,tid=GetWindowThreadProcessId(window,&pid);
    if(!batch||!tid||!batch->count||batch->count>MaxBatch||pid!=batch->requests[0].pid)return 0;
    AcquireSRWLockExclusive(&stateLock);
    if(busy||!CleanLocked()){ReleaseSRWLockExclusive(&stateLock);return -1;}busy=true;
    if(cancelled){busy=false;batch->status=5;ReleaseSRWLockExclusive(&stateLock);return 1;}
    uint64_t token=0;wchar_t name[128]{};
    bool initialized=BCryptGenRandom(nullptr,reinterpret_cast<PUCHAR>(&token),sizeof(token),BCRYPT_USE_SYSTEM_PREFERRED_RNG)>=0;
    if(initialized){BatchMapName(name,pid,tid,token);mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(Batch),name);initialized=mapping!=nullptr;}
    if(initialized){active=static_cast<Batch*>(MapViewOfFile(mapping,FILE_MAP_READ|FILE_MAP_WRITE,0,0,sizeof(Batch)));initialized=active!=nullptr;}
    HMODULE module=nullptr;
    if(initialized){
        *active=*batch;active->status=active->error=active->completed=0;
        targetProcess=OpenProcess(SYNCHRONIZE|PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid);
        initialized=targetProcess&&Created(targetProcess)==batch->requests[0].created&&GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(&Hook),&module);
        if(initialized){installed=SetWindowsHookExW(WH_CALLWNDPROC,Hook,module,tid);initialized=installed!=nullptr;}
    }
    if(!initialized){batch->error=GetLastError();batch->status=2;busy=false;if(active)CleanLocked();else{if(mapping)CloseHandle(mapping);mapping=nullptr;}ReleaseSRWLockExclusive(&stateLock);return 0;}
    ReleaseSRWLockExclusive(&stateLock);
    DWORD_PTR reply=0;bool delivered=SendMessageTimeoutW(window,BatchMessage(),WPARAM(token),0,SMTO_ABORTIFHUNG|SMTO_BLOCK,3000,&reply)!=0;
    AcquireSRWLockExclusive(&stateLock);
    if(!delivered)InterlockedExchange(reinterpret_cast<volatile LONG*>(&active->cancel),1);
    *batch=*active;bool success=delivered&&(batch->status==1||batch->status==5);busy=false;
    bool clean=CleanLocked();if(!clean){batch->status=4;batch->error=27;}
    else if(!delivered){batch->status=4;batch->error=28;}
    ReleaseSRWLockExclusive(&stateLock);return !clean?-1:success?1:0;
}
static LRESULT CALLBACK Wnd(HWND h,UINT m,WPARAM w,LPARAM l){return DefWindowProcW(h,m,w,l);}
extern "C" __declspec(dllexport) int TestBridge(){
    WNDCLASSW c{};c.hInstance=GetModuleHandleW(nullptr);c.lpfnWndProc=Wnd;c.lpszClassName=L"SuiteCollectionFixture";RegisterClassW(&c);
    HWND window=CreateWindowExW(0,c.lpszClassName,L"",0,0,0,0,0,HWND_MESSAGE,nullptr,c.hInstance,nullptr);if(!window)return 0;
    fixture=true;fixtureCalls=fixtureCancelAt=0;
    BeginCollection();
    auto make=[](uint32_t count){Batch b{};b.version=1;b.count=count;b.hostPid=GetCurrentProcessId();b.hostCreated=Created(GetCurrentProcess());for(uint32_t i=0;i<count&&i<MaxBatch;i++){auto& r=b.requests[i];r.version=1;r.pid=b.hostPid;r.created=b.hostCreated;r.deadline=GetTickCount64()+5000;r.mode=1;r.targetId=i+1;}return b;};
    Batch b=make(64);bool ok=CollectBatch(window,&b)==1&&fixtureCalls==64&&b.completed==64;InvokeBatch(&b);ok=ok&&fixtureCalls==64;
    fixtureCalls=0;b=make(3);b.requests[1].targetId=UINT32_MAX;ok=ok&&CollectBatch(window,&b)==1&&fixtureCalls==2&&b.completed==3;
    fixtureCalls=0;b=make(3);b.requests[1].targetId=UINT32_MAX-1;ok=ok&&CollectBatch(window,&b)==0&&fixtureCalls==1&&b.completed==2&&!b.requests[2].status;
    fixtureCalls=0;b=make(3);b.requests[2].created++;ok=ok&&CollectBatch(window,&b)==0&&!fixtureCalls;
    b=make(3);b.requests[1].targetId=1;ok=ok&&CollectBatch(window,&b)==0&&!fixtureCalls;
    b=make(3);b.hostCreated++;ok=ok&&CollectBatch(window,&b)==0&&!fixtureCalls;
    b=make(3);b.cancel=1;ok=ok&&CollectBatch(window,&b)==1&&b.status==5&&!fixtureCalls;
    b=make(64);fixtureCancelAt=1;ok=ok&&CollectBatch(window,&b)==1&&b.status==5&&fixtureCalls==1&&b.completed==1;
    fixtureCancelAt=0;b=make(3);b.requests[0].deadline=1;ok=ok&&CollectBatch(window,&b)==0&&fixtureCalls==1;
    ok=ok&&CleanupCollection()==1&&CancelCollection()==1;fixture=false;DestroyWindow(window);return ok?1:0;
}
extern "C" __declspec(dllexport) int BatchSize(){return sizeof(Batch);}
extern "C" __declspec(dllexport) int TestCleanup(){
    AcquireSRWLockExclusive(&stateLock);
    if(busy||!CleanLocked()){ReleaseSRWLockExclusive(&stateLock);return 0;}
    mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(Batch),nullptr);
    if(mapping)active=static_cast<Batch*>(MapViewOfFile(mapping,FILE_MAP_READ|FILE_MAP_WRITE,0,0,sizeof(Batch)));
    if(!active){if(mapping)CloseHandle(mapping);mapping=nullptr;ReleaseSRWLockExclusive(&stateLock);return 0;}
    *active={};active->status=3;targetProcess=OpenProcess(SYNCHRONIZE,FALSE,GetCurrentProcessId());
    ReleaseSRWLockExclusive(&stateLock);
    bool ok=CancelCollection()==1&&CleanupCollection()==0;
    AcquireSRWLockExclusive(&stateLock);ok=ok&&active&&active->cancel==1;if(active)active->status=5;ReleaseSRWLockExclusive(&stateLock);
    ok=ok&&CleanupCollection()==1&&BeginCollection()==1;return ok?1:0;
}
extern "C" __declspec(dllexport) int TestGuards(){
    std::vector<unsigned char> mem(0x10000);uint64_t base=0x700000000000;
    auto put=[&](uint64_t a,auto value){memcpy(mem.data()+a-base,&value,sizeof(value));};
    auto read=[&](uint64_t a,void* out,size_t n){if(a<base||a-base>mem.size()||n>mem.size()-(a-base))return false;memcpy(out,mem.data()+a-base,n);return true;};
    Layout l{};l.version=1;l.imageSize=0x1000;l.timestamp=777;l.root=0x600;l.ownerVtable=0x500;l.playerVtable=0x510;l.monsterVtable=0x520;l.maintainVtable=0x530;
    l.playerOffset=0x20;l.sessionOffset=0x28;l.registryOffset=0x30;l.actorIdOffset=0x20;l.actorTypeOffset=0x24;l.actionOffset=0x25;l.healthOffset=0x28;l.ghostOffset=0x2c;l.parentOffset=0x40;l.reverseParentOffset=0x38;l.maintainOffset=0x50;l.skillOffset=0x20;
    for(auto c:{&l.sender,&l.lookup,&l.death,&l.maintain,&l.parent}){c->rva=0x700;c->size=1;c->bytes[0]=0x90;}mem[0x700]=0x90;
    Request r{};r.version=r.mode=1;r.base=base;r.owner=base+0x2000;r.player=base+0x3000;r.target=base+0x4000;r.session=base+0x5000;r.head=base+0x6000;r.node=base+0x6100;r.targetId=99;
    auto buffHead=base+0x7000,buffNode=base+0x7100,record=base+0x7200,skill=base+0x7300;
    put(base+0x3c,uint32_t(0x100));put(base+0x100,uint32_t(0x4550));put(base+0x108,l.timestamp);put(base+0x150,l.imageSize);
    put(base+l.root,r.owner);put(r.owner,base+l.ownerVtable);put(r.owner+l.playerOffset,r.player);put(r.owner+l.sessionOffset,r.session);put(r.owner+l.registryOffset,r.head);put(r.owner+l.registryOffset+8,uint64_t(1));
    put(r.player,base+l.playerVtable);put(r.player+l.actorIdOffset,uint32_t(5));put(r.player+l.actorTypeOffset,uint8_t(1));put(r.player+l.healthOffset,uint32_t(100));put(r.player+l.maintainOffset,buffHead);
    put(r.head+8,r.node);put(r.head+0x19,uint8_t(1));put(r.node,r.head);put(r.node+8,r.head);put(r.node+0x10,r.head);put(r.node+0x20,r.targetId);put(r.node+0x28,r.target);
    put(r.target,base+l.monsterVtable);put(r.target+l.actorIdOffset,r.targetId);put(r.target+l.actorTypeOffset,uint8_t(2));put(r.target+l.actionOffset,uint8_t(6));
    put(buffHead+8,buffHead);put(buffHead+0x19,uint8_t(1));
    auto guard=[&](){try{return Validate(r,l,read);}catch(...){return uint32_t(24);}};
    bool ok=guard()==0;put(r.target+l.actionOffset,uint8_t(0));ok=ok&&guard()==14;put(r.target+l.actionOffset,uint8_t(7));
    put(r.node+0x28,r.target+8);ok=ok&&guard()==25;put(r.node+0x28,r.target);
    mem[0x700]^=1;ok=ok&&guard()==11;mem[0x700]^=1;
    put(r.player+l.parentOffset,base+0x8000);ok=ok&&guard()==0;put(base+0x8000+l.reverseParentOffset,r.player);ok=ok&&guard()==16;put(r.player+l.parentOffset,uint64_t(0));
    put(r.player+l.maintainOffset+8,uint64_t(1));put(buffHead+8,buffNode);put(buffNode,buffHead);put(buffNode+0x10,buffHead);put(buffNode+0x28,record);put(record,base+l.maintainVtable);put(record+l.skillOffset,skill);put(skill,uint16_t(9909));ok=ok&&guard()==22;
    put(skill,uint16_t(1234));ok=ok&&guard()==0;
    put(r.owner+l.sessionOffset,r.session+8);ok=ok&&guard()==12;
    return ok?1:0;
}

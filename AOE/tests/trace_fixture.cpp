#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#include <array>
#include <atomic>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

// Sacrificial local integration fixture. It never locates or interacts with TClient.
extern "C" __declspec(noinline) __declspec(dllexport)
uint64_t TraceEntry(uint64_t connection, const uint8_t* packet, uint64_t length, uint64_t extra) {
    volatile uint64_t values[4] = {connection, reinterpret_cast<uint64_t>(packet), length, extra};
    return values[0] ^ values[1] ^ values[2] ^ values[3] ^ GetCurrentThreadId();
}

extern "C" __declspec(noinline) __declspec(dllexport)
void DormantMarker() { volatile DWORD value = GetCurrentThreadId(); (void)value; }

namespace {
struct Shared {
    HANDLE start=nullptr,done=nullptr,finish=nullptr;
    std::atomic<unsigned> completed{0};
    std::atomic<uint64_t> checksum{0};
};
struct Worker { Shared* shared; unsigned index; };

bool VerifyExceptionForwarding() {
    constexpr DWORD code=0xE042414F;
    __try { RaiseException(code,0,0,nullptr); }
    __except(GetExceptionCode()==code ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH) {
        return true;
    }
    // Reaching here means a debugger incorrectly swallowed our first chance.
    return false;
}

DWORD WINAPI RunWorker(void* opaque) {
    const auto& worker = *static_cast<Worker*>(opaque);
    HANDLE initialGates[]{worker.shared->start,worker.shared->finish};
    const DWORD initial=WaitForMultipleObjects(2,initialGates,FALSE,20000);
    if(initial==WAIT_OBJECT_0+1) return 0;
    if(initial!=WAIT_OBJECT_0) return 71;
    if(!VerifyExceptionForwarding()) return 75;
    std::array<uint8_t,160> packet{};
    for (unsigned iteration=0; iteration<20; ++iteration) {
        for (size_t n=0; n<packet.size(); ++n) packet[n]=static_cast<uint8_t>(n+worker.index);
        packet[0]=static_cast<uint8_t>(worker.index);
        packet[1]=static_cast<uint8_t>(iteration);
        uint32_t length=32;
        const uint8_t* pointer=packet.data();
        if (iteration==1) { pointer=reinterpret_cast<const uint8_t*>(1); length=48; }
        if (iteration==2) { pointer=reinterpret_cast<const uint8_t*>(1); length=0; }
        if (iteration==3) length=160;
        const uint64_t result=TraceEntry(0x1122334455667700ull|worker.index, pointer,
            0xABCDEF0100000000ull|length,0x9988776655443300ull|iteration);
        worker.shared->checksum.fetch_xor(result);
        Sleep(2);
    }
    if (worker.shared->completed.fetch_add(1)+1==3) SetEvent(worker.shared->done);
    return WaitForSingleObject(worker.shared->finish,20000)==WAIT_OBJECT_0 ? 0 : 72;
}

bool SeedInactiveSlot(HANDLE thread,bool occupied) {
    CONTEXT context{};
    context.ContextFlags=CONTEXT_DEBUG_REGISTERS;
    if (!GetThreadContext(thread,&context)) return false;
    context.Dr3=reinterpret_cast<uint64_t>(&DormantMarker);
    context.Dr7 &= ~(3ull<<6);
    context.Dr7 = (context.Dr7 & ~(15ull<<28)) | (1ull<<28);
    if (occupied) {
        context.Dr0=context.Dr1=context.Dr2=context.Dr3;
        context.Dr7 = (context.Dr7 & ~0xFFFF00FFull) | 0x55;
    }
    return SetThreadContext(thread,&context)!=FALSE;
}
}

int wmain(int argc,wchar_t** argv) {
    std::wstring prefix,report;
    bool occupied=false;
    for(int n=1;n<argc;++n) {
        if(std::wstring(argv[n])==L"--prefix" && n+1<argc) prefix=argv[++n];
        else if(std::wstring(argv[n])==L"--report" && n+1<argc) report=argv[++n];
        else if(std::wstring(argv[n])==L"--occupied") occupied=true;
    }
    if(prefix.empty()||report.empty()) return 61;
    const auto open=[&](const wchar_t* suffix) {
        return OpenEventW(EVENT_MODIFY_STATE|SYNCHRONIZE,FALSE,(prefix+suffix).c_str());
    };
    HANDLE ready=open(L"_Ready");
    Shared shared{open(L"_Start"),open(L"_Done"),open(L"_Finish")};
    if(!ready||!shared.start||!shared.done||!shared.finish) return 62;
    std::array<Worker,3> workers{{{&shared,1},{&shared,2},{&shared,3}}};
    std::array<HANDLE,3> threads{};
    std::array<DWORD,3> ids{};
    for(unsigned n=0;n<2;++n) {
        threads[n]=CreateThread(nullptr,0,RunWorker,&workers[n],CREATE_SUSPENDED,&ids[n]);
        if(!threads[n]||!SeedInactiveSlot(threads[n],occupied)) return 63;
        if(ResumeThread(threads[n])==static_cast<DWORD>(-1)) return 64;
    }
    const auto writeMetadata=[&]() {
        std::ofstream output{std::filesystem::path(report),std::ios::trunc};
        const auto base=reinterpret_cast<uint64_t>(GetModuleHandleW(nullptr));
        output << "pid " << GetCurrentProcessId() << '\n'
               << "base " << base << '\n'
               << "trace_rva " << reinterpret_cast<uint64_t>(&TraceEntry)-base << '\n'
               << "dormant_rva " << reinterpret_cast<uint64_t>(&DormantMarker)-base << '\n'
               << "thread1 " << ids[0] << '\n'
               << "thread2 " << ids[1] << '\n'
               << "thread3 " << ids[2] << '\n';
        return static_cast<bool>(output);
    };
    if(!writeMetadata()) return 65;
    SetEvent(ready);
    HANDLE gates[]{shared.start,shared.finish};
    DWORD wait=WaitForMultipleObjects(2,gates,FALSE,20000);
    if(wait==WAIT_OBJECT_0) {
        // This thread is deliberately born after the debugger has attached.
        threads[2]=CreateThread(nullptr,0,RunWorker,&workers[2],0,&ids[2]);
        if(!threads[2]) return 66;
        if(!writeMetadata()) return 68;
        if(WaitForSingleObject(shared.done,20000)!=WAIT_OBJECT_0) return 67;
        if(WaitForSingleObject(shared.finish,20000)!=WAIT_OBJECT_0) return 69;
    } else if(wait!=WAIT_OBJECT_0+1) return 70;
    for(HANDLE thread:threads) if(thread) {
        if(WaitForSingleObject(thread,2000)!=WAIT_OBJECT_0) return 73;
        DWORD exitCode=0;
        if(!GetExitCodeThread(thread,&exitCode)||exitCode) return 74;
        CloseHandle(thread);
    }
    CloseHandle(ready); CloseHandle(shared.start); CloseHandle(shared.done); CloseHandle(shared.finish);
    return 0;
}

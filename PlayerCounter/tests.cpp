#include "reader.hpp"
#include "exit_profile.hpp"
#include <cstring>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <vector>
#include <fstream>
void Require(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
int main(int argc, char** argv) {
    try {
        if(argc==3&&std::string(argv[1])=="--current-image"){
            std::ifstream file(pc::targetPath,std::ios::binary);
            std::vector<unsigned char> disk((std::istreambuf_iterator<char>(file)),{});
            std::string error;auto recovered=pc::RecoverCounterProfile(disk,argv[2],&error);
            Require(bool(recovered),error.c_str());auto p=*recovered;
            Require(pc::ValidateRecoveredCounter(p,disk),"Recovered counter disk identity");
            Require(!pc::RecoverCounterProfile(disk,std::string(64,'A')),"Wrong SHA accepted");
            for(auto& guard:p.guards){guard.bytes[0]^=1;Require(!pc::ValidateRecoveredCounter(p,disk),"Corrupt cache fingerprint accepted");guard.bytes[0]^=1;}
            p.countOffset+=8;Require(!pc::ValidateRecoveredCounter(p,disk),"Wrong count operand accepted");p=*recovered;
            p.rootSlot+=8;Require(!pc::ValidateRecoveredCounter(p,disk),"Wrong root method accepted");p=*recovered;
            constexpr uintptr_t base=0x180000000,context=0x50010000;
            std::vector<unsigned char> memory(p.imageSize);
            IMAGE_DOS_HEADER dos{};std::memcpy(&dos,disk.data(),sizeof(dos));
            std::memcpy(memory.data(),disk.data(),size_t(dos.e_lfanew)+sizeof(IMAGE_NT_HEADERS64));
            for(const auto& g:p.guards)std::memcpy(memory.data()+g.rva,g.bytes.data(),g.bytes.size());
            uintptr_t contextTable=base+p.contextVtable,rootTable=base+p.rootVtable,method=base+p.rootVirtual;
            std::memcpy(memory.data()+p.rootVtable+p.rootSlot,&method,8);
            uint64_t count=7;bool changedRoot=false,missing=false;int rootReads=0;
            pc::Read read=[&](uintptr_t address,void* data,size_t length){
                auto copy=[&](const auto& value){if(length!=sizeof(value))return false;std::memcpy(data,&value,length);return true;};
                if(address==base+p.rootRva){auto root=changedRoot&&++rootReads==2?context+8:context;return copy(root);}
                if(address==context+p.countOffset)return !missing&&copy(count);
                if(address==context)return copy(contextTable);
                if(address==context-p.rootDelta)return copy(rootTable);
                if(address<base||address-base>memory.size()||length>memory.size()-(address-base))return false;
                std::memcpy(data,memory.data()+address-base,length);return true;
            };
            Require(pc::ValidateLive(read,base,&p),"Recovered live code rejected");
            Require(pc::ReadCount(read,base,&p)==7,"Dynamic count path failed");
            count=0;Require(pc::ReadCount(read,base,&p)==0,"Verified zero count failed");
            missing=true;Require(!pc::ReadCount(read,base,&p),"Missing count became zero");missing=false;
            contextTable+=8;Require(!pc::ReadCount(read,base,&p),"Wrong context accepted");contextTable-=8;
            changedRoot=true;rootReads=0;Require(!pc::ReadCount(read,base,&p),"Root replacement accepted");changedRoot=false;
            auto plan=pc::PrepareExit(read,base,&p);Require(plan&&plan->function==base+p.exitRva&&plan->root==context-p.rootDelta,"Dynamic Exit plan failed");
            for(const auto& g:p.guards){memory[g.rva]^=1;Require(!pc::PrepareExit(read,base,&p),"Changed live Exit evidence accepted");memory[g.rva]^=1;}
            memory[p.guards[2].rva]^=1;Require(!pc::ValidateLive(read,base,&p),"Changed live count resolver accepted");
            std::cout<<"PASS: automatic Counter/Exit recovery, SHA/cache/live guards, ASLR, missing count and root replacement; no game writes or Exit dispatch\n";return 0;
        }
        if (argc > 1 && std::string(argv[1]) == "--verify-profile") {
            std::ifstream file(pc::targetPath,std::ios::binary);
            std::vector<unsigned char> bytes((std::istreambuf_iterator<char>(file)),{});
            Require(pc::ValidateExitImage(bytes),"approved disk Exit AOB profile");
            std::cout << "PASS: unique executable-section Exit AOB at 0x95F200\n"; return 0;
        }
        if (argc > 1) {
            pc::ProcessReader reader;
            if (!reader.Open()) { std::wcerr << reader.Error() << L'\n'; return 2; }
            auto count = reader.Poll();
            if (!count) { std::wcerr << reader.Error() << L'\n'; return 3; }
            std::cout << "Live profile verified; Player : " << *count << "; Exit ready: " << reader.CanExit() << '\n'; return 0;
        }
        constexpr uintptr_t base = 0x140000000, context = 0x200000000;
        uintptr_t root = context; uint64_t count = 0; bool failure = false, replacement = false;
        unsigned rootReads = 0;
        pc::Read read = [&](uintptr_t p, void* data, size_t n) {
            if (failure || n != 8) return false;
            if (p == base + pc::rootRva) { auto value = replacement && ++rootReads == 2 ? context + 0x10000 : root; std::memcpy(data,&value,8); return true; }
            if (p == context + pc::countOffset) { std::memcpy(data,&count,8); return true; }
            return false;
        };
        for (auto value : {0ULL,1ULL,4ULL,5ULL,127ULL,4ULL,0xFFFFFFFFFFFFFFFFULL}) {
            count = value; auto actual = pc::ReadCount(read,base);
            Require(actual && *actual == value,"64-bit counter");
            Require(pc::Color(actual) == (value >= 5 ? pc::Tone::Alert : pc::Tone::Normal),"threshold transition");
        }
        failure = true; Require(!pc::ReadCount(read,base),"failed read"); failure = false;
        root = 0; Require(!pc::ReadCount(read,base),"null context");
        root = std::numeric_limits<uintptr_t>::max(); Require(!pc::ReadCount(read,base),"overflow context");
        root = context; replacement = true; Require(!pc::ReadCount(read,base),"context replacement");
        Require(pc::Color({}) == pc::Tone::Missing,"unavailable state");
        std::vector<unsigned char> memory(pc::imageSize);
        IMAGE_DOS_HEADER dos{}; dos.e_magic=IMAGE_DOS_SIGNATURE; dos.e_lfanew=0x100;
        IMAGE_NT_HEADERS64 pe{}; pe.Signature=IMAGE_NT_SIGNATURE; pe.FileHeader.Machine=IMAGE_FILE_MACHINE_AMD64;
        pe.FileHeader.TimeDateStamp=pc::timestamp; pe.OptionalHeader.Magic=IMAGE_NT_OPTIONAL_HDR64_MAGIC; pe.OptionalHeader.SizeOfImage=pc::imageSize;
        std::memcpy(memory.data(),&dos,sizeof(dos)); std::memcpy(memory.data()+0x100,&pe,sizeof(pe));
        std::memcpy(memory.data()+pc::getterRva,pc::getter.data(),pc::getter.size());
        pc::Read image = [&](uintptr_t p,void* d,size_t n) { if(p<base || p-base>memory.size() || n>memory.size()-(p-base)) return false; std::memcpy(d,memory.data()+(p-base),n); return true; };
        Require(pc::ValidateLive(image,base),"valid loaded profile");
        memory[pc::getterRva]=0xE9; Require(!pc::ValidateLive(image,base),"patched resolver rejected");
        memory[pc::getterRva]=pc::getter[0]; memory[0x108]^=1;
        Require(!pc::ValidateLive(image,base),"different PE timestamp rejected");
        memory[0x108]^=1;
        for (const auto& guard : pc::exit_profile::guards)
            std::memcpy(memory.data()+guard.rva,guard.bytes.data(),guard.bytes.size());
        Require(pc::ValidateExitCode(image,base),"approved Exit code");
        for (const auto& guard : pc::exit_profile::guards) {
            memory[guard.rva]^=1;
            Require(!pc::ValidateExitCode(image,base),"Exit/callee patch rejected");
            memory[guard.rva]^=1;
        }
        const uintptr_t expectedRoot = context-pc::exit_profile::rootDelta;
        uintptr_t rootTable=base+pc::exit_profile::rootVtableRva, contextTable=base+pc::exit_profile::contextVtableRva;
        uintptr_t rootMethod=base+pc::exit_profile::rootVirtualRva;
        std::memcpy(memory.data()+pc::exit_profile::rootVtableRva+0x2F0,&rootMethod,8);
        root = context; rootReads = 0; replacement = false;
        pc::Read exitRead = [&](uintptr_t p, void* d, size_t n) {
            if (n == 8 && p == base+pc::rootRva) {
                auto current = replacement && ++rootReads == 2 ? context+0x10000 : root;
                std::memcpy(d,&current,8); return true;
            }
            if (n == 8 && p == expectedRoot) { std::memcpy(d,&rootTable,8); return true; }
            if (n == 8 && p == context) { std::memcpy(d,&contextTable,8); return true; }
            return image(p,d,n);
        };
        auto plan = pc::PrepareExit(exitRead,base);
        Require(plan && plan->root == expectedRoot && plan->context == context && plan->function == base+0x95F200,"Exit argument and ASLR function");
        rootTable+=8; Require(!pc::PrepareExit(exitRead,base),"wrong root type rejected"); rootTable-=8;
        contextTable+=8; Require(!pc::PrepareExit(exitRead,base),"wrong context type rejected"); contextTable-=8;
        root=1; Require(!pc::PrepareExit(exitRead,base),"root subtraction underflow rejected"); root=context;
        replacement=true; rootReads=0; Require(!pc::PrepareExit(exitRead,base),"Exit context replacement rejected"); replacement=false;
        memory[pc::exit_profile::rootVtableRva+0x2F0]^=1;
        Require(!pc::PrepareExit(exitRead,base),"patched virtual slot rejected");
        memory[pc::exit_profile::rootVtableRva+0x2F0]^=1;
        // Synthetic disk fixture: AOB must occur exactly once in executable code at the approved RVA.
        std::vector<unsigned char> disk(4096);
        pe.FileHeader.SizeOfOptionalHeader=sizeof(IMAGE_OPTIONAL_HEADER64); pe.FileHeader.NumberOfSections=1;
        std::memcpy(disk.data(),&dos,sizeof(dos)); std::memcpy(disk.data()+0x100,&pe,sizeof(pe));
        IMAGE_SECTION_HEADER section{}; section.VirtualAddress=static_cast<DWORD>(pc::exit_profile::functionRva);
        section.PointerToRawData=1024; section.SizeOfRawData=1024; section.Characteristics=IMAGE_SCN_MEM_EXECUTE;
        std::memcpy(disk.data()+0x100+sizeof(pe),&section,sizeof(section));
        std::memcpy(disk.data()+1024,pc::exit_profile::exitBody.data(),pc::exit_profile::exitBody.size());
        Require(pc::ValidateExitImage(disk),"unique disk AOB");
        disk[1024]^=1; Require(!pc::ValidateExitImage(disk),"missing AOB"); disk[1024]^=1;
        std::memcpy(disk.data()+1280,pc::exit_profile::exitBody.data(),pc::exit_profile::exitBody.size());
        Require(!pc::ValidateExitImage(disk),"ambiguous AOB");
        std::fill(disk.begin()+1280,disk.begin()+1536,static_cast<unsigned char>(0));
        section.Characteristics=0; std::memcpy(disk.data()+0x100+sizeof(pe),&section,sizeof(section));
        Require(!pc::ValidateExitImage(disk),"non-executable AOB");
        Require(!pc::ValidateExitImage(std::span(disk).first(128)),"truncated PE");
        pc::ProcessReader reader; reader.Close(); reader.Close(); Require(!reader.Poll(),"closed process");
        Require(!reader.RequestExit(),"disconnected Exit never dispatched");
        std::cout << "PASS: counter/color, read failures, Exit root/context/ASLR, all code patch guards, unique AOB and fail-closed dispatch\n";
        return 0;
    } catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}

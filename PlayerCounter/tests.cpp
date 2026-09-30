#include "reader.hpp"
#include <cstring>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <vector>
void Require(bool value, const char* message) { if (!value) throw std::runtime_error(message); }
int main(int argc, char**) {
    try {
        if (argc > 1) {
            pc::ProcessReader reader;
            if (!reader.Open()) { std::wcerr << reader.Error() << L'\n'; return 2; }
            auto count = reader.Poll();
            if (!count) { std::wcerr << reader.Error() << L'\n'; return 3; }
            std::cout << "Live profile verified; Player : " << *count << '\n'; return 0;
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
        pc::ProcessReader reader; reader.Close(); reader.Close(); Require(!reader.Poll(),"closed process");
        std::cout << "PASS: counters, colors, invalid reads, context change, PE/profile and live patch guards\n";
        return 0;
    } catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}

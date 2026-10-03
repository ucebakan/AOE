#include "reader.hpp"
#include "exit_profile.hpp"
#include <algorithm>
#include <cstring>
#include <vector>

namespace pc {
bool ValidateExitImage(std::span<const unsigned char> bytes,const RecoveredProfile* profile) {
    if(profile)return ValidateRecoveredCounter(*profile,bytes);
    auto copy = [&](size_t offset, void* data, size_t length) {
        if (offset > bytes.size() || length > bytes.size() - offset) return false;
        std::memcpy(data, bytes.data() + offset, length); return true;
    };
    IMAGE_DOS_HEADER dos{};
    if (!copy(0, &dos, sizeof(dos)) || dos.e_magic != IMAGE_DOS_SIGNATURE || dos.e_lfanew < 64 || dos.e_lfanew > 4096) return false;
    IMAGE_NT_HEADERS64 nt{};
    if (!copy(dos.e_lfanew, &nt, sizeof(nt)) || nt.Signature != IMAGE_NT_SIGNATURE ||
        nt.FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64 || nt.FileHeader.TimeDateStamp != timestamp ||
        nt.FileHeader.SizeOfOptionalHeader != sizeof(IMAGE_OPTIONAL_HEADER64) ||
        nt.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC || nt.OptionalHeader.SizeOfImage != imageSize ||
        !nt.FileHeader.NumberOfSections || nt.FileHeader.NumberOfSections > 96) return false;
    size_t hits = 0;
    bool expected = false;
    for (size_t i = 0; i < nt.FileHeader.NumberOfSections; ++i) {
        IMAGE_SECTION_HEADER section{};
        if (!copy(static_cast<size_t>(dos.e_lfanew) + sizeof(nt) + i * sizeof(section), &section, sizeof(section))) return false;
        if (section.PointerToRawData > bytes.size() || section.SizeOfRawData > bytes.size() - section.PointerToRawData ||
            section.VirtualAddress >= imageSize || section.SizeOfRawData > imageSize - section.VirtualAddress) return false;
        if (!(section.Characteristics & IMAGE_SCN_MEM_EXECUTE)) continue;
        auto data = bytes.subspan(section.PointerToRawData, section.SizeOfRawData);
        const auto& pattern = exit_profile::exitBody;
        auto cursor = data.begin();
        while (cursor != data.end()) {
            auto found = std::search(cursor, data.end(), pattern.begin(), pattern.end());
            if (found == data.end()) break;
            if (++hits > 1) return false;
            expected = section.VirtualAddress + static_cast<size_t>(found - data.begin()) == exit_profile::functionRva;
            cursor = found + 1;
        }
    }
    return hits == 1 && expected;
}
bool ValidateExitCode(const Read& read, uintptr_t base,const RecoveredProfile* profile) {
    if (!ValidateLive(read, base,profile)) return false;
    if(profile){for(const auto& guard:profile->guards){std::vector<unsigned char> actual(guard.bytes.size());if(!read(base+guard.rva,actual.data(),actual.size())||actual!=guard.bytes)return false;}return true;}
    for (const auto& guard : exit_profile::guards) {
        std::vector<unsigned char> actual(guard.bytes.size());
        if (!read(base + guard.rva, actual.data(), actual.size()) ||
            !std::equal(actual.begin(), actual.end(), guard.bytes.begin())) return false;
    }
    return true;
}
std::optional<ExitPlan> PrepareExit(const Read& read, uintptr_t base,const RecoveredProfile* profile) {
    const auto resolvedRoot=profile?profile->rootRva:pc::rootRva;const auto resolvedCount=profile?profile->countOffset:pc::countOffset;
    const auto delta=profile?profile->rootDelta:exit_profile::rootDelta;
    if (!ValidateExitCode(read, base,profile)) return {};
    uintptr_t context = 0, rootVtable = 0, contextVtable = 0, rootVirtual = 0, after = 0;
    if (!read(base + resolvedRoot, &context, sizeof(context)) || context < delta ||
        context % 8 || !ValidPointer(context, resolvedCount + 8)) return {};
    const uintptr_t root = context - delta;
    if (!ValidPointer(root, delta + resolvedCount + 8) ||
        !read(root, &rootVtable, sizeof(rootVtable)) || rootVtable != base + (profile?profile->rootVtable:exit_profile::rootVtableRva) ||
        !read(context, &contextVtable, sizeof(contextVtable)) || contextVtable != base + (profile?profile->contextVtable:exit_profile::contextVtableRva) ||
        !read(rootVtable + (profile?profile->rootSlot:0x2F0), &rootVirtual, sizeof(rootVirtual)) || rootVirtual != base + (profile?profile->rootVirtual:exit_profile::rootVirtualRva) ||
        !read(base + resolvedRoot, &after, sizeof(after)) || after != context) return {};
    return ExitPlan{context, root, base + (profile?profile->exitRva:exit_profile::functionRva)};
}
}

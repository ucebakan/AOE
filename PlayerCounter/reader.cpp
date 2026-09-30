#include "reader.hpp"
#include <tlhelp32.h>
#include <bcrypt.h>
#include <vector>
#include <cstring>

namespace pc {
namespace {
struct Handle {
    HANDLE value;
    explicit Handle(HANDLE h) : value(h) {}
    ~Handle() { if (value && value != INVALID_HANDLE_VALUE) CloseHandle(value); }
    bool Valid() const { return value && value != INVALID_HANDLE_VALUE; }
};
bool KnownFile(HANDLE file) {
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0) return false;
    bool good = BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) >= 0;
    std::array<unsigned char, 65536> buffer{};
    DWORD length = 0;
    while (good) {
        if (!ReadFile(file, buffer.data(), static_cast<DWORD>(buffer.size()), &length, nullptr)) { good = false; break; }
        if (!length) break;
        good = BCryptHashData(hash, buffer.data(), length, 0) >= 0;
    }
    std::array<unsigned char, 32> digest{};
    if (good) good = BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0) >= 0;
    if (hash) BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    std::string hex;
    for (auto b : digest) { hex += "0123456789ABCDEF"[b >> 4]; hex += "0123456789ABCDEF"[b & 15]; }
    return good && hex == knownSha;
}
}
bool ValidPointer(uintptr_t p, size_t size) {
    constexpr uintptr_t limit = 0x00007FFFFFFF0000ULL;
    return size && p >= 0x10000 && p < limit && size <= limit - p;
}
bool ValidateLive(const Read& read, uintptr_t base) {
    if (!ValidPointer(base, imageSize)) return false;
    IMAGE_DOS_HEADER dos{};
    if (!read(base, &dos, sizeof(dos)) || dos.e_magic != IMAGE_DOS_SIGNATURE || dos.e_lfanew < 64 || dos.e_lfanew > 4096) return false;
    IMAGE_NT_HEADERS64 pe{};
    if (!read(base + dos.e_lfanew, &pe, sizeof(pe)) || pe.Signature != IMAGE_NT_SIGNATURE ||
        pe.FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64 || pe.FileHeader.TimeDateStamp != timestamp ||
        pe.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC || pe.OptionalHeader.SizeOfImage != imageSize) return false;
    std::array<unsigned char, getter.size()> actual{};
    return read(base + getterRva, actual.data(), actual.size()) && actual == getter;
}
std::optional<uint64_t> ReadCount(const Read& read, uintptr_t base) {
    if (!ValidPointer(base, rootRva + sizeof(uintptr_t))) return {};
    uintptr_t context = 0, after = 0;
    uint64_t count = 0;
    if (!read(base + rootRva, &context, sizeof(context)) || context % 8 != 0 ||
        !ValidPointer(context, countOffset + sizeof(count)) ||
        !read(context + countOffset, &count, sizeof(count)) ||
        !read(base + rootRva, &after, sizeof(after)) || after != context) return {};
    return count;
}
bool ProcessReader::ReadMemory(uintptr_t address, void* data, size_t size) const {
    if (!process_ || !ValidPointer(address, size)) return false;
    SIZE_T got = 0;
    return ReadProcessMemory(process_, reinterpret_cast<const void*>(address), data, size, &got) && got == size;
}
void ProcessReader::Close() {
    if (process_) CloseHandle(process_);
    process_ = nullptr; base_ = 0;
}
bool ProcessReader::IsAlive() const {
    DWORD code = 0;
    return process_ && GetExitCodeProcess(process_, &code) && code == STILL_ACTIVE;
}
bool ProcessReader::Open() {
    Close();
    error_ = L"Game unavailable or access denied";
    Handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
    if (!snapshot.Valid()) return false;
    PROCESSENTRY32W entry{sizeof(entry)};
    DWORD selected = 0;
    if (Process32FirstW(snapshot.value, &entry)) do {
        if (_wcsicmp(entry.szExeFile, L"TClient.exe")) continue;
        Handle candidate(OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, entry.th32ProcessID));
        if (!candidate.Valid()) {
            auto error = GetLastError();
            error_ = L"Cannot open TClient.exe; Windows error " + std::to_wstring(error);
            if (error == ERROR_ACCESS_DENIED) error_ += L" (access denied; run PlayerCounter as administrator)";
            continue;
        }
        wchar_t path[32768]{}; DWORD size = static_cast<DWORD>(std::size(path));
        if (!QueryFullProcessImageNameW(candidate.value, 0, path, &size) || _wcsicmp(path, targetPath)) continue;
        if (selected) { error_ = L"Multiple matching game processes"; Close(); return false; }
        selected = entry.th32ProcessID;
        process_ = candidate.value; candidate.value = nullptr;
    } while (Process32NextW(snapshot.value, &entry));
    if (!process_) return false;
    // Keep the file locked against replacement while hashing and validating the loaded image.
    Handle file(CreateFileW(targetPath, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
    if (!file.Valid() || !KnownFile(file.value)) {
        error_ = L"Unsupported game build: profile rejected"; Close(); return false;
    }
    Handle modules(CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, selected));
    MODULEENTRY32W module{sizeof(module)};
    if (modules.Valid() && Module32FirstW(modules.value, &module)) do {
        if (!_wcsicmp(module.szModule, L"TClient.exe") && !_wcsicmp(module.szExePath, targetPath) && module.modBaseSize == imageSize) {
            base_ = reinterpret_cast<uintptr_t>(module.modBaseAddr); break;
        }
    } while (Module32NextW(modules.value, &module));
    Read read = [this](uintptr_t p, void* d, size_t n) { return ReadMemory(p, d, n); };
    if (!base_ || !IsAlive() || !ValidateLive(read, base_)) {
        error_ = L"Loaded image/profile mismatch"; Close(); return false;
    }
    error_.clear(); return true;
}
std::optional<uint64_t> ProcessReader::Poll() {
    if (!IsAlive()) { error_ = L"Game unavailable"; Close(); return {}; }
    Read read = [this](uintptr_t p, void* d, size_t n) { return ReadMemory(p, d, n); };
    // Recheck every sample so changes to the protected resolver immediately disable the display.
    if (!ValidateLive(read, base_)) { error_ = L"Live resolver/profile mismatch"; return {}; }
    auto count = ReadCount(read, base_);
    if (!IsAlive()) { Close(); count.reset(); }
    error_ = count ? L"" : L"Counter unavailable";
    return count;
}
}

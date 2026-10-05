#pragma once
#include <windows.h>
#include <array>
#include <cstdint>
#include <functional>
#include <optional>
#include <string>
#include <span>
#include <vector>
#include <memory>

namespace pc {
inline constexpr wchar_t targetPath[] = L"C:\\Games\\4Unity\\TClient.exe";
inline constexpr char knownSha[] = "9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28";
inline constexpr uint32_t timestamp = 1790155782, imageSize = 16023552;
inline constexpr uintptr_t rootRva = 0xE6E9C0, countOffset = 0x1148;
inline constexpr uintptr_t getterRva = 0x7C1A20;
inline constexpr std::array<unsigned char, 8> getter{0x48,0x8B,0x05,0x99,0xCF,0x6A,0x00,0xC3};
using Read = std::function<bool(uintptr_t, void*, size_t)>;
struct CodeGuard { uintptr_t rva=0;std::vector<unsigned char> bytes; };
struct RecoveredProfile {
    std::string sha;
    uint32_t timestamp=0,imageSize=0;
    uintptr_t getterRva=0,rootRva=0,countOffset=0,contextVtable=0,rootVtable=0,rootDelta=0,exitRva=0,rootVirtual=0,rootSlot=0;
    std::vector<CodeGuard> guards;
};
std::shared_ptr<const RecoveredProfile> RecoverCounterProfile(std::span<const unsigned char> disk,const std::string& sha,std::string* error=nullptr);
bool ValidateRecoveredCounter(const RecoveredProfile& profile,std::span<const unsigned char> disk);
bool ValidPointer(uintptr_t p, size_t size);
bool ValidateLive(const Read& read, uintptr_t base,const RecoveredProfile* profile=nullptr);
std::optional<uint64_t> ReadCount(const Read& read, uintptr_t base,const RecoveredProfile* profile=nullptr);
struct ExitPlan { uintptr_t context, root, function; };
bool ValidateExitImage(std::span<const unsigned char> image,const RecoveredProfile* profile=nullptr);
bool ValidateExitCode(const Read& read, uintptr_t base,const RecoveredProfile* profile=nullptr);
// The action path must explicitly choose the recovered profile. An omitted
// argument must never silently select pre-update fixed RVAs.
std::optional<ExitPlan> PrepareExit(const Read& read, uintptr_t base,const RecoveredProfile* profile);
enum class Tone { Missing, Normal, Alert };
inline Tone Color(std::optional<uint64_t> count) { return !count ? Tone::Missing : *count >= 5 ? Tone::Alert : Tone::Normal; }

class ProcessReader {
    HANDLE process_ = nullptr;
    uintptr_t base_ = 0;
    std::shared_ptr<const RecoveredProfile> profile_;
    HANDLE exitThread_ = nullptr;
    bool exitProfileValid_ = false, exitBlocked_ = false;
    ULONGLONG lastExit_ = 0;
    std::wstring exitError_;
    std::wstring error_ = L"Game unavailable";
    bool ReadMemory(uintptr_t address, void* data, size_t size) const;
public:
    ~ProcessReader() { Close(); }
    ProcessReader() = default;
    ProcessReader(const ProcessReader&) = delete;
    ProcessReader& operator=(const ProcessReader&) = delete;
    bool Open();
    void Close();
    bool IsAlive() const;
    bool Connected() const { return process_ != nullptr; }
    std::optional<uint64_t> Poll();
    bool CanExit();
    bool RequestExit();
    const std::wstring& ExitError() const { return exitError_; }
    const std::wstring& Error() const { return error_; }
};
}

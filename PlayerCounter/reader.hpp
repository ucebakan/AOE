#pragma once
#include <windows.h>
#include <array>
#include <cstdint>
#include <functional>
#include <optional>
#include <string>

namespace pc {
inline constexpr wchar_t targetPath[] = L"C:\\Games\\4Unity\\TClient.exe";
inline constexpr char knownSha[] = "9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28";
inline constexpr uint32_t timestamp = 1790155782, imageSize = 16023552;
inline constexpr uintptr_t rootRva = 0xE6E9C0, countOffset = 0x1148;
inline constexpr uintptr_t getterRva = 0x7C1A20;
inline constexpr std::array<unsigned char, 8> getter{0x48,0x8B,0x05,0x99,0xCF,0x6A,0x00,0xC3};
using Read = std::function<bool(uintptr_t, void*, size_t)>;
bool ValidPointer(uintptr_t p, size_t size);
bool ValidateLive(const Read& read, uintptr_t base);
std::optional<uint64_t> ReadCount(const Read& read, uintptr_t base);
enum class Tone { Missing, Normal, Alert };
inline Tone Color(std::optional<uint64_t> count) { return !count ? Tone::Missing : *count >= 5 ? Tone::Alert : Tone::Normal; }

class ProcessReader {
    HANDLE process_ = nullptr;
    uintptr_t base_ = 0;
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
    const std::wstring& Error() const { return error_; }
};
}

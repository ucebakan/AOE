#include "../../PlayerCounter/reader.hpp"
// Independent read-only reader: closing the overlay must never stop SafeMode.
extern "C" __declspec(dllexport) void* CreateCountReader() { return new pc::ProcessReader(); }
extern "C" __declspec(dllexport) int PollCount(void* handle, unsigned long long* count) {
    auto reader = static_cast<pc::ProcessReader*>(handle);
    if (!reader || !count) return 0;
    if (!reader->Connected() && !reader->Open()) return 0;
    auto value = reader->Poll();
    if (!value) return 0;
    *count = *value; return 1;
}
extern "C" __declspec(dllexport) void DestroyCountReader(void* handle) { delete static_cast<pc::ProcessReader*>(handle); }

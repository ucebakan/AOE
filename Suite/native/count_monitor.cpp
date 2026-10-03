#include "../../PlayerCounter/reader.hpp"
#include <fstream>
#include <sstream>
#include <cstring>
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
extern "C" __declspec(dllexport) int ResolveCounterDisk(const char* sha,char* message,int capacity){
    std::ifstream input(pc::targetPath,std::ios::binary);std::vector<unsigned char> disk((std::istreambuf_iterator<char>(input)),{});std::string error;
    auto p=pc::RecoverCounterProfile(disk,sha,&error);std::ostringstream text;
    if(p)text<<"Counter+Exit resolved: root="<<std::hex<<p->rootRva<<" count="<<p->countOffset<<" exit="<<p->exitRva;else text<<error;
    if(message&&capacity>0)strncpy_s(message,size_t(capacity),text.str().c_str(),_TRUNCATE);
    return p?1:0;
}

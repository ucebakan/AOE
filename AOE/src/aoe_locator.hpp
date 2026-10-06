#pragma once
#include "profile.hpp"
#include <Windows.h>

namespace aoe {
struct AoeSignatureResult {
    std::string name,pattern,evidence;
    std::vector<uint64_t> matches;
    uint64_t rva=0;
    bool valid=false;
};
struct VisualReaderCandidate {
    uint64_t rva=0,lookupRva=0,branchTargetRva=0,fieldOffset=0;
    X64RegisterId baseRegister=X64RegisterId::Invalid,valueRegister=X64RegisterId::Invalid;
    std::vector<uint8_t> fingerprint;
};
struct AoeLocatorResult {
    ImageInfo image;
    uint64_t preferredBase=0;
    std::vector<AoeSignatureResult> signatures;
    bool ready=false;
    bool cacheUsed=false;
    std::string error;
    // Exact disk bytes from all validated semantic neighborhoods, for read-only
    // live comparison. No wildcard bytes are ignored by the runtime safety gate.
    std::vector<std::pair<uint64_t,std::vector<uint8_t>>> liveEvidence;
    std::vector<VisualReaderCandidate> visualCandidates;
    VisualReaderCandidate visualReader;
    bool visualReady=false;
    std::string visualError;
    uint64_t prepDistance=29;
    RuntimeLayout recovered;
};
std::vector<size_t> MatchAoePattern(const std::vector<uint8_t>& bytes,const std::string& pattern);
bool DecodeAoeCall(const std::vector<uint8_t>& bytes,uint64_t instructionRva,uint64_t imageSize,uint64_t& target);
bool ResolveAlternateInitialRoute(const ImageInfo& image,RuntimeLayout& runtime,std::string& error);
bool ResolveAoeWorkerRoutes(const ImageInfo& image,RuntimeLayout& runtime,std::string& error);
std::vector<VisualReaderCandidate> DecodeVisualReaderSequences(const std::vector<uint8_t>& bytes,uint64_t baseRva,uint64_t imageSize);
bool LocateAoeImage(const std::filesystem::path& path,AoeLocatorResult& result,std::string& error,const BuildProfile* cached=nullptr);
bool ValidateAoeProfile(const BuildProfile& profile,const AoeLocatorResult& result,std::string& error);
using OwnedPatchValidator = int (__cdecl*)(uint32_t,uint64_t,uint64_t,const char*,uint64_t,const uint8_t*,const uint8_t*,size_t);
void ConfigureOwnedPatchValidator(OwnedPatchValidator validator);
int CheckOwnedPatchProof(uint32_t pid,uint64_t created,uint64_t base,const char* sha,uint64_t rva,const uint8_t* original,const uint8_t* patched,size_t count);
bool MatchAoeLiveEvidence(const TargetInfo& target,const AoeLocatorResult& result,uint64_t rva,
    const std::vector<uint8_t>& expected,const std::vector<uint8_t>& actual,const std::vector<uint8_t>& liveBranch,OwnedPatchValidator validator);
bool ValidateAoeLive(HANDLE process,const TargetInfo& target,const AoeLocatorResult& result,std::string& error);
bool RecoverAndSaveAoeProfile(const std::filesystem::path& image,const std::filesystem::path& directory,BuildProfile& profile,std::filesystem::path& selected,std::string& error);
}

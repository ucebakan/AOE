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
    std::string error;
    // Exact disk bytes from all validated semantic neighborhoods, for read-only
    // live comparison. No wildcard bytes are ignored by the runtime safety gate.
    std::vector<std::pair<uint64_t,std::vector<uint8_t>>> liveEvidence;
    std::vector<VisualReaderCandidate> visualCandidates;
    VisualReaderCandidate visualReader;
    bool visualReady=false;
    std::string visualError;
};
std::vector<size_t> MatchAoePattern(const std::vector<uint8_t>& bytes,const std::string& pattern);
bool DecodeAoeCall(const std::vector<uint8_t>& bytes,uint64_t instructionRva,uint64_t imageSize,uint64_t& target);
std::vector<VisualReaderCandidate> DecodeVisualReaderSequences(const std::vector<uint8_t>& bytes,uint64_t baseRva,uint64_t imageSize);
bool LocateAoeImage(const std::filesystem::path& path,AoeLocatorResult& result,std::string& error);
bool ValidateAoeProfile(const BuildProfile& profile,const AoeLocatorResult& result,std::string& error);
bool ValidateAoeLive(HANDLE process,const TargetInfo& target,const AoeLocatorResult& result,std::string& error);
}

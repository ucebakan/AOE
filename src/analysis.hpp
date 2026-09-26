#pragma once
#include "model.hpp"
namespace aoe {
bool Compatible(const Capture& a,const Capture& b,std::string& reason);
Analysis Analyze(const Capture& capture,const Capture* idle=nullptr);
bool ExportJson(const std::filesystem::path& path,const Capture& capture,const Analysis& analysis,std::string& error);
std::vector<Capture> SyntheticCaptures();
std::vector<uint64_t> StackFingerprint(const Event& event);
std::string FingerprintText(const Event& event);
MarkerRelation RelativeToMarkers(const Capture& capture,double ms);
void AddTimingResearch(const Capture& capture,const Capture* idle,Analysis& analysis);
Capture ObservedTimingFixture(); // supplied timestamps, synthetic packet bytes/provenance
}

#pragma once
#include "model.hpp"
#include <filesystem>
#include <string>
#include <vector>

namespace aoe {
struct ProfileAnchor {std::string name;uint64_t rva=0;};
struct BuildProfile {uint32_t schemaVersion=0;std::string targetSha256,created,notes;std::vector<ProfileAnchor> anchors;RuntimeLayout runtime;};
const ProfileAnchor* FindProfileAnchor(const BuildProfile& profile,const std::string& name);
uint64_t ProfileTraceRva(const BuildProfile& profile,TraceMode mode);
bool RuntimeLayoutSupportsInitialNx(const RuntimeLayout& layout,std::string& reason);
bool RuntimeLayoutSupportsVisualSuppression(const RuntimeLayout& layout,std::string& reason);
struct ResolvedAnchor {std::string name;uint64_t rva=0,address=0;bool inModule=false,executable=false;};
enum class ProfileValidation { NotAttached,KnownProfile,UnknownBuild,Validating,Valid,ValidationFailed,HashMismatch };
const char* ProfileValidationName(ProfileValidation value);
bool LoadBuildProfile(const std::filesystem::path& path,BuildProfile& profile,std::string& error);
bool SelectBuildProfile(const std::filesystem::path& directory,const std::string& sha256,BuildProfile& profile,std::filesystem::path& selected,std::string& error);
bool ResolveAndValidateProfile(const BuildProfile& profile,const TargetInfo& target,std::vector<ResolvedAnchor>& anchors,std::string& error);
}

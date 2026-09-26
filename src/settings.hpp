#pragma once
#include <filesystem>
#include <functional>
#include <string>

namespace aoe {
struct ManagerSettings {
    uint32_t initialCalls=2;
    bool autoAttach=false,autoArm=false,startWithWindows=false,minimizeAfterAutoArm=false;
    int lastSelectedTab=2,windowX=-1,windowY=-1,windowWidth=1120,windowHeight=760;
};
ManagerSettings SafeDefaultSettings();
std::filesystem::path DefaultSettingsPath();
bool ValidateSettings(ManagerSettings& settings);
bool LoadSettings(const std::filesystem::path& path,ManagerSettings& settings,std::string& diagnostic);
bool SaveSettings(const std::filesystem::path& path,const ManagerSettings& settings,std::string& error);
std::wstring StartupCommand(const std::filesystem::path& executable);
struct StartupRegistryIo {std::function<bool(const std::wstring&,std::string&)> set;std::function<bool(std::string&)> remove;};
StartupRegistryIo WindowsStartupRegistryIo();
bool ApplyStartupSetting(const StartupRegistryIo& io,bool enabled,const std::filesystem::path& executable,std::string& error);
}

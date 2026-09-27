#pragma once
#include <array>
#include <cstdint>
#include <string>

namespace aoe {
struct UiRect { int x=0,y=0,width=0,height=0; };
constexpr int MinimumClientWidth96=960,MinimumClientHeight96=650;
int ScaleUi(int value,uint32_t dpi);
UiRect MainTabBounds(int clientWidth,int clientHeight,uint32_t dpi);
bool RectFits(const UiRect& outer,const UiRect& inner);

struct ResearchPageLayout {
    UiRect heading;
    std::array<UiRect,4> buttons;
    UiRect inspectorHeading;
    std::array<UiRect,5> inspectorButtons;
    UiRect inspectorPrepare,inspectorStatus,inspectorList;
    UiRect provenanceHeading;
    std::array<UiRect,7> provenanceButtons;
    UiRect provenanceStatus,provenanceOutput;
    UiRect writerHeading;
    std::array<UiRect,8> writerButtons;
    UiRect writerStatus,writerOutput,viewer;
};
struct TargetInspectorAvailability {
    bool startEnabled=false,prepareEnabled=false,suppressAutoArm=false;
    std::string reason;
};
TargetInspectorAvailability CalculateTargetInspectorAvailability(bool attached,bool profileValid,bool inspectorRunning,bool initialNxActive,bool transitionPending,bool prepared,const std::string& transitionError={});
struct PageVisibility {
    bool research=false,profiles=false,play=false,diagnostics=false;
};
PageVisibility CalculatePageVisibility(int selectedTab,bool diagnosticsExpanded);
struct ProfilesPageLayout { UiRect heading,summary,list; };
struct PlayPageLayout {
    UiRect heading,status,initialLabel,initialEdit,initialSpin;
    std::array<UiRect,5> options;
    UiRect visualStatus;
    UiRect save,attach,detach,arm,reset,exportButton,advanced,copy,freeze,diagnostics;
};
ResearchPageLayout CalculateResearchLayout(const UiRect& page,uint32_t dpi);
ProfilesPageLayout CalculateProfilesLayout(const UiRect& page,uint32_t dpi);
PlayPageLayout CalculatePlayLayout(const UiRect& page,uint32_t dpi);
}

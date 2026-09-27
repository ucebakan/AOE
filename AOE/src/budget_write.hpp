#pragma once
#include "tracer.hpp"
#include <ostream>

namespace aoe {
inline constexpr char BudgetWriteLimitation[]="This experiment establishes only the causal effect of modifying the finite record+34 budget for the matched 0x020A/0x0246 runtime record. Gameplay semantics and physical units must be inferred from observed behavior, not from the numeric value alone.";
struct BudgetProbe {
    uint64_t recordPointer=0,linkedPointer=0;
    bool recordPlausible=false,recordReadable=false,linkedPointerReadable=false,linkedWordReadable=false;
    bool record64Readable=false,record98Readable=false,record34Readable=false;
    uint16_t linkedWord0=0;
    uint32_t record64=0,old34=0;
    uint8_t record98=0;
};
struct BudgetWritePlan {
    uint64_t recordPointer=0,linkedPointer=0,address=0,moduleBase=0,breakpointAddress=0;
    uint16_t linkedWord0=0;
    uint32_t record64=0,old34=0,new34=0;
    uint8_t record98=0;
    double timestampMs=0;
};
BudgetProbe ProbeBudgetWrite(HANDLE process,uint64_t recordPointer);
bool IsBudgetWriteMatch(const BudgetProbe& probe);
bool ArmBudgetExperiment(BudgetExperiment& experiment,const TargetInfo& attached,const TargetInfo& captureTarget,std::string& error);
bool ClaimBudgetWrite(BudgetExperiment& experiment,const BudgetProbe& probe,const TargetInfo& target,double timestampMs,BudgetWritePlan& plan);
void FinishBudgetWrite(BudgetExperiment& experiment,const BudgetWritePlan& plan,bool success,const std::string& error={});
void MarkBudgetBreakpointRemoved(BudgetExperiment& experiment);
void WriteBudgetExperimentJson(std::ostream& out,const Capture& capture);
std::string BudgetExperimentDetails(const BudgetExperiment& experiment);
}

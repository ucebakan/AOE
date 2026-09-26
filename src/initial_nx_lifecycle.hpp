#pragma once
#include "initial_2x.hpp"

namespace aoe {
enum class InitialNxConfigurationAction { None,PrepareReady,ReplaceArmedPending,QueueAfterActiveReplay,ResetTerminalExperiment };

struct InitialNxLifecycleState {
    uint32_t configuredInitialCalls=DefaultInitialCalls;
    uint32_t pendingNextInitialCalls=0;
    bool configurationPending=false;
    bool hasLastExperiment=false;
    Initial2xExperiment lastExperiment;
};

bool InitialNxReplayActive(const Initial2xExperiment& experiment);
bool ShouldQueueContinuousInitialNx(const Initial2xExperiment& experiment,bool continuousEnabled,bool researchActive,bool configurationPending);
InitialNxConfigurationAction ConfigureNextInitialNx(InitialNxLifecycleState& state,const Initial2xExperiment& current,uint32_t configuredInitialCalls);
InitialNxConfigurationAction PendingInitialNxAction(InitialNxLifecycleState& state,const Initial2xExperiment& current);
void MarkInitialNxConfigurationApplied(InitialNxLifecycleState& state);
void ResetInitialNxLifecycleForNewSession(InitialNxLifecycleState& state);
const char* InitialNxConfigurationActionName(InitialNxConfigurationAction action);
}

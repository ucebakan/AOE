#include "initial_nx_lifecycle.hpp"

namespace aoe {
namespace {
bool Terminal(const Initial2xExperiment& experiment){return experiment.completed||experiment.aborted;}
void ArchiveTerminal(InitialNxLifecycleState& state,const Initial2xExperiment& experiment){if(!Terminal(experiment))return;state.lastExperiment=experiment;state.hasLastExperiment=true;}
}

bool InitialNxReplayActive(const Initial2xExperiment& experiment){return experiment.armed&&!Terminal(experiment)&&(experiment.initialCallsObserved>0||experiment.repeatPending||experiment.replayInProgress||experiment.awaitingReplayedCall||experiment.redirectClaimed);}
bool ShouldQueueContinuousInitialNx(const Initial2xExperiment& experiment,bool continuousEnabled,bool researchActive,bool configurationPending){return continuousEnabled&&!researchActive&&!configurationPending&&experiment.completed&&!experiment.aborted;}
InitialNxConfigurationAction PendingInitialNxAction(InitialNxLifecycleState& state,const Initial2xExperiment& current){if(!state.configurationPending)return InitialNxConfigurationAction::None;if(InitialNxReplayActive(current))return InitialNxConfigurationAction::QueueAfterActiveReplay;if(Terminal(current)){ArchiveTerminal(state,current);return InitialNxConfigurationAction::ResetTerminalExperiment;}if(current.armed)return InitialNxConfigurationAction::ReplaceArmedPending;return InitialNxConfigurationAction::PrepareReady;}
InitialNxConfigurationAction ConfigureNextInitialNx(InitialNxLifecycleState& state,const Initial2xExperiment& current,uint32_t configuredInitialCalls){if(!ValidInitialCallCount(configuredInitialCalls))return InitialNxConfigurationAction::None;state.configuredInitialCalls=configuredInitialCalls;state.pendingNextInitialCalls=configuredInitialCalls;state.configurationPending=true;return PendingInitialNxAction(state,current);}
void MarkInitialNxConfigurationApplied(InitialNxLifecycleState& state){state.pendingNextInitialCalls=0;state.configurationPending=false;}
void ResetInitialNxLifecycleForNewSession(InitialNxLifecycleState& state){const uint32_t configured=ValidInitialCallCount(state.configuredInitialCalls)?state.configuredInitialCalls:DefaultInitialCalls;state={};state.configuredInitialCalls=configured;}
const char* InitialNxConfigurationActionName(InitialNxConfigurationAction action){switch(action){case InitialNxConfigurationAction::PrepareReady:return "PREPARE_READY";case InitialNxConfigurationAction::ReplaceArmedPending:return "REPLACE_ARMED_PENDING";case InitialNxConfigurationAction::QueueAfterActiveReplay:return "QUEUE_AFTER_ACTIVE_REPLAY";case InitialNxConfigurationAction::ResetTerminalExperiment:return "RESET_TERMINAL_EXPERIMENT";default:return "NONE";}}
}

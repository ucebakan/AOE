#pragma once
#include "model.hpp"
#include <filesystem>
#include <iosfwd>

namespace aoe {
inline constexpr char Initial2xLimitation[] = "Repeating the client-side initial 0x0209 call establishes only whether the client can execute and serialize the prepared operation the configured number of times. It does not by itself prove that a remote server accepts the operations as independent gameplay effects or damage instances.";
constexpr uint32_t MinInitialCalls=1,MaxInitialCalls=100,DefaultInitialCalls=2;
enum class InitialCallAction { Ignore, FirstAccepted, SecondObserved, Aborted };
enum class InitialReturnAction { Ignore, Redirect, Completed, Aborted };
bool ValidInitialCallCount(uint32_t value);
bool ParseInitialCallCount(const std::wstring& text,uint32_t& value);
Initial2xExperiment CreateInitial2xExperiment(const TargetInfo& target,bool producerObservationAvailable,uint32_t targetInitialCalls=DefaultInitialCalls);
bool Initial2xSessionMatches(const Initial2xExperiment& experiment,const TargetInfo& target);
InitialCallAction ObserveInitialCall(Initial2xExperiment& experiment,const InitialCallSnapshot& call);
InitialReturnAction ObserveInitialReturn(Initial2xExperiment& experiment,uint32_t threadId,uint64_t rsp,uint64_t r12,uint64_t r13,uint64_t r14,uint64_t r15,uint64_t rsi,uint64_t rdi);
void CommitInitialRedirect(Initial2xExperiment& experiment,bool success,const std::string& error={});
void AbortInitial2x(Initial2xExperiment& experiment,const std::string& reason);
void InvalidateInitial2xSession(Initial2xExperiment& experiment,const std::string& reason);
void RecordInitial2xProducer(Initial2xExperiment& experiment,bool readable,uint64_t operation,int64_t qpc);
std::string Initial2xStateName(const Initial2xExperiment& experiment);
std::string Initial2xDetails(const Initial2xExperiment& experiment);
void WriteInitial2xJson(std::ostream& out,const Initial2xExperiment& experiment);
bool ExportInitial2xJson(const std::filesystem::path& path,const Initial2xExperiment& experiment,std::string& error);
}

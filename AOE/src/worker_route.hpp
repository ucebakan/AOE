#pragma once
#include "model.hpp"
#include "operation_observer.hpp"

namespace aoe {
// Read-only ABI and ownership checks for the common worker entry.
bool ReadAoeCaster(const OperationRead& read,const TargetInfo& target,uint64_t game,uint64_t owner,uint64_t& localActor,uint32_t& casterId);
bool ResolveWorkerCall(const OperationRead& read,const TargetInfo& target,InitialCallSnapshot& call);
std::vector<uint64_t> WorkerBreakpointRvas(const RuntimeLayout& runtime);
}

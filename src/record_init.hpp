#pragma once
#include "tracer.hpp"
#include <ostream>

namespace aoe {
inline constexpr char RecordInitLimitation[]="ECX at +57E35 is the serialized uint32 written to record+34h. Runtime correlation is required before assigning gameplay units or duration semantics.";
void SnapshotRecordInit(HANDLE process,Event& event);
void ClassifyRecordInit(RecordInitSnapshot& snapshot);
void WriteRecordInitJson(std::ostream& out,const Capture& capture,const Event& event);
void WriteRecordInitSummaryJson(std::ostream& out,const Capture& capture);
std::string RecordInitDetails(const Capture& capture,const Event& event);
std::string RecordInitSummary(const Capture& capture);
}

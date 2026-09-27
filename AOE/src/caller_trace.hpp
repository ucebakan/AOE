#pragma once
#include "tracer.hpp"
#include <ostream>

namespace aoe {
inline constexpr char Caller020ALimitation[]="This trace identifies runtime callers of +7BB160 for operationWord0 0x020A. Call frequency alone does not prove scheduler ownership or damage mechanics.";
void ClassifyCaller020A(Event& event);
void SnapshotCaller020A(HANDLE process,Event& event);
void SnapshotCaller020AEntry(HANDLE process,const TargetInfo& target,Event& event);
std::vector<DirectCallsite> DirectCallsitesToCaller020A();
Caller020ASummary BuildCaller020ASummary(const Capture& capture);
void WriteCaller020AEventJson(std::ostream& out,const Event& event);
void WriteCaller020ASummaryJson(std::ostream& out,const Caller020ASummary& summary);
std::string Caller020AEventDetails(const Event& event);
std::string Caller020ASummaryText(const Caller020ASummary& summary,uint64_t moduleBase);
}

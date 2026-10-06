#pragma once
#include <cstddef>
#include <cstdint>
#include <functional>

namespace aoe {
enum class AoeSkillFamily { Unknown, Priest, Mage, Archer };
struct AoeSkillContract {
    AoeSkillFamily family=AoeSkillFamily::Unknown;
    uint16_t initial=0,periodic=0;
    const char* name="Use a normal supported AOE cast";
};
// Resource IDs are distinct from relocated code RVAs. Every family still
// requires the SHA-selected common anchors and its own live call/return proof.
AoeSkillContract SkillContract(AoeSkillFamily family);
AoeSkillFamily OperationSkillFamily(uint16_t word);
enum class OperationClassification { Unreadable,Initial0209,Periodic020A,Other };
struct OperationObservation {
    bool operationReadable=false;
    uint16_t operationWord=0;
    OperationClassification classification=OperationClassification::Unreadable;
    uint64_t address=0;
    uint32_t readError=0;
    AoeSkillFamily family=AoeSkillFamily::Unknown;
};
using OperationRead=std::function<bool(uint64_t,void*,size_t,uint32_t&)>;
OperationObservation ClassifyOperationWord(uint16_t word);
OperationObservation DecodeOperation(const OperationRead& read,uint64_t address);
const char* OperationClassificationName(OperationClassification value);
}

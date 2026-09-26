#pragma once
#include <cstddef>
#include <cstdint>
#include <functional>

namespace aoe {
enum class OperationClassification { Unreadable,Initial0209,Periodic020A,Other };
struct OperationObservation {
    bool operationReadable=false;
    uint16_t operationWord=0;
    OperationClassification classification=OperationClassification::Unreadable;
    uint64_t address=0;
    uint32_t readError=0;
};
using OperationRead=std::function<bool(uint64_t,void*,size_t,uint32_t&)>;
OperationObservation ClassifyOperationWord(uint16_t word);
OperationObservation DecodeOperation(const OperationRead& read,uint64_t address);
const char* OperationClassificationName(OperationClassification value);
}

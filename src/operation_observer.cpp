#include "operation_observer.hpp"

namespace aoe {
OperationObservation ClassifyOperationWord(uint16_t word){OperationObservation result;result.operationReadable=true;result.operationWord=word;result.classification=word==0x0209?OperationClassification::Initial0209:word==0x020A?OperationClassification::Periodic020A:OperationClassification::Other;return result;}
OperationObservation DecodeOperation(const OperationRead& read,uint64_t address){OperationObservation result;result.address=address;if(!address||!read||!read(address,&result.operationWord,sizeof(result.operationWord),result.readError))return result;result.operationReadable=true;result.classification=result.operationWord==0x0209?OperationClassification::Initial0209:result.operationWord==0x020A?OperationClassification::Periodic020A:OperationClassification::Other;return result;}
const char* OperationClassificationName(OperationClassification value){switch(value){case OperationClassification::Initial0209:return "Initial0209";case OperationClassification::Periodic020A:return "Periodic020A";case OperationClassification::Other:return "Other";default:return "Unreadable";}}
}

#include "operation_observer.hpp"

namespace aoe {
AoeSkillContract SkillContract(AoeSkillFamily family){
    switch(family){
    case AoeSkillFamily::Priest:return{family,521,522,"Priest / Shadow Thunderstorm"};
    case AoeSkillFamily::Mage:return{family,424,425,"Mage / Ice Rain"};
    case AoeSkillFamily::Archer:return{family,321,322,"Archer / Rain of Arrows"};
    default:return{};
    }
}
AoeSkillFamily OperationSkillFamily(uint16_t word){
    for(auto family:{AoeSkillFamily::Priest,AoeSkillFamily::Mage,AoeSkillFamily::Archer}){
        const auto skill=SkillContract(family);if(word==skill.initial||word==skill.periodic)return family;
    }
    return AoeSkillFamily::Unknown;
}
OperationObservation ClassifyOperationWord(uint16_t word){OperationObservation result;result.operationReadable=true;result.operationWord=word;result.family=OperationSkillFamily(word);const auto skill=SkillContract(result.family);result.classification=result.family==AoeSkillFamily::Unknown?OperationClassification::Other:word==skill.initial?OperationClassification::Initial0209:OperationClassification::Periodic020A;return result;}
OperationObservation DecodeOperation(const OperationRead& read,uint64_t address){uint16_t word=0;OperationObservation result;result.address=address;if(!address||!read||!read(address,&word,sizeof(word),result.readError))return result;result=ClassifyOperationWord(word);result.address=address;return result;}
const char* OperationClassificationName(OperationClassification value){switch(value){case OperationClassification::Initial0209:return "Initial";case OperationClassification::Periodic020A:return "Periodic";case OperationClassification::Other:return "Other";default:return "Unreadable";}}
}

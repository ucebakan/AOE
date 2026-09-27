#pragma once
#include "model.hpp"
#include <array>
#include <functional>

namespace aoe {
inline constexpr uint64_t TargetVectorOffset=0x660;
inline constexpr uint64_t TargetActorIdOffset=0x768;
inline constexpr uint64_t TargetActorTypeOffset=0x7E1;
inline constexpr size_t TargetRecordBytes=8;
inline constexpr size_t TargetMaximumEntries=32;
inline constexpr size_t TargetTreeTraversalLimit=128;
inline constexpr size_t DifferentialOperationRecordBytes=0x30;
inline constexpr size_t DifferentialEventLimit=128;
inline constexpr double SpatialMatchTolerance=0.75;

using InspectorRead=std::function<bool(uint64_t,void*,size_t,uint32_t&)>;

enum class TargetActorLookupStatus { NotAttempted,Resolved,InvalidKey,UnsupportedType,NotFound,ReadError,TraversalLimit };

struct TargetActorResolution {
    TargetActorLookupStatus status=TargetActorLookupStatus::NotAttempted;
    uint32_t error=0;
    uint64_t containerHeader=0,node=0,actorPointer=0;
    bool actorIdentityReadable=false,idMatches=false,typeMatches=false;
    uint32_t actorId=0;
    uint8_t actorType=0;
};

struct TargetRecord {
    uint32_t index=0,error=0;
    uint64_t slotAddress=0,recordPointer=0;
    bool pointerReadable=false,recordReadable=false;
    std::array<uint8_t,TargetRecordBytes> raw{};
    uint32_t actorId=0;
    uint8_t actorType=0;
    std::array<uint8_t,3> trailingBytes{};
    TargetActorResolution resolution;
};

struct TargetVectorCapture {
    uint64_t entityPointer=0,expectedAddress=0,addressFromR12=0;
    bool r12MatchesEntity660=false,headerReadable=false,structurallyValid=false;
    bool countWithinLimit=false,truncated=false;
    uint32_t error=0;
    uint64_t begin=0,end=0,capacity=0,count=0;
    std::string notes;
    std::vector<TargetRecord> records;
};

struct SpatialPoint {
    bool readable=false,finite=false,plausible=false;
    uint32_t error=0;
    std::array<uint32_t,3> raw{};
    std::array<float,3> value{};
    std::string source,confidence,reason;
};

struct ActorSpatialSnapshot {
    bool identityReadable=false,positionAvailable=false,provenanceValidated=false;
    uint64_t pointer=0;
    uint32_t actorId=0,error=0;
    uint8_t category=0;
    SpatialPoint position;
    std::string source,confidence,reason;
};

struct OperationSpatialFields {
    bool readable=false,finite=false,plausible=false;
    std::array<uint32_t,3> raw{};
    std::array<float,3> value{};
    uint32_t field24Raw=0;
};

struct SpatialSnapshot {
    ActorSpatialSnapshot player,selectedTarget;
};

enum class Field24Correlation { Unknown,MatchesActorId,MatchesActorType,MatchesPointerLow32,DoesNotMatch };
enum class SpatialClassification { InsufficientData,MatchesPlayerPosition,MatchesSelectedTargetPosition,MatchesNeither,Ambiguous };

struct CaptureSpatialComparison {
    uint64_t markerId=0;
    bool captured=false,operationAvailable=false,playerAvailable=false,targetAvailable=false;
    double operationToPlayer3d=0,operationToPlayerHorizontal=0,operationToTarget3d=0,operationToTargetHorizontal=0;
    Field24Correlation field24Correlation=Field24Correlation::Unknown;
};

struct SpatialAnalysis {
    SpatialClassification classification=SpatialClassification::InsufficientData;
    std::string reason,selectedTargetSource;
    std::vector<CaptureSpatialComparison> captures;
};

struct DifferentialActorProbe {
    uint32_t idOffset=0,typeOffset=0,actorId=0,error=0;
    uint8_t actorType=0,category=0;
    uint64_t actorPointer=0;
    bool resolved=false,categoryReadable=false;
    ActorSpatialSnapshot spatial;
};

struct TargetInspectorEvent {
    uint64_t sequence=0,markerId=0;
    int64_t qpc=0;
    double timestampMs=0;
    uint32_t threadId=0,operationReadError=0;
    uint64_t rcx=0,rdx=0,r8=0,r9=0,r12=0,r13=0,r14=0,r15=0,rsp=0;
    uint16_t operationWord0=0;
    bool operationReadable=false,initial0209=false,periodic020A=false;
    bool operationRecordReadable=false,ownerIdentityReadable=false,ownerCategoryReadable=false;
    uint32_t operationRecordError=0,ownerReadError=0,ownerActorId=0;
    uint8_t ownerCategory=0;
    std::array<uint8_t,DifferentialOperationRecordBytes> operationRecord{};
    std::vector<DifferentialActorProbe> actorProbes;
    OperationSpatialFields operationSpatial;
    SpatialSnapshot spatial;
    TargetVectorCapture targetVector;
};

struct ParsedInputCapture {
    uint64_t markerId=0,sequence=0,rip=0,rbp=0,operationRecordPointer=0,ownerPointer=0,gameContext=0,resolvedActorPointer=0,actorStatePointer=0;
    int64_t qpc=0;
    double timestampMs=0;
    uint32_t threadId=0,error=0,parsedActorId=0,outerField24=0,ownerActorId=0;
    uint16_t operationWord=0;
    uint8_t parsedActorType=0,modeByte=0,recordCount=0,ownerCategory=0;
    bool operationReadable=false,operation0209=false,recordReadable=false,ownerIdentityReadable=false,semanticInput=false;
    std::array<uint8_t,DifferentialOperationRecordBytes> operationRecord{};
};

enum class TargetInspectorPhase { Idle,WaitingForAoe,Capturing,Complete,Error };
enum class TargetCaptureSlotState { Empty,ArmedForNext0209,Captured };
struct TargetCaptureSlot {
    TargetCaptureSlotState state=TargetCaptureSlotState::Empty;
    uint64_t markerId=0,captureSequence=0;
};
struct TargetInspectorSession {
    TargetInspectorPhase phase=TargetInspectorPhase::Idle;
    uint32_t pid=0;
    uint64_t creationTime=0,moduleBase=0;
    std::string targetSha256,error;
    RuntimeLayout runtime;
    int64_t frequency=0,startQpc=0;
    std::array<TargetCaptureSlot,4> captureSlots{};
    std::vector<Marker> markers;
    std::vector<TargetInspectorEvent> events;
    std::vector<TargetInspectorEvent> periodicEvents;
    std::vector<ParsedInputCapture> parsedInputs;
    std::array<SpatialSnapshot,4> markerSpatial{};
};

struct DifferentialFieldComparison {
    uint32_t offset=0,width=0;
    std::string label,interpretation;
    std::array<bool,4> present{};
    std::array<uint64_t,4> values{};
    bool changed=false,allPairwiseDifferent=false;
};

struct DifferentialActorCandidate {
    uint32_t idOffset=0,typeOffset=0;
    std::array<bool,4> present{},resolved{},categoryReadable{};
    std::array<uint32_t,4> ids{};
    std::array<uint8_t,4> types{},categories{};
    std::array<uint64_t,4> actors{};
    bool idsChange=false,actorsChange=false,allMonsters=false,provenForAbc=false;
};

struct TargetRecordComparison {
    uint32_t index=0;
    bool present=false,rawChangedFromA=false,idChangedFromA=false,typeChangedFromA=false,actorChangedFromA=false;
    std::array<uint8_t,TargetRecordBytes> raw{};
    uint32_t actorId=0;
    uint8_t actorType=0;
    uint64_t actorPointer=0;
};

struct TargetCaptureComparison {
    uint64_t markerId=0;
    bool captured=false,countChangedFromA=false;
    int64_t count=-1;
    std::vector<TargetRecordComparison> records;
};

bool IsCanonicalUserPointer(uint64_t value);
bool InitialNxConflictsWithTargetInspector(const Initial2xExperiment& experiment);
bool ReleaseInitialNxForTargetInspector(Initial2xExperiment& experiment);
TargetActorResolution ResolveTargetActor(const InspectorRead& read,const RuntimeLayout& layout,uint64_t context,uint32_t actorId,uint8_t actorType);
TargetVectorCapture InspectPrimaryTargetVector(const InspectorRead& read,const RuntimeLayout& layout,uint64_t context,uint64_t entity,uint64_t r12);
TargetInspectorEvent CaptureTargetInspectorEvent(const InspectorRead& read,const RuntimeLayout& layout,const TargetInspectorEvent& registers);
SpatialPoint CaptureSpatialPoint(const InspectorRead& read,uint64_t actorPointer,uint64_t positionOffset);
ActorSpatialSnapshot CaptureActorSpatial(const InspectorRead& read,const RuntimeLayout& layout,uint64_t actorPointer,const std::string& source,const std::string& confidence);
ActorSpatialSnapshot CaptureSelectedTargetSpatial(const InspectorRead& read,const RuntimeLayout& layout,uint64_t gameContext,const std::string& source);
OperationSpatialFields DecodeOperationSpatialFields(const std::array<uint8_t,DifferentialOperationRecordBytes>& record,bool readable);
SpatialAnalysis AnalyzeSpatialProvenance(const TargetInspectorSession& session);
const char* SpatialClassificationName(SpatialClassification value);
const char* Field24CorrelationName(Field24Correlation value);
ParsedInputCapture CaptureParsedInput(const InspectorRead& read,const RuntimeLayout& layout,const ParsedInputCapture& registers);
bool RecordDifferentialEvent(TargetInspectorSession& session,TargetInspectorEvent event);
bool RecordParsedInput(TargetInspectorSession& session,ParsedInputCapture capture);
bool MarkerHasInitial0209(const TargetInspectorSession& session,uint64_t markerId);
bool CanMarkNextTargetCapture(const TargetInspectorSession& session);
bool ArmNextTargetCapture(TargetInspectorSession& session,const Marker& marker,std::string& error,const SpatialSnapshot* markerSpatial=nullptr);
bool CancelCurrentTargetCapture(TargetInspectorSession& session,std::string& error);
int ArmedTargetCaptureSlot(const TargetInspectorSession& session);
int NextTargetCaptureSlot(const TargetInspectorSession& session);
const char* TargetCaptureSlotStateName(TargetCaptureSlotState state);
std::vector<DifferentialFieldComparison> CompareOperationFields(const TargetInspectorSession& session);
std::vector<DifferentialActorCandidate> CompareActorCandidates(const TargetInspectorSession& session);
std::vector<TargetCaptureComparison> CompareTargetCaptures(const TargetInspectorSession& session);
const char* TargetActorLookupStatusName(TargetActorLookupStatus status);
const char* TargetActorTypeName(uint8_t type);
std::string TargetRecordRawHex(const TargetRecord& record);
const char* TargetInspectorStateName(TargetInspectorPhase phase);
void InvalidateTargetInspector(TargetInspectorSession& session,const std::string& reason);
bool ExportTargetInspectorJson(const std::filesystem::path& path,const TargetInspectorSession& session,std::string& error);
}

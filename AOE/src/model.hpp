#pragma once
#include "operation_observer.hpp"
#include <cstdint>
#include <array>
#include <filesystem>
#include <string>
#include <vector>

namespace aoe {
constexpr int MinCaptureSeconds=5,MaxCaptureSeconds=30,DefaultCaptureSeconds=15;
bool ValidCaptureDuration(double seconds);
bool ParseCaptureDuration(const std::wstring& text,int& seconds);
inline constexpr wchar_t TargetPath[] = L"C:\\Games\\4Unity\\TClient.exe";
constexpr uint64_t SendRva = 0xA129F0;
constexpr uint64_t ProducerRva = 0x99240, ProducerCallerRva = 0x7BB4BC;
constexpr uint64_t RecordInitRva = 0x57E35;
constexpr uint64_t BudgetWriteRva = 0x854049;
constexpr uint64_t Caller020ARva = 0x7BB160,Known853B10ReturnRva=0x85404E;
constexpr uint64_t TickThresholdRva=0x853C43,TickSubtractRva=0x8540E7,TickRepeatRva=0x8540F2;
constexpr uint64_t InitialPrepareRva=0x7F0900,InitialCallRva=0x7F091D,InitialReturnRva=0x7F0922;
enum class TraceMode { Queue, Producer, RecordInit, Caller020A, BudgetWrite, Tick100, Initial2x, TargetInspector, TargetWriter };
inline const char* ModeName(TraceMode mode){switch(mode){case TraceMode::Producer:return "Producer 99240";case TraceMode::RecordInit:return "Record Init 57E35";case TraceMode::Caller020A:return "0x020A Caller 7BB160";case TraceMode::BudgetWrite:return "AOE Budget Write 854049";case TraceMode::Tick100:return "AOE Tick 100ms";case TraceMode::Initial2x:return "AOE Initial Nx";case TraceMode::TargetInspector:return "AOE Target Differential Inspector";case TraceMode::TargetWriter:return "Target Writer Runtime Inspector";default:return "Queue A129F0";}}
inline constexpr uint64_t TraceRva(TraceMode mode){switch(mode){case TraceMode::Producer:case TraceMode::Tick100:return ProducerRva;case TraceMode::RecordInit:return RecordInitRva;case TraceMode::Caller020A:return Caller020ARva;case TraceMode::BudgetWrite:return BudgetWriteRva;case TraceMode::Initial2x:case TraceMode::TargetInspector:return InitialCallRva;default:return SendRva;}}
inline constexpr int DefaultCaptureSecondsForMode(TraceMode mode){return mode==TraceMode::RecordInit?10:(mode==TraceMode::Caller020A||mode==TraceMode::BudgetWrite)?12:DefaultCaptureSeconds;}
inline constexpr bool ResolveTraceAddress(uint64_t base,uint64_t imageSize,uint64_t rva,uint64_t& address){if(!base||rva>=imageSize||base>UINT64_MAX-rva)return false;address=base+rva;return true;}
inline constexpr char ApprovedHash[] = "CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E";
inline constexpr char PreviousPatchHash[] = "D0ECBBA10685D94E7CA632D6A9CEB9B3C2C420D609DB7CC4243AFF2F493A62C1";
inline constexpr char CurrentPatchHash[] = "FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7";
enum class X64RegisterId : uint8_t { Rax, Rcx, Rdx, Rbx, Rsp, Rbp, Rsi, Rdi, R8, R9, R10, R11, R12, R13, R14, R15, Invalid=0xFF };
inline const char* X64RegisterName(X64RegisterId value){switch(value){case X64RegisterId::Rax:return "RAX";case X64RegisterId::Rcx:return "RCX";case X64RegisterId::Rdx:return "RDX";case X64RegisterId::Rbx:return "RBX";case X64RegisterId::Rsp:return "RSP";case X64RegisterId::Rbp:return "RBP";case X64RegisterId::Rsi:return "RSI";case X64RegisterId::Rdi:return "RDI";case X64RegisterId::R8:return "R8";case X64RegisterId::R9:return "R9";case X64RegisterId::R10:return "R10";case X64RegisterId::R11:return "R11";case X64RegisterId::R12:return "R12";case X64RegisterId::R13:return "R13";case X64RegisterId::R14:return "R14";case X64RegisterId::R15:return "R15";default:return "INVALID";}}
struct AoeOwnerGetter {
    uint64_t slotRva=0,methodRva=0;
    uint32_t fieldOffset=0;
    std::vector<uint8_t> bytes;
};
struct RuntimeLayout {
    // Ephemeral read-only probe anchor; never saved as a gameplay capability.
    uint64_t classProbeLookupRva=0;
    bool classProbeWorkerEntry=false;
    uint64_t alternateInitialPrepRva=0,alternateInitialCallRva=0,alternateInitialReturnRva=0;
    std::vector<uint8_t> alternateInitialPrepBytes,alternateInitialCallBytes,alternateInitialReturnBytes;
    bool workerEntryCoverage=false;
    std::vector<AoeOwnerGetter> ownerGetters;
    uint64_t initialPrepRva=0,initialCallRva=0,initialReturnRva=0,sharedWorkerRva=0,producerRva=0;
    uint64_t producerCallRva=0,typedActorLookupRva=0,targetBuilderVectorRva=0,differentialParsedRva=0,idSerializationRva=0,typeSerializationRva=0;
    uint64_t targetVectorOffset=0,actorIdOffset=0,actorTypeOffset=0,actorEligibilityOffset=0,actorStatusOffset=0,localActorOffset=0,actorPositionOffset=0;
    uint64_t actorWorldLinkOffset=0,actorWorldLinkOwnerOffset=0,actorWorldPositionOffset=0;
    uint64_t gameContextPointerRva=0,selectedTargetOffset=0,selectedTargetWriterRva=0;
    std::vector<uint64_t> selectedTargetReaderRvas;
    std::array<uint64_t,20> actorTreeOffsets{};
    size_t targetRecordBytes=0,targetMaximumEntries=0;
    uint64_t visualReaderRva=0,visualLookupRva=0,visualFieldOffset=0;
    X64RegisterId visualBaseRegister=X64RegisterId::Invalid;
    bool targetInspectorAvailable=false,initialNxAvailable=false,liveValidationRequired=false,visualSuppressionAvailable=false;
    bool aoeLocatorRequired=false;
    std::string initialNxUnavailableReason;
    std::vector<uint8_t> initialPrepBytes,initialCallBytes,initialReturnBytes,sharedWorkerBytes,producerBytes,typedActorLookupBytes,targetBuilderVectorBytes,differentialParsedBytes,idSerializationBytes,typeSerializationBytes,producerCallBytes,selectedTargetWriterBytes,visualReaderBytes;
};
struct ExecutableRange { uint32_t rva=0,size=0; };
struct ImageInfo {
    std::wstring path;
    std::string sha256;
    uint32_t timestamp=0,imageSize=0;
    uint64_t fileSize=0;
    bool amd64=false,traceExecutable=false,functionEntry=false;
    std::vector<uint8_t> entryBytes;
    std::vector<ExecutableRange> executableRanges;
};
struct TargetInfo {
    TraceMode mode=TraceMode::Queue;
    uint32_t pid=0;
    uint64_t creationTime=0,base=0,size=0,traceAddress=0,traceRva=SendRva;
    ImageInfo image;
    RuntimeLayout runtime;
    bool verified=false;
};
struct StackCandidate { uint32_t stackOffset=0;uint64_t address=0,rva=0;bool directCallPrecedes=false; };
struct ProducerArgument {
    unsigned number=0,stackOffset=0,width=0;
    std::string type,source;
    bool readable=false;
    uint64_t bits=0;
    std::vector<uint8_t> raw;
};
struct VectorElement { uint64_t pointer=0;bool pointerReadable=false,recordReadable=false;uint32_t error=0;std::vector<uint8_t> bytes; };
struct ProducerSnapshot {
    bool captured=false,stackReadable=false,expectedCaller=false,vectorHeaderReadable=false,vectorValid=false;
    uint32_t stackError=0,vectorError=0;
    uint64_t vectorPointer=0,begin=0,end=0,capacity=0,elementCount=0;
    std::string vectorState="not captured";
    std::vector<uint8_t> rawStack,vectorHeader;
    std::vector<ProducerArgument> arguments;
    std::vector<VectorElement> elements;
};
struct RecordRead {
    bool readable=false;
    uint32_t error=0,width=0;
    uint64_t value=0;
    std::vector<uint8_t> raw;
};
struct Caller020ASnapshot {
    bool captured=false,operation0209=false,operation020A=false,known853B10Path=false;
    uint64_t operationPointer=0;
    RecordRead operationWord0;
};
struct RecordInitSnapshot {
    bool captured=false,linked020A=false,primary0209=false,primary0209Linked020A=false;
    uint64_t recordPointer=0;
    uint32_t ecxNew34=0;
    RecordRead old34,record64,record90,record98,primaryPointer,linkedPointer,rbpMinus7C;
    RecordRead primaryWord0,primaryField24,linkedWord0,linkedField6C;
};
struct BudgetExperiment {
    bool armed=false,active=false,writeClaimed=false,writePerformed=false,completed=false,breakpointRemoved=false;
    uint64_t totalBreakpointHits=0,readableRecords=0,linked020AMatches=0,linked020AAnd0246Matches=0;
    uint64_t recordPointer=0,linkedPointer=0,moduleBase=0,breakpointAddress=0;
    uint16_t linkedWord0=0;
    uint32_t record64=0;
    uint8_t record98=0;
    uint32_t old34=0,new34=0;
    double timestampMs=0;
    uint64_t breakpointRva=BudgetWriteRva;
    std::string error;
};
struct TickPatchTarget {
    uint64_t rva=0,resolvedAddress=0;
    bool validationPassed=false,patchReadbackVerified=false,restoreReadbackVerified=false;
    std::vector<uint8_t> originalBytes,intendedPatchedBytes,actualPatchedReadback,actualRestoredReadback;
};
struct TickPatchExperimentState {
    int originalInterval=1000,patchedInterval=100;
    bool armed=false,validationPassed=false,patchApplied=false,patchRestored=false,active=false,restoreRequired=false,explicitRestoreAttempted=false;
    bool producerObservationAvailable=false;
    uint32_t pid=0;
    uint64_t creationTime=0,moduleBase=0,producer0209Count=0,producer020ACount=0,producerOtherCount=0;
    int64_t armQpc=0,restoreQpc=0,frequency=0;
    double durationMs=0;
    std::string imageSha256,armTimestamp,restoreTimestamp,failureReason;
    std::vector<double> producer020ATimestampsMs,producer020ADeltasMs;
    std::vector<TickPatchTarget> targets;
};
using TickExperiment=TickPatchExperimentState;
enum class Initial2xPhase { NotArmed, Waiting0209, WaitingFirstReturn, ReplayingSecondCall, Completed, Aborted };
struct InitialCallSnapshot {
    bool observed=false,operationReadable=false,operation0209=false;
    uint32_t threadId=0;
    int64_t qpc=0;
    double timestampMs=0;
    uint64_t rip=0,rsp=0,r12=0,r13=0,r14=0,r15=0,rsi=0,rdi=0,rcx=0,rdx=0,r8=0,r9=0;
    uint16_t operationWord0=0;
    uint64_t prepRva=0,returnRva=0,rbp=0,localActor=0;
    uint32_t casterId=0;
    bool ownershipRequired=false,ownershipVerified=false;
};
struct InitialReturnValidation {
    bool performed=false,rspMatched=false,r12Matched=false,r13Matched=false,r14Matched=false,r15Matched=false,rsiMatched=false,rdiMatched=false;
};
struct Initial2xExperiment {
    AoeSkillFamily skillFamily=AoeSkillFamily::Priest;
    uint64_t expectedReturnRva=0;
    Initial2xPhase phase=Initial2xPhase::NotArmed;
    bool armed=false,completed=false,aborted=false,repeatPending=false,replayInProgress=false;
    bool redirectClaimed=false,ripRedirectPerformed=false,producerObservationAvailable=false,awaitingReplayedCall=false;
    uint32_t pid=0,firstThreadId=0,redirectCount=0,targetInitialCalls=0,initialCallsObserved=0,redirectsPerformed=0;
    uint32_t returnValidationsPassed=0,returnValidationsFailed=0;
    uint64_t creationTime=0,moduleBase=0,redirectTargetRva=InitialPrepareRva;
    int64_t armQpc=0,frequency=0;
    std::string imageSha256,armTimestamp,abortReason;
    InitialCallSnapshot firstInitialCall,secondInitialCall;
    InitialReturnValidation returnValidation;
    double initialCallDeltaMs=0;
    std::vector<double> initialCallTimestamps,initialCallIntervalsMs;
    uint64_t producer0209Count=0,producer020ACount=0,producerOtherCount=0;
    std::vector<double> producer0209Timestamps,producer020ATimestamps,producer020ADeltas;
};
struct FieldStability {
    std::string field,type,first,last;
    uint64_t observations=0,distinct=0,missing=0;
    bool constant=false;
    std::vector<std::string> sequence,rawValues,deltas;
};
struct Event {
    uint64_t rip=0;
    ProducerSnapshot producer;
    RecordInitSnapshot recordInit;
    Caller020ASnapshot caller020A;
    uint64_t sequence=0;
    int64_t qpc=0;
    double ms=0,deltaMs=0,handlerMs=0;
    uint32_t threadId=0;
    uint64_t rcx=0,rdx=0,r8=0,r9=0,rbx=0,rbp=0,rsp=0,returnAddress=0;
    uint32_t packetLength=0;
    uint64_t returnRva=0;
    bool returnInModule=false,returnReadable=false,bufferReadable=false,fullPacket=false;
    uint32_t bufferReadError=0,returnReadError=0;
    std::vector<uint8_t> bytes;
    std::string signature; // stable SHA-256 of captured bytes, not necessarily full packet
    std::vector<StackCandidate> stackCandidates; // raw stack scan, not an unwind
};
enum class Label { Idle, Aoe };
struct Marker { uint64_t id=0;std::string label;int64_t qpc=0;double ms=0; };
struct Capture {
    uint64_t id=0;
    Label label=Label::Idle;
    std::string utc,stopReason;
    TargetInfo target;
    int64_t frequency=0,startQpc=0,endQpc=0;
    double requestedSeconds=DefaultCaptureSeconds,durationMs=0;
    bool complete=false,synthetic=false;
    uint64_t droppedEvents=0;
    BudgetExperiment experiment;
    TickExperiment tickExperiment;
    std::vector<Event> events;
    std::vector<Marker> markers;
};
struct ByteStability {
    uint32_t offset=0,samples=0,distinctValues=0;
    bool constant=false;uint8_t value=0;double entropyBits=0;
};
struct SequenceHypothesis {
    uint64_t periodicCount=0,totalPossibleCount=0,markerId=0;
    int64_t possibleInitialEventIndex=-1;
    uint32_t initialCandidateCount=0;
    double spanMs=0;
    bool spanNear8Seconds=false,consistentOnePlusEight=false;
    std::string assessment;
};
struct Group {
    std::string kind,key,signature,pattern,notes;
    uint32_t packetSize=0;
    uint64_t callerRva=0;
    bool callerKnown=false,periodicNear1s=false,nearNine=false;
    uint64_t count=0,idleCount=0;
    double meanDeltaMs=0,medianDeltaMs=0,minDeltaMs=0,maxDeltaMs=0,coefficientVariation=0;
    double near1sFraction=0,aoeRate=0,idleRate=0,score=0;
    std::vector<double> timestampsMs,deltasMs;
    std::vector<uint32_t> eventIndices,stablePositions;
    std::vector<uint8_t> stableValues;
    std::vector<std::string> reasons;
    std::string fingerprint,category,status,truncationAssessment,maskedSignature;
    std::vector<uint64_t> fingerprintRvas;
    double standardDeviationMs=0,near100Fraction=0,near250Fraction=0;
    double firstMs=0,lastMs=0,expectedNextTimestampMs=0,expectedPreviousTimestampMs=0;
    bool strongTimingCandidate=false,likelyTruncated=false,possibleEarlierEvents=false;
    bool aoeOnly=false,background=false,maskAvailable=false;
    uint64_t maskSamples=0,maskedIdleCount=0;
    std::vector<ByteStability> byteStability;
    SequenceHypothesis hypothesis;
};
struct MarkerRelation {bool available=false;double firstRelativeMs=0,nearestRelativeMs=0;uint64_t firstId=0,nearestId=0;};
struct Diagnostic78 {
    std::string summary;
    uint64_t idleCount=0;
    double captureRate=0,idleRate=0;
    std::vector<uint32_t> eventIndices;
};
struct DirectCallsite {uint64_t callRva=0,returnRva=0;};
struct Caller020AGroup {
    uint64_t returnRva=0,returnAddress=0,count=0;
    double firstTimestampMs=0,lastTimestampMs=0,meanDeltaMs=0,medianDeltaMs=0;
    bool near1000msPeriodic=false,known853B10Path=false;
    std::vector<double> timestampsMs,deltasMs;
};
struct Caller020ASummary {
    uint64_t operation020ACount=0;
    bool dominantPeriodicAvailable=false;
    uint64_t dominantReturnRva=0,dominantReturnAddress=0;
    std::vector<Caller020AGroup> callerGroups;
    std::vector<DirectCallsite> directCallsites;
};
struct Analysis {
    std::vector<FieldStability> producerFields;
    std::string summary,idleReference;
    bool idleCompatible=false;
    std::vector<Group> groups;
    Diagnostic78 size78;
    Caller020ASummary caller020A;
};
std::string Hex(uint64_t n);
std::string BytesHex(const std::vector<uint8_t>& bytes,size_t maxBytes=128);
std::string Utf8(const std::wstring& text);
std::wstring Wide(const std::string& text);
std::string UtcNow();
std::string LabelName(Label label);
int64_t QpcNow();
int64_t QpcFrequency();
std::string Sha256(const uint8_t* data,size_t size);
void InitLog(const std::filesystem::path& root);
void Log(const std::string& event,const std::string& detail={});
std::string WinError(const std::string& operation,uint32_t error);
}

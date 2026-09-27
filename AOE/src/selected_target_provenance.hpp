#pragma once
#include "target_inspector.hpp"
#include <filesystem>

namespace aoe {
enum class SelectedTargetPhase { Stopped, Running, Captured, Error };
enum class SelectedTargetClassification { ProvenDirectPointer, ProvenIdTypePair, ProvenWrapperReference, CandidateOnly, InsufficientData };

struct SelectedTargetCapture {
    std::string label;
    std::string utc;
    uint64_t timestampMs=0,storageAddress=0,gameContext=0,rawValue=0,actorPointer=0;
    uint32_t actorId=0,error=0;
    uint8_t actorType=0,category=0;
    bool storageReadable=false,nullSelection=false,identityReadable=false,categoryMonster=false,resolverMatched=false,positionAvailable=false;
    SpatialPoint position;
    std::string validation;
};

struct SelectedTargetConclusion {
    SelectedTargetClassification classification=SelectedTargetClassification::InsufficientData;
    std::string confidence="none",reason="Capture A, B, and C from three different selected monsters.";
};

struct SelectedTargetProvenanceSession {
    SelectedTargetPhase phase=SelectedTargetPhase::Stopped;
    uint32_t pid=0;
    uint64_t creationTime=0,moduleBase=0,startTickMs=0;
    std::string targetSha256,error;
    RuntimeLayout runtime;
    std::vector<SelectedTargetCapture> captures;
};

SelectedTargetCapture CaptureSelectedTarget(const InspectorRead& read,const RuntimeLayout& layout,uint64_t moduleBase,const std::string& label,uint64_t timestampMs);
bool RecordSelectedTargetCapture(SelectedTargetProvenanceSession& session,SelectedTargetCapture capture,std::string& error);
SelectedTargetConclusion AnalyzeSelectedTargetProvenance(const SelectedTargetProvenanceSession& session);
const char* SelectedTargetPhaseName(SelectedTargetPhase value);
const char* SelectedTargetClassificationName(SelectedTargetClassification value);
std::string FormatHardwareBreakpointDiagnostic(uint32_t threadId,const std::array<uint64_t,4>& dr,uint64_t dr7,unsigned required,unsigned available);
bool ExportSelectedTargetProvenanceJson(const std::filesystem::path& path,const SelectedTargetProvenanceSession& session,std::string& error);
}

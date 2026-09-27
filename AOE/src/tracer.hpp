#pragma once
#include "model.hpp"
#include "profile.hpp"
#include "target_inspector.hpp"
#include "target_writer_runtime.hpp"
#include "live_validation.hpp"
#include "visual_suppression.hpp"
#include <Windows.h>
#include <atomic>
#include <memory>
#include <mutex>
#include <thread>

namespace aoe {
bool InspectImage(const std::filesystem::path& path,uint64_t rva,ImageInfo& info,std::string& error);
std::vector<uint32_t> FindProcesses(const std::wstring& name=L"TClient.exe");
bool DiscoverTarget(TargetInfo& info,std::string& error);
bool SafeRead(HANDLE process,uint64_t address,void* destination,size_t size,uint32_t& error);
enum class Phase { Detached, Attaching, Attached, Detaching, CleanupBlocked, Failed };
struct BreakpointCoverage {
    uint32_t threadId=0;
    uint64_t rva=0,address=0;
    unsigned drSlot=0;
    bool validation=false,replay=false,producer=false,visual=false,inspector=false,writer=false;
};
struct Status {
    Phase phase=Phase::Detached;
    TargetInfo target;
    std::string message="Not attached",lastError;
    std::string profileIdentity;
    uint64_t totalHits=0,captureId=0,capturedEvents=0;
    uint32_t threadCount=0;
    bool capturing=false;
    bool tickProducerObservationAvailable=false;
    bool initialBreakpointsReady=false;
    bool processHandleOpen=false,debuggerAttached=false,debugRegisterBookkeeping=false;
    bool targetInspectorPrepared=false,targetInspectorTransitionPending=false;
    std::string targetInspectorTransitionError;
    bool targetWriterTransitionPending=false;
    std::string targetWriterTransitionError;
    uint64_t processHandleValue=0;
    uint32_t initialBreakpointThreadCount=0,activeBreakpointSlots=0;
    std::vector<uint32_t> debuggerThreadIds;
    std::vector<uint64_t> initialBreakpointAddresses;
    std::vector<BreakpointCoverage> breakpointCoverage;
    Label label=Label::Idle;
    double elapsedMs=0,pauseMs=0,requestedSeconds=DefaultCaptureSeconds,remainingMs=0;
    BudgetExperiment experiment;
    TickExperiment tickExperiment;
    Initial2xExperiment initial2x;
    LiveValidationState liveValidation;
    TargetInspectorSession targetInspector;
    TargetWriterRuntimeSession targetWriter;
    VisualRuntimeState visual;
};
class Tracer {
public:
    Tracer()=default;
    ~Tracer();
    bool attach(const BuildProfile& profile,std::string& error,TraceMode mode=TraceMode::Queue); // invoked by Attach UI; never automatic on startup
    void detach(); // request; poll state until fully restored/detached before closing UI
    bool startCapture(Label label,double seconds,std::string& error);
    bool armBudgetWrite(std::string& error);
    bool armTick100(std::string& error);
    bool restoreTick100(std::string& error);
    bool resetTickExperiment(std::string& error);
    bool armInitial2x(uint32_t targetInitialCalls,std::string& error);
    bool prepareInitialExperiment(std::string& error);
    bool resetInitialExperiment(std::string& error);
    bool requestVisualCapture(std::string& error);
    bool cancelVisualCapture(std::string& error);
    bool setVisualSuppressionActive(bool active,std::string& error);
    bool prepareTargetInspector(std::string& error);
    bool startTargetInspector(std::string& error);
    bool markTargetInspector(Marker& marker,std::string& error);
    bool cancelTargetInspectorMarker(std::string& error);
    bool completeTargetInspector(std::string& error);
    bool startTargetWriter(std::string& error);
    bool setTargetWriterPhase(TargetWriterPhase phase,std::string& error);
    bool clearTargetWriterEvents(std::string& error);
    bool completeTargetWriter(std::string& error);
    void stopCapture();
    bool markAoeCast(Marker& marker,std::string& error);
    std::vector<Marker> markers(uint64_t captureId) const;
    Status status() const;
    std::vector<Event> eventsSince(uint64_t captureId,size_t first) const;
    std::vector<std::shared_ptr<Capture>> takeCompleted();
    // Fixture-only seam; exact image/session/RVA verification still applies. Never calls game input.
    bool attachFixture(uint32_t pid,const std::filesystem::path& image,uint64_t rva,std::string& error,TraceMode mode=TraceMode::Queue);
private:
    bool begin(uint32_t pid,std::filesystem::path path,uint64_t rva,bool fixture,std::string& error,TraceMode mode,BuildProfile profile={});
    void run(uint32_t pid,std::filesystem::path path,uint64_t rva,bool fixture,TraceMode mode,BuildProfile profile);
    void finishCaptureLocked(const std::string& reason,bool complete,int64_t end);
    mutable std::mutex mutex_;
    std::thread worker_;
    std::atomic<bool> detachRequested_{false},stopCaptureRequested_{false},invalidateLiveValidationOnDetach_{false},preserveLiveValidationOnDetach_{false};
    std::atomic<int> tickRequest_{0},initialRequest_{0},inspectorRequest_{0},visualRequest_{0};
    Status status_;
    LiveValidationState liveValidation_;
    std::shared_ptr<Capture> capture_;
    std::vector<std::shared_ptr<Capture>> completed_;
    uint64_t nextCaptureId_=1;
};
}

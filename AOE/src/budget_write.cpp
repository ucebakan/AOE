#include "budget_write.hpp"
#include <cstring>
#include <iomanip>
#include <sstream>

namespace aoe {
namespace {
bool Plausible(uint64_t pointer,size_t size){return pointer>=0x10000&&pointer<=0x00007FFFFFFFFFFFull&&size<=0x0000800000000000ull-pointer&&(pointer&7)==0;}
template<class T> bool Read(HANDLE process,uint64_t address,T& value){uint32_t error=0;return SafeRead(process,address,&value,sizeof(value),error);}
std::string Quote(const std::string& text){std::ostringstream out;out<<'"';for(unsigned char c:text){if(c=='"'||c=='\\')out<<'\\'<<char(c);else if(c=='\n')out<<"\\n";else if(c=='\r')out<<"\\r";else if(c=='\t')out<<"\\t";else if(c<0x20)out<<"\\u"<<std::hex<<std::setw(4)<<std::setfill('0')<<unsigned(c)<<std::dec;else out<<char(c);}out<<'"';return out.str();}
const char* Bool(bool value){return value?"true":"false";}
}

BudgetProbe ProbeBudgetWrite(HANDLE process,uint64_t recordPointer){
    BudgetProbe probe;probe.recordPointer=recordPointer;probe.recordPlausible=Plausible(recordPointer,0xA0);if(!probe.recordPlausible)return probe;
    probe.linkedPointerReadable=Read(process,recordPointer+0x80,probe.linkedPointer);
    probe.record64Readable=Read(process,recordPointer+0x64,probe.record64);
    probe.record98Readable=Read(process,recordPointer+0x98,probe.record98);
    probe.record34Readable=Read(process,recordPointer+0x34,probe.old34);
    probe.recordReadable=probe.linkedPointerReadable&&probe.record64Readable&&probe.record98Readable&&probe.record34Readable;
    if(probe.linkedPointerReadable&&probe.linkedPointer&&Plausible(probe.linkedPointer,2))probe.linkedWordReadable=Read(process,probe.linkedPointer,probe.linkedWord0);
    return probe;
}

bool IsBudgetWriteMatch(const BudgetProbe& probe){return probe.recordPlausible&&probe.recordReadable&&probe.linkedPointerReadable&&probe.linkedPointer!=0&&probe.linkedWordReadable&&probe.linkedWord0==0x020A&&probe.record64==0x0246&&probe.record98==0&&probe.old34>2000;}

bool ArmBudgetExperiment(BudgetExperiment& experiment,const TargetInfo& attached,const TargetInfo& captureTarget,std::string& error){
    const bool same=attached.verified&&attached.mode==TraceMode::BudgetWrite&&attached.traceRva==BudgetWriteRva&&attached.pid&&attached.pid==captureTarget.pid&&attached.creationTime&&attached.creationTime==captureTarget.creationTime&&attached.base==captureTarget.base&&attached.size==captureTarget.size&&attached.traceAddress==captureTarget.traceAddress&&attached.image.sha256==ApprovedHash&&captureTarget.image.sha256==ApprovedHash;
    if(!same){experiment.active=false;error="Process/session or verified module identity changed; re-attach and re-arm.";return false;}
    if(experiment.active){error="The one-shot write is already armed.";return false;}
    if(experiment.writeClaimed||experiment.writePerformed){error="This arm has already been consumed; start a new experiment and explicitly arm again.";return false;}
    experiment.armed=true;experiment.active=true;experiment.completed=false;experiment.breakpointRva=BudgetWriteRva;experiment.moduleBase=attached.base;experiment.breakpointAddress=attached.traceAddress;experiment.error.clear();error.clear();return true;
}

bool ClaimBudgetWrite(BudgetExperiment& experiment,const BudgetProbe& probe,const TargetInfo& target,double timestampMs,BudgetWritePlan& plan){
    ++experiment.totalBreakpointHits;if(probe.recordReadable)++experiment.readableRecords;const bool linked020A=probe.linkedWordReadable&&probe.linkedWord0==0x020A;if(linked020A)++experiment.linked020AMatches;if(linked020A&&probe.record64Readable&&probe.record64==0x0246)++experiment.linked020AAnd0246Matches;
    if(!experiment.active||experiment.writeClaimed||experiment.writePerformed||!IsBudgetWriteMatch(probe))return false;
    experiment.active=false;experiment.writeClaimed=true;plan.recordPointer=probe.recordPointer;plan.linkedPointer=probe.linkedPointer;plan.address=probe.recordPointer+0x34;plan.linkedWord0=probe.linkedWord0;plan.record64=probe.record64;plan.record98=probe.record98;plan.old34=probe.old34;plan.new34=probe.old34/2;plan.timestampMs=timestampMs;plan.moduleBase=target.base;plan.breakpointAddress=target.traceAddress;return true;
}

void FinishBudgetWrite(BudgetExperiment& experiment,const BudgetWritePlan& plan,bool success,const std::string& error){experiment.active=false;experiment.completed=true;experiment.error=error;if(!success)return;experiment.writePerformed=true;experiment.recordPointer=plan.recordPointer;experiment.linkedPointer=plan.linkedPointer;experiment.linkedWord0=plan.linkedWord0;experiment.record64=plan.record64;experiment.record98=plan.record98;experiment.old34=plan.old34;experiment.new34=plan.new34;experiment.timestampMs=plan.timestampMs;experiment.moduleBase=plan.moduleBase;experiment.breakpointRva=BudgetWriteRva;experiment.breakpointAddress=plan.breakpointAddress;}
void MarkBudgetBreakpointRemoved(BudgetExperiment& experiment){if(experiment.writePerformed)experiment.breakpointRemoved=true;}

void WriteBudgetExperimentJson(std::ostream& out,const Capture& capture){const auto& e=capture.experiment;out<<"{\"mode\":"<<Quote(ModeName(capture.target.mode))<<",\"armed\":"<<Bool(e.armed)<<",\"writePerformed\":"<<Bool(e.writePerformed)<<",\"totalBreakpointHits\":"<<e.totalBreakpointHits<<",\"readableRecords\":"<<e.readableRecords<<",\"linked020AMatches\":"<<e.linked020AMatches<<",\"linked020AAnd0246Matches\":"<<e.linked020AAnd0246Matches;if(e.writePerformed)out<<",\"recordPointer\":"<<Quote(Hex(e.recordPointer))<<",\"linkedPointer\":"<<Quote(Hex(e.linkedPointer))<<",\"linkedWord0\":"<<Quote(Hex(e.linkedWord0))<<",\"record64\":"<<Quote(Hex(e.record64))<<",\"record98\":"<<unsigned(e.record98)<<",\"old34\":{\"decimal\":"<<e.old34<<",\"hex\":"<<Quote(Hex(e.old34))<<"},\"new34\":{\"decimal\":"<<e.new34<<",\"hex\":"<<Quote(Hex(e.new34))<<"},\"timestampMs\":"<<std::fixed<<std::setprecision(6)<<e.timestampMs<<",\"breakpointRva\":"<<Quote(Hex(e.breakpointRva))<<",\"breakpointAddress\":"<<Quote(Hex(e.breakpointAddress))<<",\"moduleBase\":"<<Quote(Hex(e.moduleBase))<<",\"breakpointRemoved\":"<<Bool(e.breakpointRemoved);out<<",\"limitation\":"<<Quote(BudgetWriteLimitation)<<'}';}

std::string BudgetExperimentDetails(const BudgetExperiment& e){std::ostringstream out;if(e.writePerformed){out<<"AOE BUDGET WRITE SUCCESS\r\nrecord = "<<Hex(e.recordPointer)<<"\r\nlinked pointer = "<<Hex(e.linkedPointer)<<"\r\nlinkedWord0 = "<<Hex(e.linkedWord0)<<"\r\n+64 = "<<Hex(e.record64)<<"\r\n+98 = "<<unsigned(e.record98)<<"\r\nold34 = "<<e.old34<<" / "<<Hex(e.old34)<<"\r\nnew34 = "<<e.new34<<" / "<<Hex(e.new34)<<"\r\ntimestamp = "<<std::fixed<<std::setprecision(3)<<e.timestampMs<<" ms\r\nmodule base = "<<Hex(e.moduleBase)<<"\r\nbreakpoint = "<<Hex(e.breakpointAddress)<<" (TClient.exe+"<<std::hex<<std::uppercase<<e.breakpointRva<<")\r\nhardware breakpoint removed = "<<(e.breakpointRemoved?"yes":"pending cleanup");}else{out<<"AOE Budget Write summary: no write performed. total hits="<<e.totalBreakpointHits<<", readable records="<<e.readableRecords<<", linked020A="<<e.linked020AMatches<<", linked020AAnd0246="<<e.linked020AAnd0246Matches;if(!e.error.empty())out<<", error="<<e.error;}out<<"\r\n"<<BudgetWriteLimitation;return out.str();}
}

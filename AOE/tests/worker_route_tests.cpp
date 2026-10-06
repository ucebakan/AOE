#include "worker_route.hpp"
#include "initial_2x.hpp"
#include "live_validation.hpp"
#include <cstring>
#include <map>
#include <functional>
#include <iostream>
#include <stdexcept>

namespace {
void Require(bool value,const char* message){if(!value)throw std::runtime_error(message);}
struct Fixture {
    aoe::TargetInfo target;aoe::InitialCallSnapshot call;std::map<uint64_t,uint8_t> memory;
    template<class T>void Put(uint64_t address,T value){const auto* b=reinterpret_cast<const uint8_t*>(&value);for(size_t i=0;i<sizeof(T);++i)memory[address+i]=b[i];}
    aoe::OperationRead Reader(){return[&](uint64_t address,void* data,size_t n,uint32_t& error){for(size_t i=0;i<n;++i){auto it=memory.find(address+i);if(it==memory.end()){error=299;return false;}static_cast<uint8_t*>(data)[i]=it->second;}return true;};}
    Fixture(){
        auto&t=target;t.verified=true;t.pid=11;t.creationTime=22;t.base=0x140000000;t.size=0x10000;t.image.sha256="route-fixture";
        auto&r=t.runtime;r.workerEntryCoverage=true;r.initialNxAvailable=true;r.initialPrepRva=0x1000;r.initialCallRva=0x1100;r.initialReturnRva=0x1105;r.sharedWorkerRva=0x2000;r.alternateInitialPrepRva=0x3000;r.alternateInitialCallRva=0x3100;r.alternateInitialReturnRva=0x3105;r.visualReaderRva=0x1200;r.localActorOffset=0x2760;r.actorIdOffset=0x770;r.actorTypeOffset=0x7E9;
        auto&c=call;c.observed=true;c.threadId=77;c.rsp=0x60000;c.rcx=c.r13=0x30000;c.rdx=c.r15=0x20000;c.r8=c.rsi=0x80000;c.r9=c.r14=0x40000;c.r12=0x70000;c.rbp=0xB0000;
        Put(c.rcx+r.localActorOffset,uint64_t(0x20000));Put(0x20000+r.actorIdOffset,uint32_t(10709));Put(0x20000+r.actorTypeOffset,uint8_t(1));Put(c.r9,uint16_t(521));Put(c.rsp,t.base+r.initialReturnRva);Put(c.rsp+0x28,c.r12);
    }
    void Archer(){auto&c=call;auto&r=target.runtime;c.r14=c.rcx;c.r15=0;c.rdi=0xA0000;c.r8=c.rdi+0x4F0;Put(c.r9,uint16_t(321));Put(c.rsp,target.base+r.alternateInitialReturnRva);Put(c.rsp+0x28,c.rsp+8+0x50);Put(c.rsp+0x30,uint32_t(0));Put(c.rbp-0x60,c.rdx);Put(c.rbp-0x68,c.r9);}
    void Summon(){auto&c=call;auto&r=target.runtime;c.rdx=c.r15=0x90000;Put(c.r9,uint16_t(522));Put(c.rdx+r.actorTypeOffset,uint8_t(11));Put(c.rdx,target.base+0x8000);Put(target.base+0x81A0,target.base+0x9000);Put(c.rdx+0x13AC,uint32_t(10709));r.ownerGetters.push_back({0x81A0,0x9000,0x13AC,{0x8B,0x81,0xAC,0x13,0,0,0xC3}});for(size_t i=0;i<7;++i)memory[target.base+0x9000+i]=r.ownerGetters.back().bytes[i];}
};
void Return(aoe::LiveValidationState& s,const aoe::InitialCallSnapshot& c,uint64_t rbp){aoe::ObserveLiveReturn(s,c.threadId,c.rsp,c.r12,c.r13,c.r14,c.r15,c.rsi,c.rdi,rbp);}
}
unsigned WorkerRouteTests(const std::filesystem::path&){unsigned passed=0;auto check=[&](const char* name,const std::function<void()>& test){test();++passed;std::cout<<"[PASS] "<<name<<'\n';};
    check("Priest common entry proves caller ABI and normalizes return stack",[]{Fixture f;Require(aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"own Priest route");Require(f.call.rsp==0x60008&&f.call.prepRva==0x1000&&f.call.returnRva==0x1105&&f.call.casterId==10709,"entry -> caller stack/route identity");});
    check("Archer alternate preparation uses its stack vector and frame fields",[]{Fixture f;f.Archer();Require(aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"own Archer route");Require(f.call.operationWord0==321&&f.call.prepRva==0x3000&&f.call.returnRva==0x3105,"alternate route, not Priest replay");});
    check("supported skill reached from a UI or unknown caller cannot replay",[]{Fixture f;f.Put(f.call.rsp,f.target.base+0x5000);Require(!aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"unprofiled caller rejected");});
    check("changed preparation arguments and incomplete frame reads fail closed",[]{for(unsigned i=0;i<4;++i){Fixture f;f.Archer();if(i==0)++f.call.r8;else if(i==1)f.Put(f.call.rsp+0x28,uint64_t(0xBAD));else if(i==2)f.Put(f.call.rbp-0x68,uint64_t(0xBAD));else f.memory.erase(f.call.rbp-0x60);Require(!aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"alternate ABI mismatch rejected");}});
    check("periodic summon ownership follows its verified creator getter",[]{Fixture f;f.Summon();Require(aoe::ResolveWorkerCall(f.Reader(),f.target,f.call)&&f.call.casterId==10709&&f.call.rdx!=f.call.localActor,"summon belongs to local caster");});
    check("foreign summon modified getter or vtable cannot provide evidence",[]{for(unsigned i=0;i<4;++i){Fixture f;f.Summon();if(i==0)f.Put(f.call.rdx+0x13AC,uint32_t(999));else if(i==1)f.memory[f.target.base+0x9000]=0x90;else if(i==2)f.Put(f.target.base+0x81A0,f.target.base+0x9001);else f.Put(f.call.rdx,uint64_t(0xD0000));Require(!aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"foreign creator/code/table rejected");}});
    check("one local initial plus nine owned summon call-returns validate",[]{Fixture f;f.Archer();Require(aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"initial proof");aoe::LiveValidationState s;aoe::EnsureLiveValidationSession(s,f.target,"profile");aoe::MarkLiveProfileFingerprintsPassed(s);aoe::ObserveLivePrep(s,77,1);aoe::ObserveLiveCall(s,aoe::ClassifyOperationWord(321),&f.call);Return(s,f.call,f.call.rbp);
        Fixture tick;tick.Summon();tick.Put(tick.call.r9,uint16_t(322));Require(aoe::ResolveWorkerCall(tick.Reader(),tick.target,tick.call),"owned periodic");for(unsigned i=0;i<9;++i){aoe::ObserveLiveCall(s,aoe::ClassifyOperationWord(322),&tick.call);Return(s,tick.call,tick.call.rbp);}Require(s.replayCriticalValidationPassed&&s.periodic020AReturnCount==9,"different owner pointer with same proven caster is valid");});
    check("foreign initial cannot change family or abort an armed local replay",[]{Fixture f;f.Archer();Require(aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"initial proof");auto nx=aoe::CreateInitial2xExperiment(f.target,false,2,aoe::AoeSkillFamily::Archer,f.call.prepRva,f.call.returnRva);auto foreign=f.call;foreign.ownershipVerified=false;foreign.operationWord0=424;Require(aoe::ObserveInitialCall(nx,foreign)==aoe::InitialCallAction::Ignore&&!nx.aborted,"foreign Mage ignored");aoe::LiveValidationState s;s.skillFamily=aoe::AoeSkillFamily::Archer;aoe::ObserveLiveCall(s,aoe::ClassifyOperationWord(424),&foreign);Require(s.skillFamily==aoe::AoeSkillFamily::Archer&&s.initial0209Count==0,"foreign Mage does not bind");});
    check("Archer Nx repeats only its validated route with bounded redirects",[]{Fixture f;f.Archer();Require(aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"initial proof");auto nx=aoe::CreateInitial2xExperiment(f.target,false,2,aoe::AoeSkillFamily::Archer,f.call.prepRva,f.call.returnRva);Require(aoe::Initial2xSessionMatches(nx,f.target),"alternate session route accepted");Require(aoe::ObserveInitialCall(nx,f.call)==aoe::InitialCallAction::FirstAccepted,"first call");auto&c=f.call;Require(aoe::ObserveInitialReturn(nx,77,c.rsp,c.r12,c.r13,c.r14,c.r15,c.rsi,c.rdi,c.rbp)==aoe::InitialReturnAction::Redirect,"validated return redirects");aoe::CommitInitialRedirect(nx,true);Require(aoe::ObserveInitialCall(nx,c)==aoe::InitialCallAction::SecondObserved,"second call");Require(aoe::ObserveInitialReturn(nx,77,c.rsp,c.r12,c.r13,c.r14,c.r15,c.rsi,c.rdi,c.rbp)==aoe::InitialReturnAction::Completed&&nx.redirectsPerformed==1,"two calls, one redirect");});
    check("missing route and changed frame register cannot arm or redirect",[]{Fixture f;f.Archer();Require(!aoe::CreateInitial2xExperiment(f.target,false,2,aoe::AoeSkillFamily::Archer).armed,"missing normal route rejected");Require(aoe::ResolveWorkerCall(f.Reader(),f.target,f.call),"initial proof");auto&c=f.call;auto nx=aoe::CreateInitial2xExperiment(f.target,false,2,aoe::AoeSkillFamily::Archer,c.prepRva,c.returnRva);aoe::ObserveInitialCall(nx,c);Require(aoe::ObserveInitialReturn(nx,77,c.rsp,c.r12,c.r13,c.r14,c.r15,c.rsi,c.rdi,c.rbp+8)==aoe::InitialReturnAction::Aborted,"frame drift prevents redirect");});
    check("four stable worker return return visual slots cover both classes",[]{Fixture f;auto slots=aoe::WorkerBreakpointRvas(f.target.runtime);Require(slots==std::vector<uint64_t>{0x2000,0x1105,0x3105,0x1200},"shared entry, two returns and visual fit DR0..DR3");});
    return passed;
}

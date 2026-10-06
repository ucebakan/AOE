#include "initial_2x.hpp"
#include "live_validation.hpp"
#include "visual_suppression.hpp"
#include <functional>
#include <iostream>
#include <stdexcept>

namespace {
void Require(bool value,const char* reason){if(!value)throw std::runtime_error(reason);}
aoe::TargetInfo Target(){aoe::TargetInfo t;t.pid=73;t.creationTime=99;t.base=0x140000000;t.verified=true;t.image.sha256="FAMILY-FIXTURE";t.runtime.initialNxAvailable=true;t.runtime.initialPrepRva=0x1100;t.runtime.liveValidationRequired=true;t.runtime.visualSuppressionAvailable=true;t.runtime.visualReaderRva=0x100;t.runtime.visualLookupRva=0x500;t.runtime.visualFieldOffset=0x8C;t.runtime.visualBaseRegister=aoe::X64RegisterId::Rdx;t.runtime.visualReaderBytes={0x8B};return t;}
aoe::InitialCallSnapshot Call(uint16_t word,uint32_t tid=77){aoe::InitialCallSnapshot c;c.observed=true;c.operationReadable=true;c.operationWord0=word;c.threadId=tid;c.rsp=0x1000;c.r12=12;c.r13=13;c.r14=14;c.r15=15;c.rsi=16;c.rdi=17;c.rdx=0x50000;return c;}
void Return(aoe::LiveValidationState& s,uint32_t tid=77){aoe::ObserveLiveReturn(s,tid,0x1000,12,13,14,15,16,17);}
void Validate(aoe::LiveValidationState& s,aoe::AoeSkillFamily family){auto t=Target();aoe::EnsureLiveValidationSession(s,t,"profile");aoe::MarkLiveProfileFingerprintsPassed(s);aoe::ObserveLivePrep(s,77,1);auto skill=aoe::SkillContract(family);auto c=Call(skill.initial);aoe::ObserveLiveCall(s,aoe::ClassifyOperationWord(skill.initial),&c);Return(s);c=Call(skill.periodic);for(unsigned i=0;i<9;++i){aoe::ObserveLiveCall(s,aoe::ClassifyOperationWord(skill.periodic),&c);Return(s);}}
}

unsigned SkillFamilyTests(const std::filesystem::path&){unsigned passed=0;auto check=[&](const char* name,const std::function<void()>& body){body();++passed;std::cout<<"[PASS] "<<name<<'\n';};
    for(auto family:{aoe::AoeSkillFamily::Priest,aoe::AoeSkillFamily::Mage,aoe::AoeSkillFamily::Archer}){
        check(aoe::SkillContract(family).name,[=]{
            auto skill=aoe::SkillContract(family);
            Require(aoe::ClassifyOperationWord(skill.initial).classification==aoe::OperationClassification::Initial0209,"initial role");
            Require(aoe::ClassifyOperationWord(skill.periodic).classification==aoe::OperationClassification::Periodic020A,"periodic role");
            aoe::LiveValidationState live;Validate(live,family);Require(live.skillFamily==family&&aoe::CanArmInitialNxFromValidation(live,Target(),"profile"),"family-specific complete proof");
            auto nx=aoe::CreateInitial2xExperiment(Target(),false,100,family);auto c=Call(skill.initial);
            aoe::VisualRuntimeState visual;aoe::InitializeVisualRuntime(visual,Target());visual.captureBreakpointActive=true;std::string error;
            Require(aoe::ObserveVisualReader(visual,77,0x60000,731,error,skill.periodic),"visual reader");aoe::ObserveVisualPreparation(visual,77);
            Require(aoe::ConfirmVisualAoeCall(visual,77,true,skill.initial)&&visual.skillFamily==family,"family-bound reader correlation");
            uint32_t memory=731;aoe::VisualMemoryIo io;io.read=[&](uint64_t,uint32_t& v,std::string&){v=memory;return true;};io.write=[&](uint64_t,uint32_t v,std::string&){memory=v;return true;};
            visual.suppressionRequested=true;Require(aoe::ApplyVisualSuppression(visual,io,error)&&memory==0,"hide captured original");
            Require(aoe::BeginVisualDamageWindow(visual,77,io,error)&&memory==731,"damage input original");
            for(unsigned i=0;i<100;++i){
                Require(aoe::ObserveInitialCall(nx,c)==(i?aoe::InitialCallAction::SecondObserved:aoe::InitialCallAction::FirstAccepted),"bounded matching initial call");
                const auto action=aoe::ObserveInitialReturn(nx,77,0x1000,12,13,14,15,16,17);
                Require(action==(i==99?aoe::InitialReturnAction::Completed:aoe::InitialReturnAction::Redirect),"bounded return");
                Require(aoe::EndVisualDamageWindow(visual,77,action==aoe::InitialReturnAction::Redirect,io,error),"combined hide/initial ownership");
                Require(memory==(i==99?0u:731u),"hide only after final replay return");
                if(i<99)aoe::CommitInitialRedirect(nx,true);
            }
            Require(nx.completed&&nx.redirectsPerformed==99,"100 calls 99 redirects");visual.suppressionRequested=false;
            Require(aoe::ApplyVisualSuppression(visual,io,error)&&memory==731&&!visual.restoreRequired,"restore dynamic original");
        });
    }
    check("cross-family initial discards prior validation and pending return",[]{
        aoe::LiveValidationState live;Validate(live,aoe::AoeSkillFamily::Priest);auto c=Call(321);
        aoe::ObserveLiveCall(live,aoe::ClassifyOperationWord(321),&c);Return(live);
        Require(live.skillFamily==aoe::AoeSkillFamily::Archer&&!live.replayCriticalValidationPassed&&live.periodic020ACount==0&&live.periodic020AReturnCount==0,"no old proof transfer");
        c=Call(522);for(unsigned i=0;i<9;++i){aoe::ObserveLiveCall(live,aoe::ClassifyOperationWord(522),&c);Return(live);}
        Require(!live.replayCriticalValidationPassed&&live.periodic020ACount==0,"foreign periodic calls cannot finish archer validation");
        c=Call(322);for(unsigned i=0;i<9;++i){aoe::ObserveLiveCall(live,aoe::ClassifyOperationWord(322),&c);Return(live);}Require(live.replayCriticalValidationPassed,"new matching normal cast proof");
    });
    check("another thread or owner cannot supply periodic return proof",[]{
        for(unsigned variant=0;variant<2;++variant){aoe::LiveValidationState live;aoe::EnsureLiveValidationSession(live,Target(),"profile");aoe::MarkLiveProfileFingerprintsPassed(live);aoe::ObserveLivePrep(live,77,1);auto c=Call(424);aoe::ObserveLiveCall(live,aoe::ClassifyOperationWord(424),&c);Return(live);
            c=Call(425,variant?77:88);if(variant)c.rdx+=1;
            for(unsigned i=0;i<9;++i){aoe::ObserveLiveCall(live,aoe::ClassifyOperationWord(425),&c);Return(live,c.threadId);}Require(!live.replayCriticalValidationPassed&&live.periodic020AReturnCount==0,"foreign thread/owner does not validate");}
    });
    check("armed replay rejects another class and wrong replayed operation",[]{
        for(unsigned variant=0;variant<2;++variant){auto nx=aoe::CreateInitial2xExperiment(Target(),false,2,aoe::AoeSkillFamily::Archer);
            if(variant){aoe::ObserveInitialCall(nx,Call(321));aoe::ObserveInitialReturn(nx,77,0x1000,12,13,14,15,16,17);aoe::CommitInitialRedirect(nx,true);}
            Require(aoe::ObserveInitialCall(nx,Call(424))==aoe::InitialCallAction::Aborted&&nx.aborted&&!nx.repeatPending,"foreign initial cannot redirect");}
    });
    check("reader family mismatch and unknown skill cannot establish visual ownership",[]{
        for(uint16_t reader:{uint16_t(425),uint16_t(0),uint16_t(323)}){aoe::VisualRuntimeState visual;aoe::InitializeVisualRuntime(visual,Target());visual.captureBreakpointActive=true;std::string error;aoe::ObserveVisualReader(visual,77,0x60000,987,error,reader);aoe::ObserveVisualPreparation(visual,77);
            Require(!aoe::ConfirmVisualAoeCall(visual,77,true,321)&&!visual.resolved,"different or unknown reader rejected");}
    });
    check("unknown operations never become supported AOE",[]{for(uint16_t word:{uint16_t(0),uint16_t(323),uint16_t(426),uint16_t(523),uint16_t(65535)})Require(aoe::ClassifyOperationWord(word).classification==aoe::OperationClassification::Other,"unknown is other");Require(!aoe::CreateInitial2xExperiment(Target(),false,2,aoe::AoeSkillFamily::Unknown).armed,"unidentified skill cannot arm");});
    return passed;
}

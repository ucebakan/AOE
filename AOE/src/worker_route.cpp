#include "worker_route.hpp"
#include <algorithm>
#include <array>

namespace aoe {
namespace {
bool Pointer(uint64_t value){return value>=0x10000&&value<0x0000800000000000ull;}
template<class T>bool Read(const OperationRead& read,uint64_t address,T& value){uint32_t error=0;return read&&read(address,&value,sizeof(value),error);}
}
std::vector<uint64_t> WorkerBreakpointRvas(const RuntimeLayout& r){return{r.sharedWorkerRva,r.initialReturnRva,r.alternateInitialReturnRva,r.visualReaderRva};}
bool ReadAoeCaster(const OperationRead& read,const TargetInfo& t,uint64_t game,uint64_t owner,uint64_t& local,uint32_t& caster){
    local=0;caster=0;const auto&r=t.runtime;
    if(!t.verified||!r.workerEntryCoverage||!Pointer(game)||!Pointer(owner)||!Read(read,game+r.localActorOffset,local)||!Pointer(local))return false;
    uint32_t localId=0;uint8_t localType=0;
    if(!Read(read,local+r.actorIdOffset,localId)||!localId||!Read(read,local+r.actorTypeOffset,localType)||localType!=1)return false;
    if(owner==local){caster=localId;return true;}
    uint8_t type=0;uint64_t table=0,method=0;
    if(!Read(read,owner+r.actorTypeOffset,type)||(type!=7&&type!=11)||!Read(read,owner,table)||table<t.base||table-t.base>=t.size||t.size-(table-t.base)<=0x1A8||!Read(read,table+0x1A0,method))return false;
    const auto slot=table+0x1A0-t.base;
    const auto it=std::find_if(r.ownerGetters.begin(),r.ownerGetters.end(),[&](const AoeOwnerGetter& g){return g.slotRva==slot&&g.methodRva<t.size&&method==t.base+g.methodRva;});
    if(it==r.ownerGetters.end()||it->bytes.size()!=7)return false;
    std::array<uint8_t,7> code{};uint32_t error=0;
    if(!read(method,code.data(),code.size(),error)||!std::equal(code.begin(),code.end(),it->bytes.begin())||!Read(read,owner+it->fieldOffset,caster)||caster!=localId){caster=0;return false;}
    return true;
}
bool ResolveWorkerCall(const OperationRead& read,const TargetInfo& t,InitialCallSnapshot& c){
    c.ownershipRequired=true;c.ownershipVerified=false;c.prepRva=c.returnRva=0;
    const auto&r=t.runtime;uint64_t returned=0,vector=0;
    if(!r.workerEntryCoverage||!Pointer(c.rsp)||!Read(read,c.rsp,returned)||returned<t.base||returned-t.base>=t.size||!Read(read,c.rsp+0x28,vector))return false;
    const auto route=returned-t.base;
    if(route==r.initialReturnRva){
        if(c.rcx!=c.r13||c.rdx!=c.r15||c.r8!=c.rsi||c.r9!=c.r14||vector!=c.r12)return false;
        c.prepRva=r.initialPrepRva;
    }else if(route==r.alternateInitialReturnRva){
        uint64_t owner=0,skill=0;uint32_t flags=1;
        if(!Pointer(c.rbp)||c.rcx!=c.r14||c.r8!=c.rdi+0x4F0||c.r15||vector!=c.rsp+8+0x50||!Read(read,c.rbp-0x60,owner)||!Read(read,c.rbp-0x68,skill)||owner!=c.rdx||skill!=c.r9||!Read(read,c.rsp+0x30,flags)||flags)return false;
        c.prepRva=r.alternateInitialPrepRva;
    }else return false;
    if(!ReadAoeCaster(read,t,c.rcx,c.rdx,c.localActor,c.casterId))return false;
    const auto operation=DecodeOperation(read,c.r9);
    if(operation.family==AoeSkillFamily::Unknown||!operation.operationReadable||(operation.classification==OperationClassification::Initial0209&&c.rdx!=c.localActor))return false;
    c.operationReadable=true;c.operationWord0=operation.operationWord;c.operation0209=operation.classification==OperationClassification::Initial0209;
    c.returnRva=route;c.rsp+=8;c.ownershipVerified=true;return true;
}
}

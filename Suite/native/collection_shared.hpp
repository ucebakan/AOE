#pragma once
#include <Windows.h>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <initializer_list>
#pragma pack(push,4)
struct CodeGuard { uint32_t rva,size; unsigned char bytes[128]; };
struct Layout {
    uint32_t version,imageSize,timestamp,root,ownerVtable,playerVtable,monsterVtable,maintainVtable;
    uint32_t playerOffset,sessionOffset,registryOffset,actorIdOffset,actorTypeOffset,actionOffset,healthOffset,deadStateOffset,ghostOffset,parentOffset,reverseParentOffset,maintainOffset,skillOffset;
    CodeGuard sender,lookup,death,maintain,parent;
};
#pragma pack(pop)
struct Request {
    uint32_t version,status,error,pid;
    uint64_t created,base,owner,player,target,session,deadline;
    uint32_t targetId,mode;
    uint64_t head,node;
};
constexpr uint32_t MaxBatch=64;
struct Batch {
    uint32_t version,status,error,count,completed,cancel,hostPid,reserved;
    uint64_t hostCreated;
    Layout layout;
    Request requests[MaxBatch];
};
static_assert(sizeof(CodeGuard)==136 && sizeof(Layout)==764 && sizeof(Request)==96 && sizeof(Batch)==6952);
inline UINT BatchMessage(){return RegisterWindowMessageW(L"4Unity.Suite.Collection.Batch.v1");}
inline void BatchMapName(wchar_t* out,DWORD pid,DWORD tid,uint64_t token){swprintf_s(out,128,L"Local\\4UnitySuiteCollectionV1_%lu_%lu_%016llx",pid,tid,token);}
inline uint64_t Created(HANDLE p){FILETIME c{},e{},k{},u{};if(!GetProcessTimes(p,&c,&e,&k,&u))return 0;return uint64_t(c.dwHighDateTime)<<32|c.dwLowDateTime;}
template<class Reader> uint32_t Validate(const Request& r,const Layout& l,Reader read){
    auto q=[&](uint64_t a){uint64_t v=0;if(!read(a,&v,8))throw 1;return v;};
    auto d=[&](uint64_t a){uint32_t v=0;if(!read(a,&v,4))throw 1;return v;};
    auto b=[&](uint64_t a){uint8_t v=0;if(!read(a,&v,1))throw 1;return v;};
    auto w=[&](uint64_t a){uint16_t v=0;if(!read(a,&v,2))throw 1;return v;};
    if(r.version!=1||r.mode!=1||!r.base||!r.owner||!r.player||!r.target||!r.session||l.version!=1||l.skillOffset!=0x20)return 10;
    auto pe=d(r.base+0x3c);
    if(pe<0x40||pe>0x1000||d(r.base+pe)!=0x4550||d(r.base+pe+8)!=l.timestamp||d(r.base+pe+80)!=l.imageSize)return 11;
    for(auto code:{&l.sender,&l.lookup,&l.death,&l.maintain,&l.parent}){
        unsigned char actual[128]{};
        if(!code->size||code->size>128||code->rva>=l.imageSize||code->size>l.imageSize-code->rva||!read(r.base+code->rva,actual,code->size)||memcmp(actual,code->bytes,code->size))return 11;
    }
    if(q(r.base+l.root)!=r.owner||q(r.owner)!=r.base+l.ownerVtable||q(r.owner+l.playerOffset)!=r.player||q(r.player)!=r.base+l.playerVtable||q(r.owner+l.sessionOffset)!=r.session)return 12;
    if(!r.head||!r.node||q(r.owner+l.registryOffset)!=r.head||b(r.head+0x19)!=1||q(r.owner+l.registryOffset+8)>4096)return 25;
    uint64_t cursor=q(r.head+8),parent=r.head,seen[128]{};unsigned n=0;int64_t low=-1,high=int64_t(UINT32_MAX)+1;bool found=false;
    while(cursor!=r.head){
        if(!cursor||n>=128||b(cursor+0x19)!=0||q(cursor+8)!=parent)return 25;
        for(unsigned k=0;k<n;k++)if(seen[k]==cursor)return 25;seen[n++]=cursor;
        uint32_t key=d(cursor+0x20);if(key<=low||key>=high)return 25;
        if(key==r.targetId){found=cursor==r.node&&q(cursor+0x28)==r.target;break;}
        parent=cursor;if(r.targetId<key){high=key;cursor=q(cursor);}else{low=key;cursor=q(cursor+0x10);}
    }
    if(!found||q(r.owner+l.registryOffset)!=r.head)return 25;
    if(q(r.target)!=r.base+l.monsterVtable||b(r.target+l.actorTypeOffset)!=2||d(r.target+l.actorIdOffset)!=r.targetId||!r.targetId)return 13;
    auto action=b(r.target+l.actionOffset);if(action!=6&&action!=7)return 14;
    action=b(r.player+l.actionOffset);if(action==6||action==7||b(r.player+l.ghostOffset)!=0||d(r.player+l.healthOffset)==0||b(r.player+l.actorTypeOffset)!=1||!d(r.player+l.actorIdOffset))return 15;
    parent=q(r.player+l.parentOffset);if(parent&&q(parent+l.reverseParentOffset)==r.player)return 16;
    uint64_t head=q(r.player+l.maintainOffset),count=q(r.player+l.maintainOffset+8);
    if(!head||count>64||b(head+0x19)!=1)return 19;
    uint64_t pending[129]{},visited[64]{};unsigned top=0;n=0;pending[top++]=q(head+8);
    while(top){
        auto node=pending[--top];if(node==head)continue;
        if(!node||n>=count||n>=64||b(node+0x19)!=0)return 20;
        for(unsigned k=0;k<n;k++)if(visited[k]==node)return 20;visited[n++]=node;
        auto maintain=q(node+0x28);if(!maintain||q(maintain)!=r.base+l.maintainVtable)return 21;
        auto skill=q(maintain+l.skillOffset);if(!skill)return 21;if(w(skill)==9909)return 22;
        if(top>126)return 20;pending[top++]=q(node);pending[top++]=q(node+0x10);
    }
    if(n!=count||q(r.player+l.maintainOffset)!=head||q(r.player+l.maintainOffset+8)!=count)return 23;
    return 0;
}

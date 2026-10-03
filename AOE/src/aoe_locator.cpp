#include "aoe_locator.hpp"
#include "tracer.hpp"
#include <algorithm>
#include <cstring>
#include <fstream>
#include <functional>
#include <sstream>
#include <stdexcept>

namespace aoe {
namespace {
struct Pattern {
    std::vector<int> bytes;
    size_t fixed=0;
    explicit Pattern(const std::string& text) {
        std::istringstream in(text);std::string word;bool found=false;
        while(in>>word){if(word=="?"||word=="??"){bytes.push_back(-1);continue;}
            if(word.size()!=2||word.find_first_not_of("0123456789abcdefABCDEF")!=std::string::npos)throw std::invalid_argument("Invalid AOE masked signature token");
            if(!found){fixed=bytes.size();found=true;}bytes.push_back(int(std::stoul(word,nullptr,16)));}
        if(bytes.empty()||!found)throw std::invalid_argument("Empty or all-wildcard AOE signature");
    }
    bool at(const std::vector<uint8_t>& data,size_t pos)const {
        if(pos>data.size()||bytes.size()>data.size()-pos)return false;
        for(size_t i=0;i<bytes.size();++i)if(bytes[i]>=0&&data[pos+i]!=bytes[i])return false;return true;
    }
};
template<class T>T Read(const std::vector<uint8_t>& data,size_t pos){if(pos>data.size()||sizeof(T)>data.size()-pos)throw std::runtime_error("Truncated PE data");T x;std::memcpy(&x,data.data()+pos,sizeof(x));return x;}
struct Pe {
    std::vector<uint8_t> data;
    std::vector<IMAGE_SECTION_HEADER> sections;
    std::vector<RUNTIME_FUNCTION> functions;
    IMAGE_NT_HEADERS64 nt{};
    explicit Pe(const std::filesystem::path& path){
        auto size=std::filesystem::file_size(path);if(size<sizeof(IMAGE_DOS_HEADER)||size>512ull*1024*1024)throw std::runtime_error("Invalid PE file size");
        data.resize(size_t(size));std::ifstream in(path,std::ios::binary);if(!in.read(reinterpret_cast<char*>(data.data()),std::streamsize(size)))throw std::runtime_error("Cannot read target PE");
        auto dos=Read<IMAGE_DOS_HEADER>(data,0);if(dos.e_magic!=IMAGE_DOS_SIGNATURE||dos.e_lfanew<0)throw std::runtime_error("Invalid DOS header");
        nt=Read<IMAGE_NT_HEADERS64>(data,size_t(dos.e_lfanew));if(nt.Signature!=IMAGE_NT_SIGNATURE||nt.FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64||nt.OptionalHeader.Magic!=IMAGE_NT_OPTIONAL_HDR64_MAGIC||nt.FileHeader.SizeOfOptionalHeader<sizeof(IMAGE_OPTIONAL_HEADER64)||!nt.OptionalHeader.SizeOfImage||nt.FileHeader.NumberOfSections>128)throw std::runtime_error("Invalid AMD64 PE headers");
        size_t table=size_t(dos.e_lfanew)+24+nt.FileHeader.SizeOfOptionalHeader;
        for(unsigned i=0;i<nt.FileHeader.NumberOfSections;++i){auto s=Read<IMAGE_SECTION_HEADER>(data,table+i*sizeof(IMAGE_SECTION_HEADER));if(uint64_t(s.VirtualAddress)+std::max(s.Misc.VirtualSize,s.SizeOfRawData)>nt.OptionalHeader.SizeOfImage||uint64_t(s.PointerToRawData)+s.SizeOfRawData>data.size())throw std::runtime_error("PE section out of bounds");sections.push_back(s);}
        if(nt.OptionalHeader.NumberOfRvaAndSizes>IMAGE_DIRECTORY_ENTRY_EXCEPTION){const auto& d=nt.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION];if(d.Size%sizeof(RUNTIME_FUNCTION))throw std::runtime_error("Invalid unwind table size");auto p=offset(d.VirtualAddress,d.Size);for(size_t i=0;i<d.Size;i+=sizeof(RUNTIME_FUNCTION)){auto f=Read<RUNTIME_FUNCTION>(data,p+i);if(f.BeginAddress>=f.EndAddress||f.EndAddress>nt.OptionalHeader.SizeOfImage)throw std::runtime_error("Invalid unwind range");functions.push_back(f);}std::sort(functions.begin(),functions.end(),[](auto a,auto b){return a.BeginAddress<b.BeginAddress;});}
    }
    size_t offset(uint64_t rva,size_t size)const{for(const auto& s:sections)if(rva>=s.VirtualAddress&&rva-s.VirtualAddress<=s.SizeOfRawData&&size<=s.SizeOfRawData-(rva-s.VirtualAddress))return size_t(s.PointerToRawData+rva-s.VirtualAddress);throw std::runtime_error("Unbacked PE RVA "+Hex(rva));}
    std::vector<uint8_t> bytes(uint64_t rva,size_t n)const{auto p=offset(rva,n);return {data.begin()+p,data.begin()+p+n};}
    bool executable(uint64_t rva,size_t n=1)const{for(const auto&s:sections)if((s.Characteristics&IMAGE_SCN_MEM_EXECUTE)&&rva>=s.VirtualAddress&&rva-s.VirtualAddress<=s.SizeOfRawData&&n<=s.SizeOfRawData-(rva-s.VirtualAddress))return true;return false;}
    std::vector<uint64_t> scan(const std::string& text)const{Pattern p(text);std::vector<uint64_t> out;for(const auto&s:sections){if(!(s.Characteristics&IMAGE_SCN_MEM_EXECUTE)||s.SizeOfRawData<p.bytes.size())continue;size_t end=s.PointerToRawData+s.SizeOfRawData-p.bytes.size()+1;for(size_t x=s.PointerToRawData;x<end;++x)if(data[x+p.fixed]==p.bytes[p.fixed]&&p.at(data,x))out.push_back(s.VirtualAddress+x-s.PointerToRawData);}return out;}
    bool at(uint64_t rva,const std::string& text)const{Pattern p(text);return executable(rva,p.bytes.size())&&p.at(data,offset(rva,p.bytes.size()));}
    uint64_t call(uint64_t rva)const{uint64_t target=0;if(!DecodeAoeCall(bytes(rva,5),rva,nt.OptionalHeader.SizeOfImage,target)||!executable(target))throw std::runtime_error("Invalid relative CALL at "+Hex(rva));return target;}
    uint64_t function(uint64_t rva)const{
        auto it=std::upper_bound(functions.begin(),functions.end(),rva,[](uint64_t x,const auto&f){return x<f.BeginAddress;});if(it==functions.begin())return 0;auto f=*--it;if(rva>=f.EndAddress)return 0;
        // Follow chained unwind entries; MSVC can split one logical worker into
        // multiple .pdata ranges after nonvolatile register saves.
        for(unsigned depth=0;depth<32;++depth){auto p=offset(f.UnwindData,4);uint8_t flags=data[p]>>3;if(!(flags&UNW_FLAG_CHAININFO))return f.BeginAddress;size_t chained=(4+size_t(data[p+2])*2+3)&~size_t(3);f=Read<RUNTIME_FUNCTION>(data,offset(f.UnwindData,chained+sizeof(f))+chained);}
        throw std::runtime_error("Cyclic/excessive unwind chain");
    }
};
const char* Prep="41 8B 46 24 89 44 24 30 89 7C 24 28 4C 89 64 24 20 4C 8B CE 4D 8B C6 49 8B D7 49 8B CD E8 ?? ?? ?? ?? 90";
const char* Dispatch="80 79 01 06 75 ?? 80 79 02 1D 75 ?? 66 83 79 08 00 75 ?? 0F B7 49 06 E8 ?? ?? ?? ?? 48 85 C0 74 ?? 41 8B 4E 24 89 4C 24 30 C7 44 24 28 01 00 00 00 89 7C 24 20 4C 8B C8 4D 8B C6 48 8B 94 24 C0 00 00 00 49 8B CD E8 ?? ?? ?? ??";
const char* Worker="41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 C0 00 00 00 4C 8B AC 24 A0 01 00 00 4D 8B F1";
const char* Producer="48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC ?? 41 0F B6 E9 45 8B F8 44 8B E2 4C 8B E9 E8 ?? ?? ?? ??";
const char* ProducerCall="66 89 6C 24 40 4C 89 6C 24 38 F3 44 0F 11 4C 24 30 F3 44 0F 11 54 24 28 F3 44 0F 11 5C 24 20 E8 ?? ?? ?? ??";
const char* Typed="85 D2 74 ?? 41 0F B6 C0 FF C8 83 F8 12 77 ?? 4C 8D 0D ?? ?? ?? ?? 48 98 45 8B 84 81 ?? ?? ?? ?? 4D 03 C1 41 FF E0";
const char* Lookup="4C 8B 05 ?? ?? ?? ?? 49 8B D0 49 8B 40 08 80 78 19 00 75 ?? 66 39 48 20 73 ?? 48 83 C0 10 EB ?? 48 8B D0 48 8B 00 80 78 19 00 74 ?? 80 7A 19 00 75 ?? 66 3B 4A 20 73 ?? 49 8B D0 49 3B D0 74 ?? 48 8B 42 28 C3 33 C0 C3";
const char* Vector="48 8D 8F ?? ?? 00 00 48 8B 51 08 48 3B 51 10";
const char* Serialization="8B 10 85 D2 74 ?? 0F B6 58 04 84 DB 74 ?? 48 8D 4C 24 28 E8 ?? ?? ?? ?? 48 8B C8 0F B6 D3 E8 ?? ?? ?? ?? 40 FE C5";
const char* TypedCall="8B 10 85 D2 74 ?? 44 0F B6 40 04 45 84 C0 74 ?? 49 8B CC E8 ?? ?? ?? ?? 48 8B F0";
const char* Insertion="8B BF ?? ?? 00 00 85 FF 0F 84 ?? ?? ?? ?? 48 8B 44 24 60 48 2B 44 24 58 48 C1 F8 03 48 83 F8 40 0F 83 ?? ?? ?? ?? B9 08 00 00 00 E8 ?? ?? ?? ?? 4C 8B F8 89 38 C6 40 04 02";
const char* Visual="8B ?? 8C 00 00 00 85 ?? 0F 84 ?? ?? ?? ?? E8 ?? ?? ?? ??";
const char* SfxLookup32="4C 8B 05 ?? ?? ?? ?? 49 8B D0 49 8B 40 08 80 78 19 00 75 ?? 39 48 20 73 ?? 48 83 C0 10";
const AoeSignatureResult* Find(const AoeLocatorResult& r,const std::string& name){auto i=std::find_if(r.signatures.begin(),r.signatures.end(),[&](const auto&s){return s.name==name;});return i==r.signatures.end()?nullptr:&*i;}
}

std::vector<size_t> MatchAoePattern(const std::vector<uint8_t>& bytes,const std::string& text){Pattern p(text);std::vector<size_t> out;if(bytes.size()<p.bytes.size())return out;for(size_t i=0;i<=bytes.size()-p.bytes.size();++i)if(p.at(bytes,i))out.push_back(i);return out;}
bool DecodeAoeCall(const std::vector<uint8_t>& bytes,uint64_t rva,uint64_t imageSize,uint64_t& target){target=0;if(bytes.size()<5||bytes[0]!=0xE8||rva>=imageSize||imageSize-rva<5||imageSize>INT64_MAX)return false;int32_t d=0;std::memcpy(&d,bytes.data()+1,4);const uint64_t next=rva+5;if(d>=0){if(uint64_t(d)>=imageSize-next)return false;target=next+uint64_t(d);}else{const uint64_t distance=uint64_t(-int64_t(d));if(distance>next)return false;target=next-distance;}return true;}

std::vector<VisualReaderCandidate> DecodeVisualReaderSequences(const std::vector<uint8_t>& bytes,uint64_t baseRva,uint64_t imageSize){
    std::vector<VisualReaderCandidate> result;
    auto rex=[&](size_t& p,uint8_t& value){value=0;if(p<bytes.size()&&bytes[p]>=0x40&&bytes[p]<=0x4F)value=bytes[p++];};
    for(size_t start=0;start<bytes.size();++start){
        size_t p=start;uint8_t loadRex=0;rex(p,loadRex);if(loadRex&8)continue;
        if(p+6>bytes.size()||bytes[p++]!=0x8B)continue;const uint8_t load=bytes[p++];
        if((load>>6)!=2||(load&7)==4)continue;int32_t displacement=0;std::memcpy(&displacement,bytes.data()+p,4);p+=4;if(displacement!=0x8C)continue;
        const unsigned value=((loadRex&4)?8u:0u)+((load>>3)&7),base=((loadRex&1)?8u:0u)+(load&7);
        uint8_t testRex=0;rex(p,testRex);if(testRex&8)continue;if(p+2>bytes.size()||bytes[p++]!=0x85)continue;const uint8_t test=bytes[p++];
        if((test>>6)!=3)continue;const unsigned testReg=((testRex&4)?8u:0u)+((test>>3)&7),testRm=((testRex&1)?8u:0u)+(test&7);if(testReg!=value||testRm!=value)continue;
        int64_t branchTarget=0;if(p+2<=bytes.size()&&bytes[p]==0x74){const int8_t d=int8_t(bytes[p+1]);p+=2;branchTarget=int64_t(baseRva+p)+d;}else if(p+6<=bytes.size()&&bytes[p]==0x0F&&bytes[p+1]==0x84){int32_t d=0;std::memcpy(&d,bytes.data()+p+2,4);p+=6;branchTarget=int64_t(baseRva+p)+d;}else continue;
        if(p+5>bytes.size()||bytes[p]!=0xE8)continue;uint64_t lookup=0;const std::vector<uint8_t> callBytes(bytes.begin()+p,bytes.begin()+p+5);if(!DecodeAoeCall(callBytes,baseRva+p,imageSize,lookup))continue;
        const uint64_t callEnd=baseRva+p+5;if(branchTarget<=int64_t(callEnd)||branchTarget<0||uint64_t(branchTarget)>=imageSize)continue;
        VisualReaderCandidate c;c.rva=baseRva+start;c.lookupRva=lookup;c.branchTargetRva=uint64_t(branchTarget);c.fieldOffset=uint64_t(displacement);c.baseRegister=X64RegisterId(base);c.valueRegister=X64RegisterId(value);c.fingerprint.assign(bytes.begin()+start,bytes.begin()+p+5);result.push_back(std::move(c));
    }
    return result;
}

void RecoverActorTrees(Pe& pe,uint64_t typed,RuntimeLayout& r,std::vector<std::pair<uint64_t,std::vector<uint8_t>>>& evidence){

        int64_t tableBase=int64_t(typed+22)+Read<int32_t>(pe.data,pe.offset(typed+18,4));
        int64_t table=tableBase+Read<int32_t>(pe.data,pe.offset(typed+28,4));
        for(unsigned type:{1,2,7,9,10,11,17,19}){
            int64_t branch=tableBase+Read<uint32_t>(pe.data,pe.offset(uint64_t(table)+(type-1)*4,4));
            if(branch<0||!pe.at(uint64_t(branch),"E9 ?? ?? ?? ??"))throw std::runtime_error("AOE typed jump table evidence changed");
            int64_t target=branch+5+Read<int32_t>(pe.data,pe.offset(uint64_t(branch)+1,4));
            if(target<0||!pe.executable(uint64_t(target)))throw std::runtime_error("AOE typed resolver target invalid");
            uint64_t load=uint64_t(target);
            if(type==1){if(!pe.at(load,"48 8B 81 ?? ?? ?? ?? 3B 90 ?? ?? ?? ?? 74 ??")||Read<uint32_t>(pe.data,pe.offset(load+3,4))!=r.localActorOffset||Read<uint32_t>(pe.data,pe.offset(load+9,4))!=r.actorIdOffset)throw std::runtime_error("AOE local actor/ID linkage mismatch");load+=15;}
            if(!pe.at(load,"4C 8B 81 ?? ?? ?? ?? 49 8B C8 49 8B 40 08 80 78 19 00"))throw std::runtime_error("AOE typed tree shape mismatch");
            auto offset=Read<uint32_t>(pe.data,pe.offset(load+3,4));if(offset<0x100||offset>0x4000000||(offset&7))throw std::runtime_error("AOE tree offset invalid");r.actorTreeOffsets[type]=offset;
            evidence.push_back({uint64_t(target),pe.bytes(uint64_t(target),type==1?33:18)});
        }
}
bool LocateAoeImage(const std::filesystem::path& path,AoeLocatorResult& out,std::string& error,const BuildProfile* cached){
    out={};try{
        Pe pe(path);auto& image=out.image;image.path=path.wstring();image.sha256=Sha256(pe.data.data(),pe.data.size());image.fileSize=pe.data.size();image.timestamp=pe.nt.FileHeader.TimeDateStamp;image.imageSize=pe.nt.OptionalHeader.SizeOfImage;image.amd64=true;out.preferredBase=pe.nt.OptionalHeader.ImageBase;
        if(cached&&(cached->schemaVersion!=3||cached->targetSha256!=image.sha256))cached=nullptr;
        out.cacheUsed=cached!=nullptr;
        auto matches=[&](const char* name,const char* pattern,uint64_t adjustment=0){
            if(!cached)return pe.scan(pattern);
            auto anchor=FindProfileAnchor(*cached,std::string("locator_")+name);
            if(!anchor||anchor->rva<adjustment)throw std::runtime_error("Missing cached AOE semantic anchor");
            auto raw=anchor->rva-adjustment;
            return pe.executable(raw)&&pe.at(raw,pattern)?std::vector<uint64_t>{raw}:std::vector<uint64_t>{};
        };
        for(const auto&s:pe.sections)if(s.Characteristics&IMAGE_SCN_MEM_EXECUTE)image.executableRanges.push_back({s.VirtualAddress,std::max(s.Misc.VirtualSize,s.SizeOfRawData)});
        auto signature=[&](const char* name,const char* pattern,const std::function<bool(uint64_t)>& valid,const char* evidence,uint64_t adjustment=0){
            AoeSignatureResult s;s.name=name;s.pattern=pattern;s.matches=matches(name,pattern,adjustment);std::vector<uint64_t> candidates;
            for(auto rva:s.matches)if(valid(rva))candidates.push_back(rva);
            s.valid=candidates.size()==1;s.evidence=std::string(evidence)+"; semantic_matches="+std::to_string(candidates.size());
            if(s.valid){s.rva=candidates[0]+adjustment;out.liveEvidence.push_back({candidates[0],pe.bytes(candidates[0],Pattern(pattern).bytes.size())});}
            out.signatures.push_back(s);return s.rva;
        };
        const char* newPrep="8B 46 24 89 44 24 30 89 7C 24 28 4C 89 64 24 20 4D 8B CE 4C 8B C6 49 8B D7 49 8B CD E8 ?? ?? ?? ?? 90";
        const bool modern=cached?cached->runtime.initialCallRva-cached->runtime.initialPrepRva==28:pe.scan(Prep).empty();const char* prepPattern=modern?newPrep:Prep;
        out.prepDistance=modern?28:29;
        auto prep=signature("AoeLifecycleCaller",prepPattern,[&](auto r){return pe.function(r)&&pe.at(r+out.prepDistance+6,modern?"4C 8D 5C 24 70 49 8B 5B 40 49 8B 73 48 49 8B E3 41 5F 41 5E 41 5D 41 5C 5F C3":"48 83 C4 70 41 5F 41 5E 41 5D 41 5C 5F 5E 5B C3");},"Correlated nonvolatile register preparation; direct CALL; NOP and unwind continuation",out.prepDistance);
        const uint64_t prepStart=prep?prep-out.prepDistance:0;
        const char* newDispatch="80 79 01 06 75 ?? 80 79 02 1D 75 ?? 66 83 79 08 00 75 ?? 0F B7 49 06 E8 ?? ?? ?? ?? 48 85 C0 74 ?? 8B 4E 24 89 4C 24 30 C7 44 24 28 01 00 00 00 89 7C 24 20 4C 8B C8 4C 8B C6 49 8B D7 49 8B CD E8 ?? ?? ?? ??";
        const char* dispatchPattern=modern?newDispatch:Dispatch;const size_t dispatchLength=Pattern(dispatchPattern).bytes.size();
        auto dispatch=signature("AoeFunctionRecordDispatch",dispatchPattern,[&](auto r){if(!prep||pe.function(r)!=pe.function(prep)||r>=prepStart)return false;auto b=pe.bytes(r,dispatchLength);uint64_t skip=r+dispatchLength;return r+6+int8_t(b[5])==skip&&r+12+int8_t(b[11])==skip&&r+19+int8_t(b[18])==skip&&pe.at(skip,"FF C3 EB ?? 33 FF")&&pe.executable(pe.call(r+23));},"record+1=6, +2=1D, +8=0; linked WORD+6; common reject; invocation=1");
        auto worker=signature("SharedOperation020A",Worker,[&](auto r){return prep&&pe.call(prep)==r&&pe.function(r)==r;},"Unique independently patterned direct target of lifecycle call; operation pointer R9 retained");
        auto typed=signature("TypedActorLookup",Typed,[&](auto){return true;},"Zero ID rejected; R8B namespace jump table");
        auto typedCall=signature("SharedTypedActorCall",TypedCall,[&](auto r){return worker&&typed&&pe.function(r)==worker&&pe.call(r+19)==typed;},"Shared worker record+0 ID, record+4 type -> independent typed resolver",19);
        auto producer=signature("OperationProducer",Producer,[&](auto r){return pe.function(r)==r;},"Independent producer prologue and parameter register flow");
        auto producerCall=signature("ProducerCall",ProducerCall,[&](auto r){return worker&&producer&&pe.function(r)==worker&&pe.call(r+31)==producer;},"Operation WORD argument9, vector argument8, XYZ stack handoff -> producer",31);
        auto serialization=signature("RecordSerialization",Serialization,[&](auto r){return producer&&pe.function(r)==producer&&pe.call(r+19)!=pe.call(r+30);},"Record+0 ID and record+4 type feed distinct serializer calls");
        auto lookup=signature("TemplateLookup",Lookup,[&](auto r){return dispatch&&pe.call(dispatch+23)==r;},"Function-record WORD+6 feeds lookup; WORD key+20, sentinel+19, payload+28");
        const char* acquisitionPattern=modern?"4C 8B C8 4C 8B C6 49 8B D7 49 8B CD E8 ?? ?? ?? ??":"4C 8B C8 4D 8B C6 48 8B 94 24 C0 00 00 00 49 8B CD E8 ?? ?? ?? ??";
        auto acquisition=signature("LinkedAcquisitionPath",acquisitionPattern,[&](auto r){return dispatch&&r==dispatch+(modern?52:53)&&pe.function(pe.call(r+(modern?12:17)))==pe.call(r+(modern?12:17));},"R9 linked lookup result; unchanged invocation=1 preparation",modern?12:17);
        auto insertion=signature("AcquisitionType2Insertion",Insertion,[&](auto r){return acquisition&&pe.function(r)==pe.call(acquisition);},"actor+768 ID, capacity64, allocation8, record+0 ID/+4 type2",51);
        auto radius=signature("AcquisitionRadiusGuard","F3 41 0F 10 46 48 0F 2F C1 0F 86 ?? ?? ?? ??",[&](auto r){return insertion&&acquisition&&r+15==insertion-51&&pe.function(r)==pe.call(acquisition);},"Template+48 radius comparison immediately precedes the uniquely validated type2 insertion path");
        auto parsedHits=matches("ParsedActionBoundary","66 47 39 74 6F 3C");
        auto vector=signature("TargetVectorBuilder",Vector,[&](auto r){return parsedHits.size()==1&&pe.function(r)&&pe.function(r)==pe.function(parsedHits[0]);},"Owner+660 vector begin/end/capacity; independently unique handler sequence");
        auto parsed=signature("ParsedActionBoundary","66 47 39 74 6F 3C",[&](auto r){return vector&&pe.function(r)==pe.function(vector);},"Same initial action handler as target-vector creation");
        std::vector<VisualReaderCandidate> visualRaw;
        if(cached){if(cached->runtime.visualSuppressionAvailable){const auto& r=cached->runtime;visualRaw=DecodeVisualReaderSequences(pe.bytes(r.visualReaderRva,r.visualReaderBytes.size()),r.visualReaderRva,pe.nt.OptionalHeader.SizeOfImage);}}
        else for(const auto& section:pe.sections)if(section.Characteristics&IMAGE_SCN_MEM_EXECUTE){auto found=DecodeVisualReaderSequences(pe.bytes(section.VirtualAddress,section.SizeOfRawData),section.VirtualAddress,pe.nt.OptionalHeader.SizeOfImage);visualRaw.insert(visualRaw.end(),found.begin(),found.end());}
        AoeSignatureResult visualSignature;visualSignature.name="VisualSfxReader";visualSignature.pattern=Visual;for(const auto& c:visualRaw)visualSignature.matches.push_back(c.rva);
        for(const auto& c:visualRaw)if(prep&&pe.function(c.rva)==pe.function(prep)&&pe.function(c.branchTargetRva)==pe.function(prep)&&pe.executable(c.lookupRva)&&pe.at(c.lookupRva,SfxLookup32))out.visualCandidates.push_back(c);
        out.visualReady=out.visualCandidates.size()==1;out.visualError=out.visualReady?std::string{}:"VISUAL LOCATOR NOT READY: semantic candidate count="+std::to_string(out.visualCandidates.size());visualSignature.valid=out.visualReady;visualSignature.evidence="DWORD [TSkill+0x8C] load; same-register TEST; zero JE skips direct CALL; 32-bit TSFX lookup; same AOE lifecycle function; semantic_matches="+std::to_string(out.visualCandidates.size());if(out.visualReady){out.visualReader=out.visualCandidates.front();visualSignature.rva=out.visualReader.rva;out.liveEvidence.push_back({out.visualReader.rva,out.visualReader.fingerprint});out.liveEvidence.push_back({out.visualReader.lookupRva,pe.bytes(out.visualReader.lookupRva,Pattern(SfxLookup32).bytes.size())});}out.signatures.push_back(std::move(visualSignature));
        bool semantic=prep&&dispatch&&worker&&typed&&typedCall&&producer&&producerCall&&serialization&&lookup&&acquisition&&insertion&&radius&&vector&&parsed;
        // No operation IDs are invented: static flow carries the record WORD.
        // Exact 0209/020A values remain subject to the existing live-cast gate.
        auto neighborhood=[&](uint64_t rva,size_t length){auto bytes=pe.bytes(rva,length);out.liveEvidence.push_back({rva,bytes});return bytes;};
        if(semantic){
            auto body=neighborhood(worker,0x200);
            auto operand=[&](const std::vector<uint8_t>& b,const std::string& pattern,size_t offset)->uint64_t{auto hits=MatchAoePattern(b,pattern);if(hits.size()!=1)throw std::runtime_error("AOE layout evidence ambiguous: "+pattern);return Read<uint32_t>(b,hits[0]+offset);};
            auto& r=out.recovered;
            r.actorEligibilityOffset=operand(body,"40 38 A8 ?? ?? 00 00",3);
            r.actorStatusOffset=operand(body,"38 86 ?? ?? 00 00",2);
            r.localActorOffset=operand(body,"48 83 B9 ?? ?? 00 00 00",3);
            r.actorTypeOffset=operand(body,"41 80 BF ?? ?? 00 00 01",3);
            r.actorIdOffset=Read<uint32_t>(pe.data,pe.offset(insertion-51+2,4));
            r.targetVectorOffset=Read<uint32_t>(pe.data,pe.offset(vector+3,4));
            auto preBody=pe.bytes(pe.function(prep),prepStart-pe.function(prep));
            r.targetMaximumEntries=operand(preBody,"48 83 F8 ?? 0F 87",3)&0xff;
            r.targetRecordBytes=8;
            semantic=r.actorEligibilityOffset>=0x100&&r.actorEligibilityOffset<0x10000&&r.actorStatusOffset>=0x100&&r.actorStatusOffset<0x10000&&r.localActorOffset>=0x100&&r.localActorOffset<0x10000&&r.actorTypeOffset>=0x100&&r.actorTypeOffset<0x10000&&r.actorIdOffset>=0x100&&r.actorIdOffset<0x10000&&r.targetVectorOffset>=0x100&&r.targetVectorOffset<0x10000&&r.targetMaximumEntries>0&&r.targetMaximumEntries<=64;
            if(semantic)RecoverActorTrees(pe,typed,r,out.liveEvidence);
            neighborhood(prepStart,Pattern(prepPattern).bytes.size()+25);
            if(!semantic)out.error="Shared worker eligibility/status/local-actor layout semantic evidence is invalid";
        }
        out.ready=semantic&&std::all_of(out.signatures.begin(),out.signatures.end(),[](const auto&s){return s.name=="VisualSfxReader"||s.valid;});
        if(!out.ready&&out.error.empty()){out.error="AOE signatures unresolved or ambiguous:";for(const auto&s:out.signatures)if(!s.valid)out.error+=' '+s.name;}
        error=out.error;return out.ready;
    }catch(const std::exception& e){out.ready=false;error=out.error=e.what();return false;}
}

bool ValidateAoeProfile(const BuildProfile& profile,const AoeLocatorResult& result,std::string& error){
    if(!result.ready){error="AOE locator is not ready: "+result.error;return false;}
    if(profile.targetSha256!=result.image.sha256){error="AOE profile SHA mismatch";return false;}
    const auto&r=profile.runtime;
    auto equal=[&](const char* name,uint64_t value,int64_t delta=0){auto*s=Find(result,name);return s&&s->valid&&int64_t(s->rva)+delta==int64_t(value);};
    if(!equal("AoeLifecycleCaller",r.initialCallRva)||!equal("AoeLifecycleCaller",r.initialPrepRva,-int64_t(result.prepDistance))||!equal("AoeLifecycleCaller",r.initialReturnRva,5)||!equal("SharedOperation020A",r.sharedWorkerRva)||!equal("OperationProducer",r.producerRva)||!equal("ProducerCall",r.producerCallRva)||!equal("TypedActorLookup",r.typedActorLookupRva)||!equal("TargetVectorBuilder",r.targetBuilderVectorRva)||!equal("ParsedActionBoundary",r.differentialParsedRva)||!equal("RecordSerialization",r.idSerializationRva,14)||!equal("RecordSerialization",r.typeSerializationRva,24)){error="AOE dynamic signature results disagree with exact SHA profile RVAs";return false;}
    if(r.visualSuppressionAvailable&&(!result.visualReady||r.visualReaderRva!=result.visualReader.rva||r.visualLookupRva!=result.visualReader.lookupRva||r.visualBaseRegister!=result.visualReader.baseRegister||r.visualFieldOffset!=result.visualReader.fieldOffset)){error="VisualSfxReader locator/profile semantic mismatch";return false;}
    if(profile.schemaVersion==3){
        const auto& d=result.recovered;
        if(!r.liveValidationRequired||!r.aoeLocatorRequired||r.targetVectorOffset!=d.targetVectorOffset||r.actorIdOffset!=d.actorIdOffset||r.actorTypeOffset!=d.actorTypeOffset||r.actorEligibilityOffset!=d.actorEligibilityOffset||r.actorStatusOffset!=d.actorStatusOffset||r.localActorOffset!=d.localActorOffset||r.targetRecordBytes!=d.targetRecordBytes||r.targetMaximumEntries!=d.targetMaximumEntries||r.actorTreeOffsets!=d.actorTreeOffsets){error="AOE recovered layout disagrees with disk semantics";return false;}
        if(r.initialPrepBytes.size()!=result.prepDistance||r.initialCallBytes.size()!=5||r.initialReturnBytes.size()!=1||r.sharedWorkerBytes.size()!=15||r.producerBytes.size()!=16||r.producerCallBytes.size()!=5||r.typedActorLookupBytes.size()!=38||r.targetBuilderVectorBytes.size()!=15||r.differentialParsedBytes.size()!=6||r.idSerializationBytes.size()!=10||r.typeSerializationBytes.size()!=11){error="AOE incomplete recovery fingerprints";return false;}
    }
    if(!RuntimeLayoutSupportsInitialNx(r,error))return false;
    // Verify the complete profile fingerprint contract, never only its RVAs.
    try{Pe pe(result.image.path);if(Sha256(pe.data.data(),pe.data.size())!=result.image.sha256){error="Target file changed during validation";return false;}
        std::vector<std::pair<uint64_t,std::vector<uint8_t>>> fingerprints={{r.initialPrepRva,r.initialPrepBytes},{r.initialCallRva,r.initialCallBytes},{r.initialReturnRva,r.initialReturnBytes},{r.sharedWorkerRva,r.sharedWorkerBytes},{r.producerRva,r.producerBytes},{r.producerCallRva,r.producerCallBytes},{r.typedActorLookupRva,r.typedActorLookupBytes},{r.targetBuilderVectorRva,r.targetBuilderVectorBytes},{r.differentialParsedRva,r.differentialParsedBytes},{r.idSerializationRva,r.idSerializationBytes},{r.typeSerializationRva,r.typeSerializationBytes}};if(r.visualSuppressionAvailable)fingerprints.push_back({r.visualReaderRva,r.visualReaderBytes});for(const auto& p:fingerprints)if(p.second.empty()||pe.bytes(p.first,p.second.size())!=p.second){error="AOE original-byte mismatch at "+Hex(p.first)+" expected="+BytesHex(p.second);return false;}
    }catch(const std::exception&e){error=e.what();return false;}
    error.clear();return true;
}
bool ValidateAoeLive(HANDLE process,const TargetInfo& target,const AoeLocatorResult& result,std::string& error){
    if(!process||!target.verified||!result.ready||result.liveEvidence.empty()||target.image.sha256!=result.image.sha256||target.size!=result.image.imageSize){error="Invalid AOE live session/build identity";return false;}
    FILETIME created{},exit{},kernel{},user{};if(GetProcessId(process)!=target.pid||!GetProcessTimes(process,&created,&exit,&kernel,&user)||(uint64_t(created.dwHighDateTime)<<32|created.dwLowDateTime)!=target.creationTime){error="AOE process creation identity changed";return false;}
    DWORD code=0;if(!GetExitCodeProcess(process,&code)||code!=STILL_ACTIVE){error="AOE target process is not active";return false;}
    for(const auto&[rva,expected]:result.liveEvidence){uint64_t address=0;if(!ResolveTraceAddress(target.base,target.size,rva,address)||expected.size()>target.size-rva){error="Live AOE evidence is out of module bounds";return false;}std::vector<uint8_t> actual(expected.size());uint32_t e=0;if(!SafeRead(process,address,actual.data(),actual.size(),e)){error=WinError("Read live AOE evidence at "+Hex(address),e);return false;}if(actual!=expected){error="Live AOE original-byte mismatch at "+Hex(rva)+" expected="+BytesHex(expected)+" actual="+BytesHex(actual);return false;}}
    error.clear();return true;
}
#include "aoe_recovery.inc"
}

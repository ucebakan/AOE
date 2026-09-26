#include "producer.hpp"
#include "analysis.hpp"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <iomanip>
#include <limits>
#include <set>
#include <sstream>

namespace aoe {
namespace {
std::string Quote(const std::string& text){std::string s="\"";for(char c:text){if(c=='\"'||c=='\\')s+='\\';if(c=='\n')s+="\\n";else if(c=='\r')s+="\\r";else s+=c;}return s+'\"';}
const char* Bool(bool b){return b?"true":"false";}
float Float(const ProducerArgument& a){uint32_t bits=uint32_t(a.bits);float f;memcpy(&f,&bits,4);return f;}
std::string Real(double n){if(!std::isfinite(n))return "null";std::ostringstream s;s.imbue(std::locale::classic());s<<std::setprecision(17)<<n;return s.str();}
std::vector<uint8_t> Read(HANDLE process,uint64_t address,size_t size,uint32_t& error){
    std::vector<uint8_t> bytes;error=0;
    if(address<0x10000||address>UINT64_MAX-size){error=ERROR_INVALID_ADDRESS;return bytes;}
    // Byte-sized safe reads retain a readable prefix across an inaccessible boundary.
    bytes.resize(size);if(SafeRead(process,address,bytes.data(),size,error))return bytes;bytes.clear();
    bytes.reserve(size);for(size_t i=0;i<size;++i){uint8_t b=0;if(!SafeRead(process,address+i,&b,1,error))break;bytes.push_back(b);}return bytes;
}
std::string Delta(const ProducerArgument& a,const ProducerArgument& b){
    if(!a.readable||!b.readable)return "null";
    if(a.type=="float32")return Real(double(Float(b))-double(Float(a)));
    return b.bits>=a.bits?std::to_string(b.bits-a.bits):"-"+std::to_string(a.bits-b.bits);
}
void Strings(std::ostream& o,const std::vector<std::string>& xs,bool nullable=false){o<<'[';for(size_t i=0;i<xs.size();++i){if(i)o<<',';o<<(nullable&&xs[i]=="null"?"null":Quote(xs[i]));}o<<']';}
}
void DecodeProducerArguments(Event& e){
    auto& p=e.producer;p.arguments.clear();p.captured=true;p.expectedCaller=e.returnReadable&&e.returnInModule&&e.returnRva==ProducerCallerRva;
    auto add=[&](unsigned n,unsigned off,unsigned width,const char* type,const char* source,uint64_t bits,bool reg){
        ProducerArgument a;a.number=n;a.stackOffset=off;a.width=width;a.type=type;a.source=source;
        a.readable=reg||p.rawStack.size()>=off+width;
        if(a.readable){a.raw.resize(width);if(reg)memcpy(a.raw.data(),&bits,width);else memcpy(a.raw.data(),p.rawStack.data()+off,width);memcpy(&a.bits,a.raw.data(),width);}p.arguments.push_back(std::move(a));
    };
    add(1,0,8,"pointer","RCX",e.rcx,true);add(2,0,4,"uint32","EDX",e.rdx,true);add(3,0,4,"uint32","R8D",e.r8,true);add(4,0,1,"uint8","R9B",e.r9,true);
    const unsigned offsets[]={0x28,0x30,0x38,0x40,0x48,0x50,0x58,0x60,0x68,0x70,0x78,0x80,0x88,0x90};
    for(unsigned i=0;i<14;++i){bool f=i<3||(i>=8&&i<=10);auto source="RSP+"+Hex(offsets[i]);add(i+5,offsets[i],i==3?8:i==4?2:4,f?"float32":i==3?"pointer":i==4?"uint16":"uint32",source.c_str(),0,false);}
    p.vectorPointer=p.arguments[7].readable?p.arguments[7].bits:0;
}
bool ValidateProducerVector(ProducerSnapshot& p){
    p.vectorValid=false;p.elementCount=0;
    if(!p.vectorHeaderReadable){p.vectorState="header unreadable";return false;}
    if(p.vectorPointer%8||p.begin%8||p.end%8||p.capacity%8){p.vectorState="unaligned candidate pointers";return false;}
    if(p.capacity>0x0000800000000000ull){p.vectorState="outside user address range";return false;}
    if(p.begin>p.end||p.end>p.capacity){p.vectorState="unordered candidate pointers";return false;}
    if((!p.begin&&(p.end||p.capacity))||(p.begin&&p.begin<0x10000)){p.vectorState="invalid begin candidate";return false;}
    if((p.end-p.begin)%8||(p.capacity-p.begin)%8){p.vectorState="span not divisible by eight";return false;}
    if((p.end-p.begin)/8>65536||(p.capacity-p.begin)/8>1048576){p.vectorState="candidate count exceeds sanity bounds";return false;}
    p.elementCount=(p.end-p.begin)/8;p.vectorValid=true;p.vectorState="validated candidate layout; pointees validated separately";return true;
}
void SnapshotProducer(HANDLE process,Event& e){
    e.producer={};auto& p=e.producer;p.rawStack=Read(process,e.rsp,0xA0,p.stackError);p.stackReadable=p.rawStack.size()==0xA0;DecodeProducerArguments(e);
    if(!p.arguments[7].readable){p.vectorState="arg8 unreadable";return;}
    p.vectorHeader=Read(process,p.vectorPointer,24,p.vectorError);p.vectorHeaderReadable=p.vectorHeader.size()==24;
    if(p.vectorHeaderReadable){memcpy(&p.begin,p.vectorHeader.data(),8);memcpy(&p.end,p.vectorHeader.data()+8,8);memcpy(&p.capacity,p.vectorHeader.data()+16,8);}
    if(!ValidateProducerVector(p))return;
    for(uint64_t i=0;i<std::min<uint64_t>(64,p.elementCount);++i){VectorElement x;x.pointerReadable=SafeRead(process,p.begin+i*8,&x.pointer,8,x.error);if(x.pointerReadable){x.bytes=Read(process,x.pointer,16,x.error);x.recordReadable=x.bytes.size()==16;}p.elements.push_back(std::move(x));}
}
std::string ArgumentText(const ProducerArgument& a){
    if(!a.readable)return "unreadable";
    if(a.type=="float32"){auto f=Float(a);std::string text=std::isnan(f)?"NaN":std::isinf(f)?(f<0?"-Infinity":"Infinity"):Real(f);return text+" (bits "+Hex(uint32_t(a.bits))+")";}
    return std::to_string(a.bits)+" ("+Hex(a.bits)+")";
}
std::vector<FieldStability> CompareProducerFields(const Capture& c){
    std::vector<const Event*> events;for(auto& e:c.events)events.push_back(&e);std::stable_sort(events.begin(),events.end(),[](auto a,auto b){return a->ms<b->ms;});
    std::vector<FieldStability> fields;
    for(unsigned n=0;n<18;++n){FieldStability f;f.field="arg"+std::to_string(n+1);std::set<uint64_t> distinct;const ProducerArgument* previous=nullptr;
        for(auto e:events){const ProducerArgument* a=e->producer.arguments.size()>n?&e->producer.arguments[n]:nullptr;
            if(a)f.type=a->type;const bool valid=a&&a->readable;
            if(valid){++f.observations;distinct.insert(a->bits);auto text=ArgumentText(*a);if(f.observations==1)f.first=text;f.last=text;f.sequence.push_back(text);f.rawValues.push_back(Hex(a->bits));}
            else{++f.missing;f.sequence.push_back("null");f.rawValues.push_back("null");}
            if(f.sequence.size()>1)f.deltas.push_back(previous&&a?Delta(*previous,*a):"null");previous=a;
        }
        f.distinct=distinct.size();f.constant=f.observations>0&&f.missing==0&&f.distinct==1;fields.push_back(std::move(f));
    }return fields;
}
void WriteProducerJson(std::ostream& o,const Event& e){
    auto& p=e.producer;if(!p.captured){o<<"null";return;}
    o<<"{\"expectedCaller\":"<<Bool(p.expectedCaller)<<",\"rawEntryStack\":"<<Quote(BytesHex(p.rawStack,p.rawStack.size()))<<",\"stackRequestedBytes\":160,\"stackReadable\":"<<Bool(p.stackReadable)<<",\"stackReadError\":"<<p.stackError<<",\"arguments\":[";
    for(size_t i=0;i<p.arguments.size();++i){if(i)o<<',';auto& a=p.arguments[i];o<<"{\"name\":"<<Quote("arg"+std::to_string(a.number))<<",\"source\":"<<Quote(a.source)<<",\"type\":"<<Quote(a.type)<<",\"stackOffset\":"<<(a.number>4?std::to_string(a.stackOffset):"null")<<",\"readable\":"<<Bool(a.readable)<<",\"rawBytes\":"<<Quote(BytesHex(a.raw))<<",\"rawHex\":"<<(a.readable?Quote(Hex(a.bits)):"null")<<",\"value\":"<<(!a.readable?"null":a.type=="float32"?Real(Float(a)):a.type=="pointer"?Quote(std::to_string(a.bits)):std::to_string(a.bits))<<",\"floatRawUint32\":"<<(a.readable&&a.type=="float32"?std::to_string(uint32_t(a.bits)):"null")<<",\"display\":"<<Quote(ArgumentText(a))<<'}';}
    o<<"],\"vector\":{\"pointer\":"<<Quote(Hex(p.vectorPointer))<<",\"headerReadable\":"<<Bool(p.vectorHeaderReadable)<<",\"headerRawBytes\":"<<Quote(BytesHex(p.vectorHeader))<<",\"readError\":"<<p.vectorError<<",\"valid\":"<<Bool(p.vectorValid)<<",\"state\":"<<Quote(p.vectorState)<<",\"begin\":"<<(p.vectorHeaderReadable?Quote(Hex(p.begin)):"null")<<",\"end\":"<<(p.vectorHeaderReadable?Quote(Hex(p.end)):"null")<<",\"capacity\":"<<(p.vectorHeaderReadable?Quote(Hex(p.capacity)):"null")<<",\"elementCount\":"<<(p.vectorValid?std::to_string(p.elementCount):"null")<<",\"truncated\":"<<Bool(p.vectorValid&&p.elementCount>p.elements.size())<<",\"elements\":[";
    for(size_t i=0;i<p.elements.size();++i){if(i)o<<',';auto& x=p.elements[i];o<<"{\"index\":"<<i<<",\"pointerReadable\":"<<Bool(x.pointerReadable)<<",\"pointer\":"<<(x.pointerReadable?Quote(Hex(x.pointer)):"null")<<",\"recordReadable\":"<<Bool(x.recordReadable)<<",\"readError\":"<<x.error<<",\"first16Bytes\":"<<Quote(BytesHex(x.bytes,16))<<'}';}o<<"]}}";
}
void WriteProducerFieldsJson(std::ostream& o,const std::vector<FieldStability>& fields){o<<'[';for(size_t i=0;i<fields.size();++i){if(i)o<<',';auto& f=fields[i];o<<"{\"field\":"<<Quote(f.field)<<",\"type\":"<<Quote(f.type)<<",\"observations\":"<<f.observations<<",\"missing\":"<<f.missing<<",\"distinct\":"<<f.distinct<<",\"constant\":"<<Bool(f.constant)<<",\"first\":"<<(f.observations?Quote(f.first):"null")<<",\"last\":"<<(f.observations?Quote(f.last):"null")<<",\"sequence\":";Strings(o,f.sequence,true);o<<",\"rawValues\":";Strings(o,f.rawValues,true);o<<",\"consecutiveDeltas\":";Strings(o,f.deltas,true);o<<'}';}o<<']';}
std::string ProducerDetails(const Event& e){std::ostringstream o;auto& p=e.producer;o<<"RIP "<<Hex(e.rip)<<" | "<<(p.expectedCaller?"expected caller":"unexpected / unreadable caller; retained")<<"\r\nRaw entry stack: "<<BytesHex(p.rawStack,p.rawStack.size())<<"\r\n";for(auto& a:p.arguments)o<<"arg"<<a.number<<" ["<<a.source<<"] "<<ArgumentText(a)<<" raw="<<BytesHex(a.raw)<<"\r\n";o<<"Vector "<<Hex(p.vectorPointer)<<": "<<p.vectorState<<"; begin/end/capacity "<<Hex(p.begin)<<" / "<<Hex(p.end)<<" / "<<Hex(p.capacity)<<"; count "<<(p.vectorValid?std::to_string(p.elementCount):"unknown")<<"\r\n";for(size_t i=0;i<p.elements.size();++i){auto& x=p.elements[i];o<<i<<": "<<Hex(x.pointer)<<" bytes="<<BytesHex(x.bytes,16)<<" readError="<<x.error<<"\r\n";}o<<ProducerLimitation;return o.str();}
std::string ProducerFieldDetails(const std::vector<FieldStability>& fields){std::ostringstream o;for(auto& f:fields){o<<f.field<<" observations="<<f.observations<<" missing="<<f.missing<<" distinct="<<f.distinct<<" constant="<<(f.constant?"yes":"no")<<" first="<<f.first<<" last="<<f.last<<"\r\nSequence: ";for(auto& x:f.sequence)o<<x<<"; ";o<<"\r\nConsecutive deltas: ";for(auto& x:f.deltas)o<<x<<"; ";o<<"\r\n";}return o.str();}
Capture SyntheticProducerCapture(){auto c=ObservedTimingFixture();c.id=1092;c.target.mode=TraceMode::Producer;c.target.traceRva=ProducerRva;c.target.traceAddress=c.target.base+ProducerRva;c.requestedSeconds=15;c.durationMs=15000;c.endQpc=c.startQpc+15*c.frequency;c.complete=true;c.stopReason="Synthetic producer demonstration; no live capture";c.events.clear();for(unsigned i=0;i<9;++i){Event e;e.sequence=i+1;e.ms=1000+i*997;e.qpc=c.startQpc+int64_t(e.ms*c.frequency/1000);e.deltaMs=i?997:0;e.rip=c.target.traceAddress;e.rcx=0x100000;e.rdx=0x1234;e.r8=0x5678;e.r9=1;e.returnReadable=e.returnInModule=true;e.returnRva=ProducerCallerRva;e.returnAddress=c.target.base+e.returnRva;e.producer.rawStack.resize(0xA0);memcpy(e.producer.rawStack.data()+0x50,&i,4);e.producer.stackReadable=true;DecodeProducerArguments(e);e.producer.vectorState="synthetic; not dereferenced";c.events.push_back(std::move(e));}c.markers.push_back({1,"AOE Cast 1",c.startQpc+c.frequency,1000});return c;}
}

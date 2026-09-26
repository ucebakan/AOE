#include "analysis.hpp"
#include <algorithm>
#include <array>
#include <cmath>
#include <iomanip>
#include <sstream>

namespace aoe {
std::vector<uint64_t> StackFingerprint(const Event& event) {
    auto candidates=event.stackCandidates;
    std::stable_sort(candidates.begin(),candidates.end(),[](const auto& a,const auto& b){return a.stackOffset<b.stackOffset;});
    std::vector<uint64_t> result;
    for(const auto& candidate:candidates) {
        if(!candidate.stackOffset||(candidate.stackOffset%8)||!candidate.rva||candidate.rva==0xA12B1E||candidate.rva==0x7EDEBF||candidate.rva==event.returnRva)continue;
        if(std::find(result.begin(),result.end(),candidate.rva)!=result.end())continue;
        result.push_back(candidate.rva);if(result.size()==4)break;
    }
    return result;
}
std::string FingerprintText(const Event& event) {
    std::string result;for(auto rva:StackFingerprint(event)){if(!result.empty())result+=" / ";result+=Hex(rva);}
    return result.empty()?"unavailable":result;
}
MarkerRelation RelativeToMarkers(const Capture& capture,double ms) {
    MarkerRelation result;const Marker* first=nullptr;const Marker* nearest=nullptr;
    for(const auto& marker:capture.markers) {
        if(!std::isfinite(marker.ms))continue;
        if(!first||marker.ms<first->ms)first=&marker;
        if(!nearest||std::abs(ms-marker.ms)<std::abs(ms-nearest->ms))nearest=&marker;
    }
    if(first&&nearest){result.available=true;result.firstId=first->id;result.nearestId=nearest->id;result.firstRelativeMs=ms-first->ms;result.nearestRelativeMs=ms-nearest->ms;}
    return result;
}
namespace {
std::string Decimal(double number){std::ostringstream out;out<<std::fixed<<std::setprecision(3)<<number;return out.str();}
bool Readable(const Event& e){return e.bufferReadable&&!e.bytes.empty()&&e.bytes.size()<=128&&e.bytes.size()<=e.packetLength;}
void ByteProfile(Group& group,const Capture& capture,const Capture* idle) {
    std::vector<const Event*> samples;
    for(auto index:group.eventIndices)if(Readable(capture.events[index]))samples.push_back(&capture.events[index]);
    group.maskSamples=samples.size();if(samples.size()<4)return;
    size_t width=0;for(auto e:samples)width=std::max(width,e->bytes.size());
    std::ostringstream mask;
    for(size_t offset=0;offset<width;++offset) {
        std::array<uint32_t,256> histogram{};ByteStability b;b.offset=uint32_t(offset);
        for(auto e:samples)if(offset<e->bytes.size()){++histogram[e->bytes[offset]];++b.samples;}
        for(size_t value=0;value<histogram.size();++value)if(histogram[value]) {
            ++b.distinctValues;b.value=uint8_t(value);const double p=double(histogram[value])/b.samples;b.entropyBits-=p*std::log2(p);
        }
        b.constant=b.samples==samples.size()&&b.distinctValues==1;
        if(offset)mask<<' ';if(b.constant)mask<<std::hex<<std::uppercase<<std::setw(2)<<std::setfill('0')<<unsigned(b.value);else mask<<"??";
        group.byteStability.push_back(b);
    }
    group.maskedSignature=mask.str();
    group.maskAvailable=std::any_of(group.byteStability.begin(),group.byteStability.end(),[](auto& b){return b.constant;});
    if(!group.maskAvailable)return; // An all-wildcard mask supplies no pattern evidence.
    if(idle)for(const auto& event:idle->events) {
        if(event.packetLength!=group.packetSize||!Readable(event))continue;
        if(group.kind=="size_fingerprint"&&FingerprintText(event)!=group.fingerprint)continue;
        bool matches=true;
        for(auto& b:group.byteStability)if(b.constant&&(event.bytes.size()<=b.offset||event.bytes[b.offset]!=b.value)){matches=false;break;}
        if(matches)++group.maskedIdleCount;
    }
    group.reasons.push_back("Byte mask uses only offsets constant in every readable sample (>=4); missing/variable positions are ??. The identical mask is checked against IDLE. Constants, including padding, are not decoded packet types.");
}
void Hypothesis(Group& group,const Capture& capture) {
    auto& h=group.hypothesis;h.periodicCount=group.count;h.totalPossibleCount=group.count;h.spanMs=group.lastMs-group.firstMs;
    h.spanNear8Seconds=std::abs(h.spanMs-8000)<=600;
    h.assessment="Not assessed: requires a strong periodic timing candidate and a cast marker. No damage timing correlation is available.";
    if(!group.strongTimingCandidate)return;
    if(capture.markers.empty()){h.assessment="Periodic sequence observed; initial event/activation unknown without a cast marker. Timing alone does not prove the 1+8 hypothesis.";return;}
    if(group.count==9) {
        const auto marker=RelativeToMarkers(capture,group.firstMs);
        if(marker.available&&std::abs(marker.nearestRelativeMs)<=350) {
            h.possibleInitialEventIndex=group.eventIndices.front();h.initialCandidateCount=1;h.markerId=marker.nearestId;h.periodicCount=8;
            h.consistentOnePlusEight=h.spanNear8Seconds&&group.near1sFraction==1.0;
        }
    } else if(group.count==8&&group.fingerprintRvas.size()>=2) {
        // A differently sized activation request may precede the eight ticks.
        // Require the same observed deeper fingerprint and a nearby manual marker.
        for(size_t i=0;i<capture.events.size();++i) {
            const auto& event=capture.events[i];const double gap=group.firstMs-event.ms;
            if(gap<=0||std::abs(gap-group.medianDeltaMs)>180||FingerprintText(event)!=group.fingerprint)continue;
            const auto marker=RelativeToMarkers(capture,event.ms);
            if(!marker.available||std::abs(marker.nearestRelativeMs)>350)continue;
            ++h.initialCandidateCount;h.possibleInitialEventIndex=int64_t(i);h.markerId=marker.nearestId;
        }
        if(h.initialCandidateCount==1) {
            h.totalPossibleCount=9;h.spanMs=group.lastMs-capture.events[size_t(h.possibleInitialEventIndex)].ms;
            h.spanNear8Seconds=std::abs(h.spanMs-8000)<=600;
            h.consistentOnePlusEight=h.spanNear8Seconds&&group.near1sFraction==1.0;
        }else {h.possibleInitialEventIndex=-1;h.markerId=0;}
    }
    if(h.consistentOnePlusEight)h.assessment="Consistent with 1 initial + 8 periodic events. Hypothesis only: correlate with actual damage timestamps; a manual marker is not a confirmed activation timestamp.";
    else if(h.initialCandidateCount>1)h.assessment="Multiple possible initial requests near the marker; initial event is ambiguous. The 1+8 hypothesis is not established.";
    else h.assessment="No complete marker-aligned 1+8 sequence established. Missing events, boundary truncation or a different producer remain possible; no damage-event proof.";
}
}
void AddTimingResearch(const Capture& capture,const Capture* idle,Analysis& result) {
    const Capture* baseline=result.idleCompatible?idle:nullptr;
    for(auto& group:result.groups) {
        if(group.eventIndices.empty())continue;
        group.firstMs=group.timestampsMs.front();group.lastMs=group.timestampsMs.back();
        const auto& exemplar=capture.events[group.eventIndices.front()];
        group.fingerprint=FingerprintText(exemplar);group.fingerprintRvas=StackFingerprint(exemplar);
        for(auto index:group.eventIndices)if(FingerprintText(capture.events[index])!=group.fingerprint){group.fingerprint="mixed";group.fingerprintRvas.clear();break;}
        if(!group.deltasMs.empty()) {
            double variance=0;size_t near100=0,near250=0;
            for(auto delta:group.deltasMs){variance+=(delta-group.meanDeltaMs)*(delta-group.meanDeltaMs);if(std::abs(delta-1000)<=100)++near100;if(std::abs(delta-1000)<=250)++near250;}
            group.standardDeviationMs=std::sqrt(variance/group.deltasMs.size());
            group.near100Fraction=double(near100)/group.deltasMs.size();group.near250Fraction=double(near250)/group.deltasMs.size();
        }
        group.expectedNextTimestampMs=group.lastMs+group.medianDeltaMs;
        group.expectedPreviousTimestampMs=group.firstMs-group.medianDeltaMs;
        if(baseline)group.idleRate=double(group.idleCount)*1000/baseline->durationMs;
        group.aoeOnly=capture.label==Label::Aoe&&baseline&&group.idleCount==0;
        group.background=baseline&&group.idleRate>0&&group.aoeRate<=1.5*group.idleRate;
        const bool contrast=baseline&&(group.idleCount==0||group.aoeRate>=2*group.idleRate);
        const bool eligible=group.kind!="caller"&&group.packetSize>0;
        group.strongTimingCandidate=eligible&&group.periodicNear1s&&contrast&&capture.label==Label::Aoe&&!capture.droppedEvents&&!baseline->droppedEvents;
        group.category=group.strongTimingCandidate?"PERIODIC TIMING CANDIDATE":group.periodicNear1s?"PERIODIC TIMING OBSERVATION":"RAW GROUP";
        group.likelyTruncated=group.periodicNear1s&&group.medianDeltaMs>0&&group.expectedNextTimestampMs>capture.durationMs&&group.expectedNextTimestampMs-capture.durationMs<=1.5*group.medianDeltaMs;
        group.possibleEarlierEvents=group.periodicNear1s&&group.expectedPreviousTimestampMs<0&&-group.expectedPreviousTimestampMs<=1.5*group.medianDeltaMs;
        group.truncationAssessment=group.likelyTruncated?"Sequence likely truncated by capture boundary; extrapolated next event is shortly after capture end. It is not evidence that another event actually occurred.":"No right-boundary truncation inferred from the next-interval estimate.";
        if(group.possibleEarlierEvents)group.truncationAssessment+=" An earlier periodic event may precede capture start (one-interval extrapolation).";
        if(group.kind=="size_fingerprint"||(group.kind=="size"&&group.packetSize==78))ByteProfile(group,capture,baseline);
        if(group.strongTimingCandidate) {
            group.reasons.push_back("Timing classification needs no byte equality and no 8-10 count gate: >=5 observations, >=75% gaps within 1000 +/-180ms, mean within that band, population CV<=0.20, and zero IDLE count or >=2x normalized IDLE rate.");
            group.score=std::max(group.score,70+20*(1-std::min(1.0,group.coefficientVariation/0.20)));
        }
        if(group.kind=="size_fingerprint")group.notes="Size plus the first up to four distinct deeper stack-scan RVAs, excluding immediate return, A12B1E and 7EDEBF. A fingerprint is not a canonical unwind; missing frames can split a producer and coincidental pointers can merge producers.";
        Hypothesis(group,capture);
        group.status=group.background?"Background":group.aoeOnly?"AOE-only":baseline?"IDLE compared":"IDLE unavailable";
        if(group.periodicNear1s)group.status+=" | Periodic ~1s";
        if(group.likelyTruncated)group.status+=" | Likely truncated";
        if(group.nearNine)group.status+=" | Near 9 events";
        if(group.hypothesis.consistentOnePlusEight)group.status+=" | Consistent with 1+8 hypothesis";
        if(capture.label==Label::Idle)group.status="IDLE reference"+(group.periodicNear1s?std::string(" | Periodic ~1s"):std::string{});
    }
    std::stable_sort(result.groups.begin(),result.groups.end(),[](const Group& a,const Group& b){
        if(a.strongTimingCandidate!=b.strongTimingCandidate)return a.strongTimingCandidate>b.strongTimingCandidate;
        if(a.score!=b.score)return a.score>b.score;
        if((a.kind=="size_fingerprint")!=(b.kind=="size_fingerprint"))return a.kind=="size_fingerprint";
        return a.key<b.key;
    });
    auto& diagnostic=result.size78;
    for(size_t i=0;i<capture.events.size();++i)if(capture.events[i].packetLength==78)diagnostic.eventIndices.push_back(uint32_t(i));
    std::stable_sort(diagnostic.eventIndices.begin(),diagnostic.eventIndices.end(),[&](auto a,auto b){return capture.events[a].ms<capture.events[b].ms;});
    if(baseline)for(auto& event:baseline->events)if(event.packetLength==78)++diagnostic.idleCount;
    diagnostic.captureRate=capture.durationMs>0?diagnostic.eventIndices.size()*1000.0/capture.durationMs:0;
    diagnostic.idleRate=baseline?diagnostic.idleCount*1000.0/baseline->durationMs:0;
    diagnostic.summary="78-byte diagnostic: "+std::to_string(diagnostic.eventIndices.size())+" capture events ("+Decimal(diagnostic.captureRate)+"/s); IDLE "+(baseline?std::to_string(diagnostic.idleCount)+" ("+Decimal(diagnostic.idleRate)+"/s)":"unavailable")+". Length 78 is a research filter, not an AOE label.";
    size_t strong=0;bool interesting78=false;
    for(auto& group:result.groups)if(group.strongTimingCandidate){++strong;if(group.packetSize==78)interesting78=true;}
    if(interesting78)diagnostic.summary+=" PERIODIC TIMING CANDIDATE present; inspect size/fingerprint groups and boundary estimates.";
    result.summary=(capture.synthetic?"SYNTHETIC. ":"")+std::to_string(strong)+" PERIODIC TIMING CANDIDATE groups (overlapping, not additive). Timing classification does not require equal bytes. No damage/AOE behavior proven. "+result.idleReference;
}
Capture ObservedTimingFixture() {
    auto capture=SyntheticCaptures()[1];capture.events.clear();capture.markers.clear();capture.id=1078;
    capture.requestedSeconds=10;capture.durationMs=10000;capture.endQpc=capture.startQpc+10*capture.frequency;
    capture.stopReason="Supplied seven-event timing regression; packet bytes and fingerprints are synthetic";
    const double times[]{3251.1716,4233.9698,5236.1199,6237.8237,7239.4411,8245.0131,9246.7828};
    for(size_t i=0;i<std::size(times);++i){Event e;e.sequence=i+1;e.ms=times[i];e.qpc=capture.startQpc+int64_t(e.ms*capture.frequency/1000);e.deltaMs=i?e.ms-times[i-1]:0;e.packetLength=78;e.r8=78;e.bytes.resize(78);for(size_t j=0;j<78;++j)e.bytes[j]=uint8_t(i*31+j*17);e.bytes[0]=0x4e;e.bytes[1]=0;e.bytes[5]=0x31;e.bufferReadable=e.fullPacket=e.returnReadable=e.returnInModule=true;e.returnRva=0xA12B1E;e.returnAddress=capture.target.base+e.returnRva;e.stackCandidates={{0,e.returnAddress,e.returnRva,true},{32,capture.target.base+0x99479,0x99479,true},{64,capture.target.base+0xA0DD71,0xA0DD71,true}};e.signature=Sha256(e.bytes.data(),e.bytes.size());capture.events.push_back(std::move(e));}
    return capture;
}
}

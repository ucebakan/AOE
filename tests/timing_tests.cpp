#include "analysis.hpp"
#include <algorithm>
#include <cmath>
#include <fstream>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>

namespace {
void Require(bool ok,const char* message){if(!ok)throw std::runtime_error(message);}
const aoe::Group& Group(const aoe::Analysis& a,const std::string& kind="size_fingerprint"){
    for(auto& g:a.groups)if(g.kind==kind&&g.packetSize==78)return g;
    throw std::runtime_error("78-byte group missing");
}
aoe::Capture Periodic(unsigned count,double first=2000,double duration=13000){
    auto c=aoe::ObservedTimingFixture();auto prototype=c.events.front();c.events.clear();c.requestedSeconds=duration/1000;c.durationMs=duration;c.endQpc=c.startQpc+int64_t(duration*c.frequency/1000);
    for(unsigned i=0;i<count;++i){auto e=prototype;e.sequence=i+1;e.ms=first+i*1000;e.qpc=c.startQpc+int64_t(e.ms*c.frequency/1000);e.deltaMs=i?1000:0;for(size_t j=2;j<e.bytes.size();++j)e.bytes[j]=uint8_t(i+j*3);e.bytes[5]=0x31;e.signature=aoe::Sha256(e.bytes.data(),e.bytes.size());c.events.push_back(e);}
    return c;
}
void Mark(aoe::Capture& c,double ms,uint64_t id=1){c.markers.push_back({id,"AOE Cast "+std::to_string(id),c.startQpc+int64_t(ms*c.frequency/1000),ms});}
}
unsigned TimingTests(const std::filesystem::path& artifacts){
    unsigned passed=0;auto check=[&](const char* name,const std::function<void()>& test){test();++passed;std::cout<<"[PASS] "<<name<<'\n';};
    auto idle=aoe::SyntheticCaptures()[0];
    check("duration defaults and whole-second 5-30 validation",[]{
        Require(aoe::Capture{}.requestedSeconds==15,"capture default must be 15 seconds");
        for(double valid:{5.0,13.0,15.0,30.0})Require(aoe::ValidCaptureDuration(valid),"valid duration rejected");
        for(double invalid:{-1.0,0.0,4.0,5.5,30.1,31.0,std::numeric_limits<double>::infinity(),std::numeric_limits<double>::quiet_NaN()})Require(!aoe::ValidCaptureDuration(invalid),"invalid duration accepted");
        int seconds=0;Require(aoe::ParseCaptureDuration(L"13",seconds)&&seconds==13,"parse 13");
        for(auto text:{L"",L"13.0",L"13x",L" 13",L"-5",L"31",L"999"})Require(!aoe::ParseCaptureDuration(text,seconds),"UI parser must reject entire invalid input");
    });
    check("real seven-timestamp regression is periodic AOE-only and truncated",[&]{
        auto c=aoe::ObservedTimingFixture();auto a=aoe::Analyze(c,&idle);
        for(auto kind:{"size","size_fingerprint"}){
            auto& g=Group(a,kind);Require(g.count==7&&g.strongTimingCandidate&&g.aoeOnly&&g.likelyTruncated,"seven events must surface independently of near-nine");
            Require(g.category=="PERIODIC TIMING CANDIDATE"&&!g.nearNine,"distinct timing category without near-nine gate");
            Require(std::abs(g.meanDeltaMs-999.2685333333)<0.0001&&std::abs(g.medianDeltaMs-1001.7364)<0.001,"regression mean/median independently calculated");
            Require(g.near100Fraction==1&&g.near1sFraction==1&&g.near250Fraction==1&&g.coefficientVariation<0.01,"low variation and all tolerance bands");
            Require(g.expectedNextTimestampMs>10000&&g.expectedNextTimestampMs<10300,"next event shortly beyond ten seconds");
            Require(!g.hypothesis.consistentOnePlusEight,"seven observations are not proof of 1+8");
        }
        Require(a.size78.eventIndices.size()==7&&a.size78.idleCount==0,"78 diagnostic retains all regression events");
        std::string error;Require(aoe::ExportJson(artifacts/"observed_timing_regression.json",c,a,error),"regression JSON export");
    });
    check("changing every byte still permits a timing candidate",[&]{
        auto c=Periodic(7);for(size_t i=0;i<c.events.size();++i){for(size_t j=0;j<78;++j)c.events[i].bytes[j]=uint8_t(i+j);c.events[i].signature=aoe::Sha256(c.events[i].bytes.data(),78);}
        auto a=aoe::Analyze(c,&idle);auto& g=Group(a);Require(g.strongTimingCandidate&&!g.maskAvailable,"no constants or equal hashes needed");
        Require(g.byteStability.size()==78&&g.byteStability.front().distinctValues==7,"all byte variability reported");
    });
    check("full eight-event series is retained without inventing an initial event",[&]{
        auto c=Periodic(8);Mark(c,1000);auto a=aoe::Analyze(c,&idle);auto& g=Group(a);
        Require(g.strongTimingCandidate&&!g.likelyTruncated,"full eight-event periodic series");
        Require(g.hypothesis.periodicCount==8&&g.hypothesis.totalPossibleCount==8&&!g.hypothesis.consistentOnePlusEight,"missing initial remains unknown");
    });
    check("marker-aligned nine events are consistent with a 1+8 hypothesis",[&]{
        auto c=Periodic(9,1000);Mark(c,980);auto a=aoe::Analyze(c,&idle);auto& h=Group(a).hypothesis;
        Require(h.consistentOnePlusEight&&h.periodicCount==8&&h.totalPossibleCount==9&&h.possibleInitialEventIndex==0&&h.spanNear8Seconds,"1 initial plus 8 periodic hypothesis");
        Require(h.assessment.find("Hypothesis only")!=std::string::npos,"must not claim damage proof");
        c.markers.clear();Require(!Group(aoe::Analyze(c,&idle)).hypothesis.consistentOnePlusEight,"no activation assumption without marker");
    });
    check("different-size initial event requires unique matching fingerprint and marker",[&]{
        auto c=Periodic(8,2000);auto initial=c.events.front();initial.ms=1000;initial.packetLength=64;initial.r8=64;initial.bytes.resize(64);c.events.insert(c.events.begin(),initial);Mark(c,1000);
        auto a=aoe::Analyze(c,&idle);auto& h=Group(a).hypothesis;Require(h.consistentOnePlusEight&&h.periodicCount==8&&h.totalPossibleCount==9,"initial request matched to eight periodic events");
        c.events.insert(c.events.begin(),initial);Require(!Group(aoe::Analyze(c,&idle)).hypothesis.consistentOnePlusEight,"ambiguous initial requests must not pick one arbitrarily");
    });
    check("multiple cast markers preserve signed first and nearest relative time",[]{
        auto c=Periodic(5);Mark(c,1000);Mark(c,5000,2);auto r=aoe::RelativeToMarkers(c,4800);
        Require(r.available&&r.firstId==1&&r.nearestId==2&&r.firstRelativeMs==3800&&r.nearestRelativeMs==-200,"marker relation is signed and identifies reference");
    });
    check("population standard deviation and three tolerance bands",[&]{
        auto c=Periodic(5);double times[]{0,1100,2250,3470,4470};for(size_t i=0;i<5;++i)c.events[i].ms=times[i];auto a=aoe::Analyze(c,&idle);auto& g=Group(a);
        Require(g.near100Fraction==0.5&&g.near1sFraction==0.75&&g.near250Fraction==1,"independent tolerance fractions");
        Require(std::abs(g.standardDeviationMs-80.1171017948)<0.0001,"population standard deviation includes divisor N");
    });
    check("boundary assessment distinguishes room after sequence and missing earlier event",[&]{
        auto c=Periodic(7,3250,10000);Require(Group(aoe::Analyze(c,&idle)).likelyTruncated,"short window truncation");
        c.durationMs=13000;c.requestedSeconds=13;Require(!Group(aoe::Analyze(c,&idle)).likelyTruncated,"long window with no next event is not truncated");
        c=Periodic(7,250);Require(Group(aoe::Analyze(c,&idle)).possibleEarlierEvents,"one interval extrapolates before start");
    });
    check("shared periodic background and unknown IDLE cannot become strong candidates",[&]{
        auto c=Periodic(9,1000);auto baseline=c;baseline.label=aoe::Label::Idle;
        auto a=aoe::Analyze(c,&baseline);Require(Group(a).background&&!Group(a).strongTimingCandidate,"same normalized rate is background");
        Require(!Group(aoe::Analyze(c,nullptr)).strongTimingCandidate,"missing IDLE is unknown, not absent");
        baseline.events.resize(3);Require(Group(aoe::Analyze(c,&baseline)).strongTimingCandidate,"threefold rise surfaces candidate");
        baseline.droppedEvents=1;Require(!Group(aoe::Analyze(c,&baseline)).strongTimingCandidate,"dropped idle must not inflate contrast into strong evidence");
    });
    check("deeper fingerprints separate producers sharing A12B1E and packet size",[&]{
        auto c=Periodic(7);auto second=c.events;for(auto& e:second){e.ms+=100;e.stackCandidates[1].rva=0x7BB4BC;e.stackCandidates[1].address=c.target.base+0x7BB4BC;}c.events.insert(c.events.end(),second.begin(),second.end());
        auto a=aoe::Analyze(c,&idle);unsigned producers=0;for(auto& g:a.groups)if(g.kind=="size_fingerprint"&&g.packetSize==78){++producers;Require(g.count==7&&g.strongTimingCandidate,"independent producer sequence");}
        Require(producers==2&&!Group(a,"size").periodicNear1s,"merged length-only timing is not falsely periodic");
        auto e=c.events.front();e.stackCandidates.insert(e.stackCandidates.begin()+1,{8,c.target.base+0x7EDEBF,0x7EDEBF,true});e.stackCandidates.push_back(e.stackCandidates[2]);
        Require(aoe::StackFingerprint(e)==aoe::StackFingerprint(c.events.front()),"wrapper frames and duplicates normalized");
    });
    check("byte stability mask reports constants distinct values entropy and idle matches",[&]{
        auto c=Periodic(7);auto baseline=c;baseline.label=aoe::Label::Idle;baseline.events.resize(2);baseline.events[1].bytes[0]=0x99;
        auto a=aoe::Analyze(c,&baseline);auto& g=Group(a);
        Require(g.maskSamples==7&&g.maskAvailable&&g.byteStability[0].constant&&g.byteStability[0].distinctValues==1,"constant bytes reported");
        Require(!g.byteStability[2].constant&&g.byteStability[2].distinctValues==7&&g.byteStability[2].entropyBits>2.8,"changing sequence bytes quantified");
        Require(g.maskedSignature.find("4E 00 ??")!=std::string::npos&&g.maskedIdleCount==1&&g.idleCount==2,"same mask applied to idle; timing and mask counts stay separate");
        c.events.resize(3);Require(Group(aoe::Analyze(c,&idle)).byteStability.empty(),"fewer than four samples has no fitted mask");
    });
    check("nine tightly spaced 78-byte events are never periodic",[&]{
        auto c=Periodic(9);for(size_t i=0;i<c.events.size();++i)c.events[i].ms=1000+double(i)*5;Mark(c,1000);
        auto a=aoe::Analyze(c,&idle);auto& g=Group(a);Require(g.nearNine&&!g.periodicNear1s&&!g.strongTimingCandidate&&!g.hypothesis.consistentOnePlusEight,"nine-count burst rejected");
    });
    check("v2 export retains v1 fields and adds research evidence",[&]{
        auto c=Periodic(9,1000);Mark(c,1000);Mark(c,12000,2);auto a=aoe::Analyze(c,&idle);std::string error;
        auto path=artifacts/"timing_v2_capture.json";Require(aoe::ExportJson(path,c,a,error),"v2 JSON export");
        std::ifstream f(path);std::string json((std::istreambuf_iterator<char>(f)),{});
        for(auto field:{"requestedSeconds","durationMs","stopReason","markers","msFromNearestCastMarker","callerFingerprint","byteStability","standardDeviationMs","near100Fraction","near180Fraction","near250Fraction","expectedNextTimestampMs","likelyTruncated","onePlusEightHypothesis","provenDamageBehavior\":false","size78Diagnostic"})Require(json.find(field)!=std::string::npos,"v2 field missing");
    });
    return passed;
}

#include "analysis.hpp"
#include "producer.hpp"
#include "record_init.hpp"
#include "budget_write.hpp"
#include "tick_patch.hpp"
#include "caller_trace.hpp"
#include <algorithm>
#include <cmath>
#include <fstream>
#include <iomanip>
#include <limits>
#include <map>
#include <numeric>
#include <sstream>
#include <utility>

namespace aoe {
namespace {
constexpr double NearPeriodMs = 1000.0;
constexpr double PeriodToleranceMs = 180.0;

std::string CapturedBytes(const Event& event) {
    return BytesHex(event.bytes, event.bytes.size());
}

std::string ValidityKey(const Event& event) {
    return std::string(event.bufferReadable ? "readable" : "unreadable") +
        ":" + (event.fullPacket ? "full" : "prefix") +
        ":length=" + std::to_string(event.packetLength) +
        ":captured=" + std::to_string(event.bytes.size());
}

std::string CallerKey(const Event& event) {
    if (event.returnReadable && event.returnInModule)
        return "TClient+" + Hex(event.returnRva);
    if (event.returnReadable) return "external=" + Hex(event.returnAddress);
    return "unreadable_return";
}

std::string ExactKey(const Event& event) {
    // Hashes are display aids. Equality uses every captured byte and its validity.
    return ValidityKey(event) + ":bytes=" + CapturedBytes(event);
}

std::string BucketKey(const Event& event) {
    return CallerKey(event) + ":" + ValidityKey(event);
}

bool ValidPacketEvidence(const Event& event) {
    return event.bufferReadable && !event.bytes.empty() &&
        event.bytes.size() <= event.packetLength && event.bytes.size() <= 128 &&
        (!event.fullPacket || event.bytes.size() == event.packetLength);
}

bool SameCaller(const Event& a, const Event& b) {
    return CallerKey(a) == CallerKey(b);
}

bool MatchesPattern(const Event& event, const Event& exemplar, const Group& group) {
    if (!ValidPacketEvidence(event) || !SameCaller(event, exemplar) ||
        ValidityKey(event) != ValidityKey(exemplar)) return false;
    for (size_t i = 0; i < group.stablePositions.size(); ++i) {
        const auto position = group.stablePositions[i];
        if (position >= event.bytes.size() || event.bytes[position] != group.stableValues[i])
            return false;
    }
    return true;
}

std::string PatternText(const Event& exemplar, const Group& group) {
    std::ostringstream stream;
    stream << (exemplar.fullPacket ? "FULL " : "CAPTURED PREFIX ")
        << exemplar.bytes.size() << "/" << exemplar.packetLength << " bytes: ";
    size_t stable = 0;
    for (size_t i = 0; i < exemplar.bytes.size(); ++i) {
        if (i) stream << ' ';
        if (stable < group.stablePositions.size() && group.stablePositions[stable] == i) {
            stream << std::hex << std::uppercase << std::setfill('0') << std::setw(2)
                << unsigned(group.stableValues[stable++]);
        } else stream << "??";
    }
    return stream.str();
}

double SafeRate(uint64_t count, double durationMs) {
    return std::isfinite(durationMs) && durationMs > 0 ? double(count) * 1000.0 / durationMs : 0.0;
}

std::string Number(double value, int precision = 3) {
    if (!std::isfinite(value)) return "null";
    std::ostringstream stream;
    stream.imbue(std::locale::classic());
    stream << std::fixed << std::setprecision(precision) << value;
    return stream.str();
}

void ComputeStatistics(Group& group, const Capture& capture) {
    std::stable_sort(group.eventIndices.begin(), group.eventIndices.end(), [&](uint32_t a, uint32_t b) {
        return capture.events[a].ms < capture.events[b].ms;
    });
    group.count = group.eventIndices.size();
    for (const auto index : group.eventIndices) group.timestampsMs.push_back(capture.events[index].ms);
    for (size_t i = 1; i < group.timestampsMs.size(); ++i)
        group.deltasMs.push_back(group.timestampsMs[i] - group.timestampsMs[i - 1]);
    if (!group.deltasMs.empty()) {
        group.meanDeltaMs = std::accumulate(group.deltasMs.begin(), group.deltasMs.end(), 0.0) / group.deltasMs.size();
        auto sorted = group.deltasMs;
        std::sort(sorted.begin(), sorted.end());
        group.minDeltaMs = sorted.front();
        group.maxDeltaMs = sorted.back();
        const auto middle = sorted.size() / 2;
        group.medianDeltaMs = sorted.size() % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
        double variance = 0;
        size_t nearCount = 0;
        for (const auto delta : group.deltasMs) {
            variance += (delta - group.meanDeltaMs) * (delta - group.meanDeltaMs);
            if (std::abs(delta - NearPeriodMs) <= PeriodToleranceMs) ++nearCount;
        }
        if (group.meanDeltaMs > 0)
            group.coefficientVariation = std::sqrt(variance / group.deltasMs.size()) / group.meanDeltaMs;
        group.near1sFraction = double(nearCount) / group.deltasMs.size();
        group.standardDeviationMs=std::sqrt(variance/group.deltasMs.size());
        for(auto delta:group.deltasMs){if(std::abs(delta-1000)<=100)group.near100Fraction+=1; if(std::abs(delta-1000)<=250)group.near250Fraction+=1;}
        group.near100Fraction/=group.deltasMs.size();group.near250Fraction/=group.deltasMs.size();
        group.periodicNear1s = group.count >= 5 && group.near1sFraction >= 0.75 &&
            std::abs(group.meanDeltaMs - NearPeriodMs) <= PeriodToleranceMs && group.coefficientVariation <= 0.20;
    }
    group.nearNine = group.count >= 8 && group.count <= 10;
    group.aoeRate = SafeRate(group.count, capture.durationMs);
    if (!group.eventIndices.empty()) {
        const auto& first = capture.events[group.eventIndices.front()];
        const bool sameSize = std::all_of(group.eventIndices.begin(), group.eventIndices.end(), [&](uint32_t i) {
            return capture.events[i].packetLength == first.packetLength;
        });
        group.packetSize = sameSize ? first.packetLength : 0;
        group.callerKnown = first.returnReadable && first.returnInModule &&
            std::all_of(group.eventIndices.begin(), group.eventIndices.end(), [&](uint32_t i) {
                return capture.events[i].returnReadable && capture.events[i].returnInModule &&
                    capture.events[i].returnRva == first.returnRva;
            });
        if (group.callerKnown) group.callerRva = first.returnRva;
    }
}

void Score(Group& group, const Capture& capture, const Capture* idle, bool idleCompatible) {
    const bool packetGroup = group.kind == "exact_bytes" || group.kind == "caller_size_pattern";
    const bool validBytes = std::all_of(group.eventIndices.begin(), group.eventIndices.end(), [&](uint32_t i) {
        return ValidPacketEvidence(capture.events[i]);
    });
    const bool fullPackets = std::all_of(group.eventIndices.begin(), group.eventIndices.end(), [&](uint32_t i) {
        return capture.events[i].fullPacket;
    });
    const bool packetCaller = packetGroup && validBytes && group.callerKnown;
    double score = 0;
    if (group.periodicNear1s) {
        const double timing = 35 * group.near1sFraction + 10 * std::max(0.0, 1 - group.coefficientVariation / 0.25);
        score += timing;
        group.reasons.push_back("Timing +" + Number(timing, 1) + ": >=5 observations; >=75% intervals within 1000 +/- 180 ms; mean in band; population CV <=0.20.");
    } else {
        group.reasons.push_back("Timing +0: insufficient observations or spacing fails the near-1-second rule; a nine-event burst/network-slot loop is not periodic evidence.");
    }
    if (group.nearNine) {
        score += 8;
        group.reasons.push_back("Count +8: 8-10 events (approximately nine); count alone is insufficient.");
    } else group.reasons.push_back("Count +0: outside 8-10 events.");
    if (packetCaller) {
        score += 20;
        group.reasons.push_back("Packet/caller +20: matching captured-byte evidence and one readable TClient immediate return RVA. This identifies a shared call path, not the originating gameplay operation.");
    } else group.reasons.push_back("Packet/caller +0: this group lacks joint valid packet-byte and same-TClient-caller evidence.");
    if (idleCompatible && idle) {
        group.idleRate = SafeRate(group.idleCount, idle->durationMs);
        double contrast = group.idleRate == 0 ? (group.aoeRate > 0 ? 20.0 : 0.0) :
            20 * std::clamp((group.aoeRate / group.idleRate - 1) / 3, -1.0, 1.0);
        score += contrast;
        group.reasons.push_back("Duration-normalized IDLE contrast " + Number(contrast, 1) +
            ": capture=" + Number(group.aoeRate) + "/s, IDLE=" + Number(group.idleRate) +
            "/s; same membership rule/mask applied to both captures.");
        if (group.idleRate > 0 && group.aoeRate <= 1.5 * group.idleRate) {
            score = std::min(score, 35.0);
            group.reasons.push_back("Score capped at 35: frequency is <=1.5x normalized IDLE rate; consistent background traffic is not AOE-specific evidence.");
        }
        if (!idle->complete) group.reasons.push_back("IDLE ended early; its shorter observation window weakens the comparison even though rates are normalized.");
        if (idle->droppedEvents) group.reasons.push_back("IDLE has dropped events; contrast may be overstated.");
    } else {
        group.reasons.push_back("No compatible IDLE baseline: absence or frequency increase is unknown; score is provisional and capped at 70.");
        score = std::min(score, 70.0);
    }
    if (!packetCaller || !group.periodicNear1s) {
        score = std::min(score, 20.0);
        group.reasons.push_back("Legacy byte-identity score capped at 20; the separate PERIODIC TIMING CANDIDATE classification does not require byte identity.");
    }
    if (!validBytes) {
        score = 0;
        group.reasons.push_back("Score set to 0: invalid/unreadable/empty buffers cannot establish packet evidence; raw hits remain available.");
    }
    if (validBytes && !fullPackets) {
        score = std::min(score, 60.0);
        group.reasons.push_back("Score capped at 60: only captured prefixes are equal; uncaptured payload bytes may differ.");
    }
    if (group.kind == "caller_size_pattern") {
        group.reasons.push_back("Exploratory/inferred mask was fitted to this capture; common non-padding bytes do not establish an opcode or protocol field. Confirm it in independent AOE captures.");
    }
    if (!capture.complete) group.reasons.push_back("Capture ended early; the expected complete-cast count cannot be evaluated confidently.");
    if (capture.droppedEvents) {
        score = std::min(score, 40.0);
        group.reasons.push_back("Score capped at 40: dropped events make counts and interval statistics incomplete.");
    }
    if (capture.label != Label::Aoe) {
        score = 0;
        group.reasons.push_back("This is an IDLE-labelled capture; AOE candidate ranking is disabled.");
    }
    group.score = std::clamp(score, 0.0, 100.0);
}

std::string JsonString(const std::string& value) {
    std::ostringstream stream;
    stream << '"';
    for (const unsigned char ch : value) {
        switch (ch) {
        case '"': stream << "\\\""; break;
        case '\\': stream << "\\\\"; break;
        case '\b': stream << "\\b"; break;
        case '\f': stream << "\\f"; break;
        case '\n': stream << "\\n"; break;
        case '\r': stream << "\\r"; break;
        case '\t': stream << "\\t"; break;
        default:
            if (ch < 0x20) stream << "\\u" << std::hex << std::setfill('0') << std::setw(4) << unsigned(ch) << std::dec;
            else stream << char(ch);
        }
    }
    stream << '"';
    return stream.str();
}

const char* Boolean(bool value) { return value ? "true" : "false"; }

template<class T> void NumericArray(std::ostream& output, const std::vector<T>& values) {
    output << '[';
    for (size_t i = 0; i < values.size(); ++i) {
        if (i) output << ',';
        output << +values[i];
    }
    output << ']';
}

void DoubleArray(std::ostream& output, const std::vector<double>& values) {
    output << '[';
    for (size_t i = 0; i < values.size(); ++i) {
        if (i) output << ',';
        output << Number(values[i], 6);
    }
    output << ']';
}

void StringArray(std::ostream& output, const std::vector<std::string>& values) {
    output << '[';
    for (size_t i = 0; i < values.size(); ++i) {
        if (i) output << ',';
        output << JsonString(values[i]);
    }
    output << ']';
}
} // namespace

bool Compatible(const Capture& a, const Capture& b, std::string& reason) {
    if (a.synthetic != b.synthetic) reason = "Synthetic and live captures cannot be compared.";
    else if (a.target.pid == 0 || b.target.pid == 0 || a.target.creationTime == 0 || b.target.creationTime == 0)
        reason = "Missing process-session identity.";
    else if (a.target.pid != b.target.pid || a.target.creationTime != b.target.creationTime)
        reason = "Different process session (PID or creation time).";
    else if (!a.target.base || a.target.base != b.target.base || !a.target.size || a.target.size != b.target.size)
        reason = "Different or missing live module base/size.";
    else if (a.target.image.sha256.empty() || b.target.image.sha256.empty() || a.target.image.sha256 != b.target.image.sha256)
        reason = "Different or missing TClient SHA-256 identity.";
    else if (a.target.mode != b.target.mode || a.target.traceRva != b.target.traceRva)
        reason = "Different traced RVA.";
    else if (a.frequency <= 0 || b.frequency <= 0 || a.frequency != b.frequency)
        reason = "Missing or incompatible QPC frequency.";
    else if (!(a.durationMs > 0) || !(b.durationMs > 0) || !std::isfinite(a.durationMs) || !std::isfinite(b.durationMs))
        reason = "Capture durations must be positive finite values for normalized rates.";
    else { reason = "Same process session, module identity, traced RVA, and QPC frequency."; return true; }
    return false;
}

Analysis Analyze(const Capture& capture, const Capture* idle) {
    Analysis result;
    std::string compatibility;
    result.idleCompatible = idle && idle->label == Label::Idle && Compatible(capture, *idle, compatibility);
    if (!idle) result.idleReference = "No IDLE capture supplied; candidate scores are provisional.";
    else if (idle->label != Label::Idle) result.idleReference = "Rejected baseline: capture is not labelled IDLE.";
    else result.idleReference = (result.idleCompatible ? "IDLE capture " : "Rejected IDLE capture ") +
        std::to_string(idle->id) + ": " + compatibility;

    if(capture.target.mode==TraceMode::Producer){
        std::map<std::string,Group> callers;
        for(size_t i=0;i<capture.events.size();++i){auto key=CallerKey(capture.events[i]);auto& g=callers[key];g.kind="producer_caller";g.key=key;g.eventIndices.push_back(uint32_t(i));}
        for(auto& [key,g]:callers){ComputeStatistics(g,capture);g.category=g.periodicNear1s?"PERIODIC PRODUCER OBSERVATION":"PRODUCER CALLER";g.status=(g.callerKnown&&g.callerRva==ProducerCallerRva?"Expected caller":"Other caller retained");if(g.periodicNear1s)g.status+=" | Periodic ~1 s";if(g.count==9)g.status+=" | Nine calls";if(result.idleCompatible){for(auto& e:idle->events)if(CallerKey(e)==key)++g.idleCount;g.idleRate=SafeRate(g.idleCount,idle->durationMs);}if(!g.timestampsMs.empty()){g.firstMs=g.timestampsMs.front();g.lastMs=g.timestampsMs.back();g.expectedNextTimestampMs=g.lastMs+g.medianDeltaMs;g.expectedPreviousTimestampMs=g.firstMs-g.medianDeltaMs;}g.notes=ProducerLimitation;result.groups.push_back(std::move(g));}
        result.producerFields=CompareProducerFields(capture);
        result.summary=std::string(capture.synthetic?"SYNTHETIC. ":"")+std::to_string(capture.events.size())+" producer hits; "+std::to_string(result.groups.size())+" caller groups. "+ProducerLimitation;
        if(!capture.complete)result.summary+=" Capture ended early.";
        if(capture.droppedEvents)result.summary+=" Dropped events: "+std::to_string(capture.droppedEvents)+".";
        return result;
    }
    if(capture.target.mode==TraceMode::RecordInit){
        result.summary=RecordInitSummary(capture);
        if(!capture.complete)result.summary+=" Capture ended early.";
        if(capture.droppedEvents)result.summary+=" Dropped events: "+std::to_string(capture.droppedEvents)+".";
        return result;
    }
    if(capture.target.mode==TraceMode::Caller020A){result.caller020A=BuildCaller020ASummary(capture);result.summary=Caller020ASummaryText(result.caller020A,capture.target.base);if(!capture.complete)result.summary+=" Capture ended early.";if(capture.droppedEvents)result.summary+=" Dropped events: "+std::to_string(capture.droppedEvents)+".";return result;}
    if(capture.target.mode==TraceMode::BudgetWrite){result.summary=BudgetExperimentDetails(capture.experiment);return result;}
    if(capture.target.mode==TraceMode::Tick100){result.summary=TickExperimentDetails(capture.tickExperiment);return result;}
    std::map<std::pair<std::string, std::string>, Group> groups;
    std::map<std::string, std::vector<uint32_t>> patternBuckets;
    std::map<std::pair<std::string, std::string>, uint64_t> idleCounts;
    std::map<std::string, std::vector<const Event*>> idlePatternBuckets;
    if (result.idleCompatible) {
        for (const auto& event : idle->events) {
            ++idleCounts[{"size", std::to_string(event.packetLength)}];
            ++idleCounts[{"size_fingerprint",std::to_string(event.packetLength)+":"+FingerprintText(event)}];
            ++idleCounts[{"caller", CallerKey(event)}];
            ++idleCounts[{"exact_bytes", ExactKey(event)}];
            if (ValidPacketEvidence(event) && event.returnReadable && event.returnInModule)
                idlePatternBuckets[BucketKey(event)].push_back(&event);
        }
    }
    for (size_t index = 0; index < capture.events.size() && index <= UINT32_MAX; ++index) {
        const auto& event = capture.events[index];
        for (const auto& entry : std::vector<std::pair<std::string, std::string>>{
            {"size", std::to_string(event.packetLength)}, {"size_fingerprint",std::to_string(event.packetLength)+":"+FingerprintText(event)}, {"exact_bytes", ExactKey(event)}, {"caller", CallerKey(event)}}) {
            auto& group = groups[entry];
            group.kind = entry.first;
            group.key = entry.second;
            group.eventIndices.push_back(static_cast<uint32_t>(index));
        }
        if (ValidPacketEvidence(event) && event.returnReadable && event.returnInModule)
            patternBuckets[BucketKey(event)].push_back(static_cast<uint32_t>(index));
    }
    for (const auto& [bucket, indices] : patternBuckets) {
        if (indices.size() < 3) continue;
        Group group;
        group.kind = "caller_size_pattern";
        group.eventIndices = indices;
        const auto& exemplar = capture.events[indices.front()];
        size_t nonPaddingPositions = 0;
        for (size_t position = 0; position < exemplar.bytes.size(); ++position) {
            const auto value = exemplar.bytes[position];
            if (std::all_of(indices.begin(), indices.end(), [&](uint32_t index) {
                return capture.events[index].bytes[position] == value;
            })) {
                group.stablePositions.push_back(static_cast<uint32_t>(position));
                group.stableValues.push_back(value);
                if (value != 0 && value != 0xff) ++nonPaddingPositions;
            }
        }
        if (nonPaddingPositions < 2) continue;
        group.pattern = PatternText(exemplar, group);
        group.key = bucket + ":mask=" + group.pattern;
        group.notes = "Exploratory/inferred from >=3 same-caller, same-size observations; at least two common non-padding bytes. Fitted mask may merge unrelated operations or miss traffic with changing fields.";
        groups[{group.kind, group.key}] = std::move(group);
    }
    for (auto& [unused, group] : groups) {
        ComputeStatistics(group, capture);
        const auto& exemplar = capture.events[group.eventIndices.front()];
        if (group.kind == "exact_bytes") {
            group.signature = exemplar.signature;
            group.pattern = (exemplar.fullPacket ? "FULL " : "CAPTURED PREFIX ") + CapturedBytes(exemplar);
            group.notes = exemplar.bufferReadable ?
                "Equality compares declared packet length, captured length, read/full status, and every captured byte; hash is display only." :
                "Unreadable-buffer hits grouped by read/full status and length; this is not a packet signature match.";
        }
        if (group.kind == "caller") group.notes = "Immediate [RSP] return address; all high-level send operations may share this caller. Stack candidates are not unwound call stacks.";
        if (group.kind == "size") group.notes = "Length alone can combine unrelated packets; zero packetSize in a mixed caller group means multiple lengths.";
        if (result.idleCompatible) {
            if (group.kind == "caller_size_pattern") {
                const auto bucket = idlePatternBuckets.find(BucketKey(exemplar));
                if (bucket != idlePatternBuckets.end()) {
                    for (const auto* event : bucket->second)
                        if (MatchesPattern(*event, exemplar, group)) ++group.idleCount;
                }
            } else {
                const auto found = idleCounts.find({group.kind, group.key});
                if (found != idleCounts.end()) group.idleCount = found->second;
            }
        }
        Score(group, capture, idle, result.idleCompatible);
        result.groups.push_back(std::move(group));
    }
    std::stable_sort(result.groups.begin(), result.groups.end(), [](const Group& a, const Group& b) {
        if (a.score != b.score) return a.score > b.score;
        if (a.periodicNear1s != b.periodicNear1s) return a.periodicNear1s > b.periodicNear1s;
        if (a.kind != b.kind) return a.kind < b.kind;
        return a.key < b.key;
    });
    size_t candidates = 0;
    for (const auto& group : result.groups) if (group.score > 35 && group.periodicNear1s) ++candidates;
    result.summary = std::to_string(capture.events.size()) + " raw internal queue-wrapper hits; " +
        std::to_string(result.groups.size()) + " overlapping groups; " + std::to_string(candidates) +
        " groups warrant timing/packet follow-up. Scores are heuristics, not confidence probabilities or damage-event proof. " + result.idleReference;
    if (capture.synthetic) result.summary = "SYNTHETIC DEMONSTRATION ONLY. " + result.summary;
    if (!capture.complete) result.summary += " Capture ended early.";
    if (capture.droppedEvents) result.summary += " Dropped events: " + std::to_string(capture.droppedEvents) + ".";
    AddTimingResearch(capture,idle,result);
    return result;
}

bool ExportJson(const std::filesystem::path& path, const Capture& capture, const Analysis& analysis, std::string& error) {
    try {
        if (!path.parent_path().empty()) std::filesystem::create_directories(path.parent_path());
        std::ofstream output(path, std::ios::binary | std::ios::trunc);
        if (!output) { error = "Cannot open JSON destination: " + path.string(); return false; }
        output.imbue(std::locale::classic());
        output << "{\n\"schema\":\"4UnityAOETracer.capture.v2\",\n\"compatibility\":\"v1 fields retained; additive timing, fingerprint, byte stability and marker fields\",\n"
            << "\"addressEncoding\":\"hex strings preserve all 64 bits; QPC tick values are decimal strings\",\n"
            << "\"limitations\":\"An A129F0 hit is an internal outgoing-queue-wrapper execution, not proof of wire transmission, an AOE operation, or damage. Stack candidates are a raw scan, not an unwind. A +99240 execution identifies the typed message producer but does not by itself prove damage, target selection, or AOE mechanics. ECX at +57E35 is the serialized uint32 written to record+34h. Runtime correlation is required before assigning gameplay units or duration semantics. This experiment establishes only the causal effect of modifying the finite record+34 budget for the matched 0x020A/0x0246 runtime record. Gameplay semantics and physical units must be inferred from observed behavior, not from the numeric value alone. Changing the client-side scheduler interval can increase the frequency of the periodic operation generated by the client. It does not by itself prove that a remote server will accept every generated operation as gameplay damage. A +7BB160 caller trace identifies runtime callers for operationWord0 0x020A; call frequency alone does not establish scheduler ownership or damage mechanics.\",\n"
            << "\"capture\":{\"id\":" << capture.id << ",\"label\":" << JsonString(LabelName(capture.label))
            << ",\"utc\":" << JsonString(capture.utc) << ",\"synthetic\":" << Boolean(capture.synthetic)
            << ",\"complete\":" << Boolean(capture.complete) << ",\"stopReason\":" << JsonString(capture.stopReason)
            << ",\"qpcFrequency\":" << JsonString(std::to_string(capture.frequency))
            << ",\"startQpc\":" << JsonString(std::to_string(capture.startQpc))
            << ",\"endQpc\":" << JsonString(std::to_string(capture.endQpc))
            << ",\"requestedSeconds\":" << Number(capture.requestedSeconds, 6)
            << ",\"durationMs\":" << Number(capture.durationMs, 6)
            << ",\"droppedEvents\":" << capture.droppedEvents << "},\n\"markers\":[";
        for(size_t i=0;i<capture.markers.size();++i){if(i)output<<',';const auto& m=capture.markers[i];output<<"{\"id\":"<<m.id<<",\"label\":"<<JsonString(m.label)<<",\"qpc\":"<<JsonString(std::to_string(m.qpc))<<",\"ms\":"<<Number(m.ms,6)<<",\"source\":\"manual operator button; no game input\"}";}
        output<<"],\n";
        output<<"\"traceMode\":"<<JsonString(ModeName(capture.target.mode))<<",\n\"producerFieldStability\":";WriteProducerFieldsJson(output,analysis.producerFields);output<<",\n\"recordInitSummary\":";WriteRecordInitSummaryJson(output,capture);output<<",\n\"operation020ACallerSummary\":";WriteCaller020ASummaryJson(output,analysis.caller020A);output<<",\n\"experiment\":";if(capture.target.mode==TraceMode::Tick100)WriteTickExperimentJson(output,capture.tickExperiment);else WriteBudgetExperimentJson(output,capture);output<<",\n";
        const auto& target = capture.target;
        output << "\"target\":{\"path\":" << JsonString(Utf8(target.image.path))
            << ",\"pid\":" << target.pid << ",\"creationTime\":" << JsonString(std::to_string(target.creationTime))
            << ",\"moduleBase\":" << JsonString(Hex(target.base)) << ",\"moduleSize\":" << target.size
            << ",\"traceRva\":" << JsonString(Hex(target.traceRva)) << ",\"traceAddress\":" << JsonString(Hex(target.traceAddress))
            << ",\"verified\":" << Boolean(target.verified) << ",\"sha256\":" << JsonString(target.image.sha256)
            << ",\"peTimestamp\":" << target.image.timestamp << ",\"peImageSize\":" << target.image.imageSize
            << ",\"fileSize\":" << target.image.fileSize << ",\"amd64\":" << Boolean(target.image.amd64)
            << ",\"traceExecutable\":" << Boolean(target.image.traceExecutable)
            << ",\"functionEntry\":" << Boolean(target.image.functionEntry)
            << ",\"entryBytes\":" << JsonString(BytesHex(target.image.entryBytes, target.image.entryBytes.size()))
            << ",\"executableRanges\":[";
        for (size_t i = 0; i < target.image.executableRanges.size(); ++i) {
            if (i) output << ',';
            output << "{\"rva\":" << target.image.executableRanges[i].rva << ",\"size\":" << target.image.executableRanges[i].size << '}';
        }
        output << "]},\n\"events\":[\n";
        for (size_t i = 0; i < capture.events.size(); ++i) {
            if (i) output << ",\n";
            const auto& event = capture.events[i];
            const auto marker=RelativeToMarkers(capture,event.ms);
            output << "{\"rip\":"<<JsonString(Hex(event.rip))<<",\"producer\":";WriteProducerJson(output,event);output<<",\"recordInit\":";WriteRecordInitJson(output,capture,event);output<<",\"caller020A\":";WriteCaller020AEventJson(output,event);
            output << ",\"sequence\":" << event.sequence << ",\"qpc\":" << JsonString(std::to_string(event.qpc))
                << ",\"ms\":" << Number(event.ms, 6) << ",\"deltaMs\":" << Number(event.deltaMs, 6)
                << ",\"timestamp\":{\"qpc\":"<<JsonString(std::to_string(event.qpc))<<",\"ms\":"<<Number(event.ms,6)<<'}'
                << ",\"handlerMs\":" << Number(event.handlerMs, 6) << ",\"threadId\":" << event.threadId
                << ",\"rcx\":" << JsonString(Hex(event.rcx)) << ",\"rdx\":" << JsonString(Hex(event.rdx))
                << ",\"r8\":" << JsonString(Hex(event.r8)) << ",\"r8dPacketLength\":" << event.packetLength
                << ",\"r9\":" << JsonString(Hex(event.r9)) << ",\"rbx\":" << JsonString(Hex(event.rbx)) << ",\"rbp\":" << JsonString(Hex(event.rbp)) << ",\"rsp\":" << JsonString(Hex(event.rsp))
                << ",\"returnAddress\":" << JsonString(Hex(event.returnAddress))
                << ",\"returnRva\":" << (event.returnInModule ? JsonString(Hex(event.returnRva)) : "null")
                << ",\"returnInModule\":" << Boolean(event.returnInModule)
                << ",\"returnReadable\":" << Boolean(event.returnReadable)
                << ",\"returnReadError\":" << event.returnReadError
                << ",\"operationWord0\":" << (event.caller020A.operationWord0.readable?std::to_string(event.caller020A.operationWord0.value):"null")
                << ",\"operation0209\":" << Boolean(event.caller020A.operation0209)
                << ",\"operation020A\":" << Boolean(event.caller020A.operation020A)
                << ",\"known853B10Path\":" << Boolean(event.caller020A.known853B10Path)
                << ",\"bufferReadable\":" << Boolean(event.bufferReadable)
                << ",\"bufferReadError\":" << event.bufferReadError
                << ",\"fullPacket\":" << Boolean(event.fullPacket)
                << ",\"capturedLength\":" << event.bytes.size()
                << ",\"captureScope\":" << JsonString(event.producer.captured ? "producer_entry" : !event.bufferReadable ? "invalid_buffer" : event.fullPacket ? "full_packet" : "captured_prefix")
                << ",\"bytesHex\":" << JsonString(CapturedBytes(event)) << ",\"bytes\":";
            NumericArray(output, event.bytes);
            output << ",\"capturedBytesSha256\":" << JsonString(event.signature)
                << ",\"callerFingerprint\":"<<JsonString(FingerprintText(event))<<",\"callerFingerprintRvas\":";
            NumericArray(output,StackFingerprint(event));
            output<<",\"firstCastMarkerId\":"<<(marker.available?std::to_string(marker.firstId):"null")
                <<",\"msFromFirstCastMarker\":"<<(marker.available?Number(marker.firstRelativeMs,6):"null")
                <<",\"nearestCastMarkerId\":"<<(marker.available?std::to_string(marker.nearestId):"null")
                <<",\"msFromNearestCastMarker\":"<<(marker.available?Number(marker.nearestRelativeMs,6):"null")
                << ",\"stackCandidatesAreUnwound\":false,\"stackCandidates\":[";
            for (size_t j = 0; j < event.stackCandidates.size(); ++j) {
                if (j) output << ',';
                const auto& candidate = event.stackCandidates[j];
                output << "{\"stackOffset\":" << candidate.stackOffset << ",\"address\":" << JsonString(Hex(candidate.address))
                    << ",\"rva\":" << JsonString(Hex(candidate.rva))
                    << ",\"directCallPrecedes\":" << Boolean(candidate.directCallPrecedes) << '}';
            }
            output << "]}";
        }
        output << "\n],\n\"analysis\":{\"summary\":" << JsonString(analysis.summary)
            << ",\"idleCompatible\":" << Boolean(analysis.idleCompatible)
            << ",\"idleReference\":" << JsonString(analysis.idleReference)
            << ",\"scoreIsProbability\":false,\"timingRule\":{\"periodMs\":1000,\"toleranceMs\":180,\"minimumCount\":5,\"minimumNearFraction\":0.75,\"maximumPopulationCV\":0.20}"
            << ",\"timingCandidateRule\":{\"byteEqualityRequired\":false,\"nearNineRequired\":false,\"minimumIdleRateRatio\":2,\"zeroIdleCountQualifies\":true,\"compatibleIdleRequired\":true,\"truncationBeyondBoundaryMedianMultiples\":1.5}"
            << ",\"size78Diagnostic\":{\"summary\":"<<JsonString(analysis.size78.summary)<<",\"idleCount\":"<<(analysis.idleCompatible?std::to_string(analysis.size78.idleCount):"null")
            <<",\"captureRatePerSecond\":"<<Number(analysis.size78.captureRate,6)<<",\"idleRatePerSecond\":"<<(analysis.idleCompatible?Number(analysis.size78.idleRate,6):"null")<<",\"events\":[";
        for(size_t j=0;j<analysis.size78.eventIndices.size();++j){if(j)output<<',';auto index=analysis.size78.eventIndices[j];const auto& e=capture.events[index];output<<"{\"eventIndex\":"<<index<<",\"ms\":"<<Number(e.ms,6)<<",\"deltaFromPrior78Ms\":"<<(j?Number(e.ms-capture.events[analysis.size78.eventIndices[j-1]].ms,6):"null")<<",\"fingerprint\":"<<JsonString(FingerprintText(e))<<",\"signature\":"<<JsonString(e.signature)<<",\"first16Bytes\":"<<JsonString(BytesHex(e.bytes,16))<<'}';}
        output<<"]},\"groups\":[\n";
        for (size_t i = 0; i < analysis.groups.size(); ++i) {
            if (i) output << ",\n";
            const auto& group = analysis.groups[i];
            output << "{\"kind\":" << JsonString(group.kind) << ",\"key\":" << JsonString(group.key)
                << ",\"signature\":" << JsonString(group.signature) << ",\"pattern\":" << JsonString(group.pattern)
                << ",\"notes\":" << JsonString(group.notes) << ",\"packetSize\":" << group.packetSize
                << ",\"callerRva\":" << (group.callerKnown ? JsonString(Hex(group.callerRva)) : "null")
                << ",\"callerKnown\":" << Boolean(group.callerKnown) << ",\"count\":" << group.count
                << ",\"idleCount\":" << (analysis.idleCompatible ? std::to_string(group.idleCount) : "null")
                << ",\"meanDeltaMs\":" << Number(group.meanDeltaMs, 6)
                << ",\"medianDeltaMs\":" << Number(group.medianDeltaMs, 6)
                << ",\"minDeltaMs\":" << Number(group.minDeltaMs, 6)
                << ",\"maxDeltaMs\":" << Number(group.maxDeltaMs, 6)
                << ",\"populationCoefficientVariation\":" << Number(group.coefficientVariation, 6)
                << ",\"near1sFraction\":" << Number(group.near1sFraction, 6)
                << ",\"periodicNear1s\":" << Boolean(group.periodicNear1s)
                << ",\"nearNine\":" << Boolean(group.nearNine)
                << ",\"captureRatePerSecond\":" << Number(group.aoeRate, 6)
                << ",\"idleRatePerSecond\":" << (analysis.idleCompatible ? Number(group.idleRate, 6) : "null")
                << ",\"rateRatioToIdle\":" << (analysis.idleCompatible && group.idleRate > 0 ? Number(group.aoeRate / group.idleRate, 6) : "null")
                << ",\"absentFromIdle\":" << (analysis.idleCompatible ? Boolean(group.idleCount == 0) : "null")
                << ",\"score\":" << Number(group.score, 3) << ",\"timestampsMs\":";
            DoubleArray(output, group.timestampsMs);
            output << ",\"deltasMs\":"; DoubleArray(output, group.deltasMs);
            output << ",\"eventIndices\":"; NumericArray(output, group.eventIndices);
            output<<",\"markerRelativeTimes\":[";for(size_t j=0;j<group.eventIndices.size();++j){if(j)output<<',';auto m=RelativeToMarkers(capture,capture.events[group.eventIndices[j]].ms);output<<"{\"firstMarkerId\":"<<(m.available?std::to_string(m.firstId):"null")<<",\"firstRelativeMs\":"<<(m.available?Number(m.firstRelativeMs,6):"null")<<",\"nearestMarkerId\":"<<(m.available?std::to_string(m.nearestId):"null")<<",\"nearestRelativeMs\":"<<(m.available?Number(m.nearestRelativeMs,6):"null")<<'}';}output<<']';

            output << ",\"stablePositions\":"; NumericArray(output, group.stablePositions);
            output << ",\"stableValues\":"; NumericArray(output, group.stableValues);
            output<<",\"category\":"<<JsonString(group.category)<<",\"status\":"<<JsonString(group.status)
                <<",\"strongPeriodicTimingCandidate\":"<<Boolean(group.strongTimingCandidate)
                <<",\"callerFingerprint\":"<<JsonString(group.fingerprint)<<",\"callerFingerprintRvas\":";
            NumericArray(output,group.fingerprintRvas);
            output<<",\"standardDeviationMs\":"<<Number(group.standardDeviationMs,6)
                <<",\"near100Fraction\":"<<Number(group.near100Fraction,6)<<",\"near180Fraction\":"<<Number(group.near1sFraction,6)<<",\"near250Fraction\":"<<Number(group.near250Fraction,6)
                <<",\"firstMs\":"<<Number(group.firstMs,6)<<",\"lastMs\":"<<Number(group.lastMs,6)
                <<",\"expectedNextTimestampMs\":"<<(group.periodicNear1s?Number(group.expectedNextTimestampMs,6):"null")
                <<",\"expectedPreviousTimestampMs\":"<<(group.periodicNear1s?Number(group.expectedPreviousTimestampMs,6):"null")
                <<",\"likelyTruncated\":"<<Boolean(group.likelyTruncated)<<",\"possibleEarlierEvents\":"<<Boolean(group.possibleEarlierEvents)
                <<",\"truncationAssessment\":"<<JsonString(group.truncationAssessment)
                <<",\"aoeOnly\":"<<(analysis.idleCompatible?Boolean(group.aoeOnly):"null")<<",\"background\":"<<(analysis.idleCompatible?Boolean(group.background):"null")
                <<",\"maskSamples\":"<<group.maskSamples<<",\"maskAvailable\":"<<Boolean(group.maskAvailable)
                <<",\"maskedSignature\":"<<JsonString(group.maskedSignature)<<",\"maskedIdleCount\":"<<(analysis.idleCompatible&&group.maskAvailable?std::to_string(group.maskedIdleCount):"null")
                <<",\"byteStability\":[";
            for(size_t j=0;j<group.byteStability.size();++j){if(j)output<<',';auto& b=group.byteStability[j];output<<"{\"offset\":"<<b.offset<<",\"sampleCount\":"<<b.samples<<",\"distinctValues\":"<<b.distinctValues<<",\"constant\":"<<Boolean(b.constant)<<",\"value\":"<<(b.constant?std::to_string(b.value):"null")<<",\"entropyBits\":"<<Number(b.entropyBits,6)<<'}';}
            const auto& h=group.hypothesis;
            output<<"],\"onePlusEightHypothesis\":{\"periodicEventCount\":"<<h.periodicCount<<",\"possibleInitialEventIndex\":"<<(h.possibleInitialEventIndex>=0?std::to_string(h.possibleInitialEventIndex):"null")
                <<",\"initialCandidateCount\":"<<h.initialCandidateCount<<",\"totalPossibleSequenceCount\":"<<h.totalPossibleCount<<",\"castMarkerId\":"<<(h.markerId?std::to_string(h.markerId):"null")
                <<",\"sequenceSpanMs\":"<<Number(h.spanMs,6)<<",\"spanNear8Seconds\":"<<Boolean(h.spanNear8Seconds)<<",\"consistentWith1InitialPlus8Periodic\":"<<Boolean(h.consistentOnePlusEight)
                <<",\"provenDamageBehavior\":false,\"assessment\":"<<JsonString(h.assessment)<<'}';
            output << ",\"reasons\":"; StringArray(output, group.reasons);
            output << '}';
        }
        output << "\n]}\n}\n";
        output.flush();
        if (!output.good()) { error = "Write failed for JSON destination: " + path.string(); return false; }
        error.clear();
        return true;
    } catch (const std::exception& exception) {
        error = std::string("JSON export failed: ") + exception.what();
        return false;
    }
}

std::vector<Capture> SyntheticCaptures() {
    auto make = [](uint64_t id, Label label, double durationMs) {
        Capture capture;
        capture.id = id;
        capture.label = label;
        capture.utc = "2026-09-17T00:00:00Z";
        capture.stopReason = "Synthetic fixture completed; not live evidence";
        capture.frequency = 1000000;
        capture.startQpc = 1000000000 + static_cast<int64_t>(id * 30000000);
        capture.endQpc = capture.startQpc + static_cast<int64_t>(durationMs * 1000);
        capture.requestedSeconds = durationMs / 1000;
        capture.durationMs = durationMs;
        capture.complete = true;
        capture.synthetic = true;
        capture.target.pid = 4242;
        capture.target.creationTime = 123456789000;
        capture.target.base = 0x140000000;
        capture.target.size = 0x1800000;
        capture.target.traceAddress = capture.target.base + SendRva;
        capture.target.traceRva = SendRva;
        capture.target.verified = true;
        capture.target.image.path = TargetPath;
        capture.target.image.sha256 = ApprovedHash;
        capture.target.image.amd64 = capture.target.image.traceExecutable = capture.target.image.functionEntry = true;
        capture.target.image.imageSize = static_cast<uint32_t>(capture.target.size);
        return capture;
    };
    auto add = [](Capture& capture, double ms, uint32_t length, uint8_t header1, uint8_t header2, unsigned sequence) {
        Event event;
        event.ms = ms;
        event.qpc = capture.startQpc + static_cast<int64_t>(ms * capture.frequency / 1000.0);
        event.handlerMs = 0.080;
        event.threadId = 4243;
        event.rcx = 0x200000100;
        event.rdx = 0x200100000 + capture.events.size() * 0x100;
        event.r8 = length;
        event.r9 = 0x123456789abcdef0;
        event.rsp = 0x300010000;
        event.packetLength = length;
        event.returnRva = 0xA12B1E;
        event.returnAddress = capture.target.base + event.returnRva;
        event.returnInModule = event.returnReadable = event.bufferReadable = true;
        event.fullPacket = length <= 128;
        event.bytes.resize(std::min<uint32_t>(128, length), 0);
        if (event.bytes.size() >= 6) {
            event.bytes[0] = header1; event.bytes[1] = header2;
            for (unsigned i = 0; i < 4; ++i) event.bytes[2 + i] = static_cast<uint8_t>(sequence >> (8 * i));
        }
        event.signature = Sha256(event.bytes.data(), event.bytes.size());
        event.stackCandidates.push_back({0, event.returnAddress, event.returnRva, true});
        capture.events.push_back(std::move(event));
    };
    auto finish = [](Capture& capture) {
        std::stable_sort(capture.events.begin(), capture.events.end(), [](const Event& a, const Event& b) { return a.ms < b.ms; });
        for (size_t i = 0; i < capture.events.size(); ++i) {
            capture.events[i].sequence = i + 1;
            capture.events[i].deltaMs = i ? capture.events[i].ms - capture.events[i - 1].ms : 0;
        }
    };
    auto idle = make(1001, Label::Idle, 10000);
    auto aoe = make(1002, Label::Aoe, 10000);
    auto longIdle = make(1003, Label::Idle, 20000);
    for (unsigned i = 0; i < 10; ++i) {
        add(idle, 25 + 1000.0 * i, 16, 0x55, 0x6a, 100 + i);
        add(aoe, 25 + 1000.0 * i, 16, 0x55, 0x6a, 120 + i);
    }
    for (unsigned i = 0; i < 20; ++i) add(longIdle, 25 + 1000.0 * i, 16, 0x55, 0x6a, 140 + i);
    for (unsigned i = 0; i < 9; ++i) {
        add(aoe, 400 + 1000.0 * i, 24, 0x31, 0x47, 500 + i);
        add(aoe, 2000 + 5.0 * i, 32, 0x70, 0x42, 600 + i);
    }
    for (unsigned i = 0; i < 3; ++i) {
        add(aoe, 1500 + 1000.0 * i, 48, 0, 0, i);
        auto& event = aoe.events.back();
        event.rdx = 1;
        event.bufferReadable = event.fullPacket = false;
        event.bufferReadError = 487;
        event.bytes.clear();
        event.signature.clear();
    }
    // Equal 128-byte captures are deliberately not equal full packets/lengths.
    add(aoe, 1600, 160, 0x61, 0x72, 0x1234);
    add(aoe, 1700, 192, 0x61, 0x72, 0x1234);
    add(aoe, 1800, 128, 0x61, 0x72, 0x1234);
    finish(idle); finish(aoe); finish(longIdle);
    return {std::move(idle), std::move(aoe), std::move(longIdle)};
}
} // namespace aoe

#include "aoe_locator.hpp"
#include <algorithm>
#include <cstdint>
#include <cstring>
#include <exception>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace {
void Require(bool condition, const std::string& message) {
    if (!condition) throw std::runtime_error(message);
}

void RequireMatches(const std::vector<uint8_t>& bytes, const std::string& pattern,
                    const std::vector<size_t>& expected) {
    Require(aoe::MatchAoePattern(bytes, pattern) == expected, "Unexpected masked-signature matches");
}

void RequireInvalidPattern(const std::string& pattern) {
    bool rejected = false;
    try {
        (void)aoe::MatchAoePattern({0x90, 0xE8, 0, 0, 0, 0}, pattern);
    } catch (const std::invalid_argument&) {
        rejected = true;
    }
    Require(rejected, "Malformed or non-discriminating pattern was accepted: " + pattern);
}

std::vector<uint8_t> CallBytes(int32_t displacement) {
    const uint32_t raw = static_cast<uint32_t>(displacement);
    return {0xE8, static_cast<uint8_t>(raw), static_cast<uint8_t>(raw >> 8),
            static_cast<uint8_t>(raw >> 16), static_cast<uint8_t>(raw >> 24)};
}

void RequireCall(int32_t displacement, uint64_t instruction, uint64_t imageSize,
                 uint64_t expected) {
    uint64_t target = std::numeric_limits<uint64_t>::max();
    Require(aoe::DecodeAoeCall(CallBytes(displacement), instruction, imageSize, target),
            "Valid direct CALL was rejected");
    Require(target == expected, "Signed rel32 target was decoded incorrectly");
}

struct ProfileFixture {
    aoe::BuildProfile profile;
    aoe::AoeLocatorResult located;
};

ProfileFixture ReadyProfileFixture() {
    ProfileFixture fixture;
    fixture.profile.targetSha256 = fixture.located.image.sha256 = std::string(64, 'A');
    fixture.located.ready = true;
    // No file exists or is required: every profile test below must stop at a
    // pure guard before the on-disk fingerprint validation stage.
    fixture.located.image.path = L"AOE_FIXTURE_MUST_NOT_REACH_FILE_ACCESS";
    fixture.located.image.imageSize = 0x1000;
    auto& runtime = fixture.profile.runtime;
    runtime.initialNxAvailable = true;
    runtime.initialCallRva = 0x100;
    runtime.initialPrepRva = 0x100 - 29;
    runtime.initialReturnRva = 0x105;
    runtime.sharedWorkerRva = 0x200;
    runtime.producerRva = 0x300;
    runtime.producerCallRva = 0x250;
    runtime.typedActorLookupRva = 0x400;
    runtime.targetBuilderVectorRva = 0x500;
    runtime.differentialParsedRva = 0x550;
    runtime.idSerializationRva = 0x60E;
    runtime.typeSerializationRva = 0x618;
    runtime.initialCallBytes = CallBytes(0xFB);
    runtime.producerCallBytes = CallBytes(0xAB);
    for (auto* bytes : {&runtime.initialPrepBytes, &runtime.initialReturnBytes,
                       &runtime.sharedWorkerBytes, &runtime.producerBytes,
                       &runtime.typedActorLookupBytes, &runtime.targetBuilderVectorBytes,
                       &runtime.differentialParsedBytes, &runtime.idSerializationBytes,
                       &runtime.typeSerializationBytes}) *bytes = {0x90};
    for (const auto& item : std::vector<std::pair<std::string, uint64_t>>{
             {"AoeLifecycleCaller", 0x100}, {"SharedOperation020A", 0x200},
             {"OperationProducer", 0x300}, {"ProducerCall", 0x250},
             {"TypedActorLookup", 0x400}, {"TargetVectorBuilder", 0x500},
             {"ParsedActionBoundary", 0x550}, {"RecordSerialization", 0x600}}) {
        aoe::AoeSignatureResult signature;
        signature.name = item.first;
        signature.rva = item.second;
        signature.matches = {item.second};
        signature.valid = true;
        fixture.located.signatures.push_back(signature);
    }
    return fixture;
}

void RequireProfileGuard(const ProfileFixture& fixture, const std::string& reason) {
    std::string error;
    Require(!aoe::ValidateAoeProfile(fixture.profile, fixture.located, error),
            "Unsafe synthetic profile passed validation");
    Require(error.find(reason) != std::string::npos,
            "Expected pure guard was not reached before file validation: " + error);
}

std::vector<uint8_t> ReadFixtureSource(const std::filesystem::path& path) {
    const auto size = std::filesystem::file_size(path);
    Require(size > 0 && size <= 512ull * 1024 * 1024, "Invalid integration image size");
    std::vector<uint8_t> bytes(static_cast<size_t>(size));
    std::ifstream input(path, std::ios::binary);
    Require(bool(input.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()))),
            "Cannot read integration image");
    return bytes;
}

template<class T> T FixtureRead(const std::vector<uint8_t>& bytes, size_t offset) {
    Require(offset <= bytes.size() && sizeof(T) <= bytes.size() - offset, "Truncated fixture metadata");
    T value{};
    std::memcpy(&value, bytes.data() + offset, sizeof(value));
    return value;
}

struct FixturePe {
    std::vector<IMAGE_SECTION_HEADER> sections;
    std::vector<RUNTIME_FUNCTION> functions;
    explicit FixturePe(const std::vector<uint8_t>& bytes) {
        const auto dos = FixtureRead<IMAGE_DOS_HEADER>(bytes, 0);
        Require(dos.e_magic == IMAGE_DOS_SIGNATURE && dos.e_lfanew >= 0, "Invalid fixture DOS header");
        const auto header = FixtureRead<IMAGE_NT_HEADERS64>(bytes, static_cast<size_t>(dos.e_lfanew));
        Require(header.Signature == IMAGE_NT_SIGNATURE && header.FileHeader.Machine == IMAGE_FILE_MACHINE_AMD64,
                "Invalid fixture AMD64 header");
        const size_t table = static_cast<size_t>(dos.e_lfanew) + 24 + header.FileHeader.SizeOfOptionalHeader;
        for (unsigned i = 0; i < header.FileHeader.NumberOfSections; ++i)
            sections.push_back(FixtureRead<IMAGE_SECTION_HEADER>(bytes, table + i * sizeof(IMAGE_SECTION_HEADER)));
        const auto& exception = header.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXCEPTION];
        Require(exception.Size != 0 && exception.Size % sizeof(RUNTIME_FUNCTION) == 0, "Invalid fixture exception table");
        const auto start = offset(exception.VirtualAddress, exception.Size);
        for (size_t i = 0; i < exception.Size; i += sizeof(RUNTIME_FUNCTION))
            functions.push_back(FixtureRead<RUNTIME_FUNCTION>(bytes, start + i));
    }
    size_t offset(uint64_t rva, size_t length) const {
        for (const auto& section : sections) {
            if (rva >= section.VirtualAddress && rva - section.VirtualAddress <= section.SizeOfRawData &&
                length <= section.SizeOfRawData - (rva - section.VirtualAddress))
                return static_cast<size_t>(section.PointerToRawData) + static_cast<size_t>(rva - section.VirtualAddress);
        }
        throw std::runtime_error("Fixture RVA is not file-backed");
    }
    bool executable(uint64_t rva, size_t length) const {
        for (const auto& section : sections)
            if ((section.Characteristics & IMAGE_SCN_MEM_EXECUTE) && rva >= section.VirtualAddress &&
                rva - section.VirtualAddress <= section.SizeOfRawData &&
                length <= section.SizeOfRawData - (rva - section.VirtualAddress)) return true;
        return false;
    }
};

class MutatedImageFixture {
public:
    MutatedImageFixture() {
        const std::filesystem::path directory = LR"(C:\Users\Public\Documents\4UnityAOEManager\build\Release\test-artifacts)";
        std::filesystem::create_directories(directory);
        path_ = directory / L"aoe-locator-mutated.bin";
        Require(!std::filesystem::exists(path_), "Existing mutation fixture retained; remove it explicitly before retrying");
        owned_ = true;
    }
    ~MutatedImageFixture() {
        if (owned_) { std::error_code ignored; std::filesystem::remove(path_, ignored); }
    }
    MutatedImageFixture(const MutatedImageFixture&) = delete;
    MutatedImageFixture& operator=(const MutatedImageFixture&) = delete;
    const std::filesystem::path& write(const std::vector<uint8_t>& bytes) const {
        std::ofstream output(path_, std::ios::binary | std::ios::trunc);
        Require(bool(output.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()))),
                "Cannot write private mutation fixture");
        output.close();
        Require(bool(output), "Cannot flush private mutation fixture");
        return path_;
    }
private:
    std::filesystem::path path_;
    bool owned_ = false;
};

const aoe::AoeSignatureResult& Signature(const aoe::AoeLocatorResult& result, const std::string& name) {
    for (const auto& signature : result.signatures) if (signature.name == name) return signature;
    throw std::runtime_error("Expected signature result missing: " + name);
}
}

// Default: self-contained synthetic tests with no target/file access. Explicit
// --current-image adds static disk analysis and an owned temporary .bin only.
// No mode opens a process, runs a fixture, uses a debugger, or starts the GUI.
int main(int argc, char** argv) {
    const bool currentImage = argc == 2 && std::string(argv[1]) == "--current-image";
    if (argc != 1 && !currentImage) {
        std::cerr << "Kullanim: aoe_locator_tests [--current-image]\n";
        return 2;
    }
    unsigned passed = 0;
    unsigned failed = 0;
    const auto check = [&](const char* name, const std::function<void()>& body) {
        try {
            body();
            ++passed;
            std::cout << "[PASS] " << name << '\n';
        } catch (const std::exception& e) {
            ++failed;
            std::cerr << "[FAIL] " << name << ": " << e.what() << '\n';
        } catch (...) {
            ++failed;
            std::cerr << "[FAIL] " << name << ": unknown exception\n";
        }
    };

    check("unique_match", [] { RequireMatches({0x90, 0x48, 0x8B, 0xC8, 0x90}, "48 8B C8", {1}); });
    check("zero_matches", [] { RequireMatches({0x48, 0x89, 0xC8}, "48 8B C8", {}); });
    check("multiple_matches_are_retained", [] { RequireMatches({0x48, 0x8B, 0xC8, 0x90, 0x48, 0x8B, 0xC8}, "48 8B C8", {0, 4}); });
    check("overlapping_matches_are_retained", [] { RequireMatches({0xAA, 0xAA, 0xAA}, "AA AA", {0, 1}); });
    check("first_and_last_possible_offsets", [] { RequireMatches({0x0F, 0xB6, 0x90, 0x0F, 0xB6}, "0F B6", {0, 3}); });
    check("empty_input_has_no_matches", [] { RequireMatches({}, "90", {}); });
    check("pattern_larger_than_input", [] { RequireMatches({0x48}, "48 8B C8", {}); });
    check("hex_case_and_whitespace", [] { RequireMatches({0x48, 0x8B, 0xC8}, " \t48  8b\nC8\r\n", {0}); });
    check("single_and_double_question_mark_tokens", [] {
        RequireMatches({0x48, 0x8B, 0xC8}, "48 ? ??", {0});
        RequireMatches({0x48, 0x89, 0xC8}, "48 ? C8", {0});
    });
    check("wildcard_rel32_accepts_moved_call", [] {
        const std::string pattern = "E8 ?? ?? ?? ?? 48 8B C8";
        auto forward = CallBytes(0x123456);
        auto backward = CallBytes(-0x123456);
        for (auto* bytes : {&forward, &backward}) bytes->insert(bytes->end(), {0x48, 0x8B, 0xC8});
        RequireMatches(forward, pattern, {0});
        RequireMatches(backward, pattern, {0});
    });
    const std::vector<uint8_t> visualSequence={0x8B,0x8A,0x8C,0x00,0x00,0x00,0x85,0xC9,0x0F,0x84,0xC4,0x00,0x00,0x00,0xE8,0xBA,0xA7,0x27,0x00};
    check("masked visual reader locator unique match",[&]{const auto found=aoe::DecodeVisualReaderSequences(visualSequence,0x7F2DE3,0xF48000);Require(found.size()==1&&found[0].rva==0x7F2DE3&&found[0].lookupRva==0xA6D5B0,"unique visual reader");});
    check("visual reader zero match fails closed",[&]{auto bytes=visualSequence;bytes[2]=0x80;Require(aoe::DecodeVisualReaderSequences(bytes,0x7F2DE3,0xF48000).empty(),"wrong displacement rejected");});
    check("duplicate visual reader remains ambiguous",[&]{auto bytes=visualSequence;bytes.push_back(0x90);bytes.insert(bytes.end(),visualSequence.begin(),visualSequence.end());Require(aoe::DecodeVisualReaderSequences(bytes,0x7F2DE3,0xF48000).size()==2,"duplicate candidates retained for fail-closed selection");});
    check("visual reader derives displacement and base register",[&]{const auto c=aoe::DecodeVisualReaderSequences(visualSequence,0x7F2DE3,0xF48000).front();Require(c.fieldOffset==0x8C&&c.baseRegister==aoe::X64RegisterId::Rdx&&c.valueRegister==aoe::X64RegisterId::Rcx,"RDX+8C to ECX data flow");});
    check("exact_opcode_survives_rel32_masking", [] {
        RequireMatches({0xE9, 1, 2, 3, 4, 0x48, 0x8B, 0xC8}, "E8 ?? ?? ?? ?? 48 8B C8", {});
    });
    check("template_constant_is_not_a_wildcard", [] {
        RequireMatches({0x41, 0xB8, 0x0B, 0x02, 0, 0}, "41 B8 0B 02 00 00", {0});
        RequireMatches({0x41, 0xB8, 0x0A, 0x02, 0, 0}, "41 B8 0B 02 00 00", {});
    });
    check("empty_and_whitespace_patterns_fail_closed", [] {
        RequireInvalidPattern("");
        RequireInvalidPattern(" \t\r\n");
    });
    check("all_wildcard_patterns_fail_closed", [] {
        RequireInvalidPattern("?");
        RequireInvalidPattern("??");
        RequireInvalidPattern("? ?? ?");
        RequireInvalidPattern("?? ?? ??");
    });
    check("malformed_tokens_fail_closed", [] {
        for (const char* pattern : {"GG", "0", "100", "0x90", "90, E8", "9?", "?9", "90 ZZ", "-1"})
            RequireInvalidPattern(pattern);
    });
    check("call_positive_rel32", [] { RequireCall(0x20, 0x100, 0x1000, 0x125); });
    check("call_negative_rel32", [] { RequireCall(-0x25, 0x100, 0x1000, 0xE0); });
    check("call_zero_displacement", [] { RequireCall(0, 0x100, 0x1000, 0x105); });
    check("call_target_at_image_start", [] { RequireCall(-0x105, 0x100, 0x1000, 0); });
    check("call_target_at_last_image_byte", [] { RequireCall(0xFA, 0x100, 0x200, 0x1FF); });
    check("call_instruction_ends_at_image_boundary", [] { RequireCall(-5, 0xFB, 0x100, 0xFB); });
    check("call_ignores_trailing_instruction_bytes", [] {
        auto bytes = CallBytes(0x20);
        bytes.insert(bytes.end(), {0x48, 0x8B, 0xC8});
        uint64_t target = 0;
        Require(aoe::DecodeAoeCall(bytes, 0x100, 0x1000, target) && target == 0x125,
                "Valid CALL followed by another instruction was rejected");
    });
    check("call_truncated_bytes_rejected", [] {
        for (const auto& bytes : {std::vector<uint8_t>{}, std::vector<uint8_t>{0xE8}, std::vector<uint8_t>{0xE8, 0, 0, 0}}) {
            uint64_t target = 0;
            Require(!aoe::DecodeAoeCall(bytes, 0x100, 0x1000, target), "Truncated CALL accepted");
        }
    });
    check("non_call_opcode_rejected", [] {
        for (uint8_t opcode : {uint8_t{0xE9}, uint8_t{0x90}, uint8_t{0xFF}}) {
            auto bytes = CallBytes(0);
            bytes[0] = opcode;
            uint64_t target = 0;
            Require(!aoe::DecodeAoeCall(bytes, 0x100, 0x1000, target), "Non-E8 opcode accepted");
        }
    });
    check("call_target_before_image_rejected", [] {
        uint64_t target = 0;
        Require(!aoe::DecodeAoeCall(CallBytes(-0x106), 0x100, 0x1000, target), "Negative target accepted");
        Require(!aoe::DecodeAoeCall(CallBytes(std::numeric_limits<int32_t>::min()), 0x100, 0x1000, target), "INT32_MIN underflow accepted");
    });
    check("call_target_at_or_beyond_image_end_rejected", [] {
        uint64_t target = 0;
        Require(!aoe::DecodeAoeCall(CallBytes(0xFB), 0x100, 0x200, target), "One-past-image target accepted");
        Require(!aoe::DecodeAoeCall(CallBytes(std::numeric_limits<int32_t>::max()), 0x100, 0x1000, target), "Far out-of-image target accepted");
    });
    check("call_instruction_outside_image_rejected", [] {
        uint64_t target = 0;
        Require(!aoe::DecodeAoeCall(CallBytes(-5), 0x100, 0x100, target), "Out-of-image instruction accepted");
        Require(!aoe::DecodeAoeCall(CallBytes(-5), 0xFD, 0x100, target), "Partial instruction past image end accepted");
        Require(!aoe::DecodeAoeCall(CallBytes(0), 0, 0, target), "Empty image accepted");
    });
    check("call_rva_overflow_rejected", [] {
        uint64_t target = 0;
        const auto maximum = std::numeric_limits<uint64_t>::max();
        Require(!aoe::DecodeAoeCall(CallBytes(0), maximum - 2, maximum, target), "RVA plus instruction length overflow accepted");
    });
    check("unresolved_locator_blocks_profile_before_file_access", [] {
        auto fixture = ReadyProfileFixture();
        fixture.located.ready = false;
        fixture.located.error = "synthetic unresolved signature";
        RequireProfileGuard(fixture, "AOE locator is not ready");
    });
    check("sha_mismatch_blocks_profile_before_file_access", [] {
        auto fixture = ReadyProfileFixture();
        fixture.profile.targetSha256 = std::string(64, 'B');
        RequireProfileGuard(fixture, "AOE profile SHA mismatch");
    });
    check("missing_signatures_block_ready_profile", [] {
        auto fixture = ReadyProfileFixture();
        fixture.located.signatures.clear();
        RequireProfileGuard(fixture, "disagree with exact SHA profile RVAs");
    });
    check("missing_required_signature_blocks_ready_profile", [] {
        auto fixture = ReadyProfileFixture();
        fixture.located.signatures.pop_back();
        RequireProfileGuard(fixture, "disagree with exact SHA profile RVAs");
    });
    check("invalid_signature_blocks_ready_profile", [] {
        auto fixture = ReadyProfileFixture();
        fixture.located.signatures.front().valid = false;
        RequireProfileGuard(fixture, "disagree with exact SHA profile RVAs");
    });
    check("tampered_locator_rva_blocks_ready_profile", [] {
        auto fixture = ReadyProfileFixture();
        ++fixture.located.signatures.front().rva;
        RequireProfileGuard(fixture, "disagree with exact SHA profile RVAs");
    });
    check("tampered_profile_prep_and_return_relationships_rejected", [] {
        auto fixture = ReadyProfileFixture();
        ++fixture.profile.runtime.initialPrepRva;
        RequireProfileGuard(fixture, "disagree with exact SHA profile RVAs");
        fixture = ReadyProfileFixture();
        ++fixture.profile.runtime.initialReturnRva;
        RequireProfileGuard(fixture, "disagree with exact SHA profile RVAs");
    });
    check("disabled_initial_nx_blocks_ready_profile", [] {
        auto fixture = ReadyProfileFixture();
        fixture.profile.runtime.initialNxAvailable = false;
        fixture.profile.runtime.initialNxUnavailableReason = "synthetic disabled runtime capability";
        RequireProfileGuard(fixture, "synthetic disabled runtime capability");
    });
    check("missing_fingerprint_blocks_ready_profile", [] {
        auto fixture = ReadyProfileFixture();
        fixture.profile.runtime.initialCallBytes.clear();
        RequireProfileGuard(fixture, "requires every replay");
    });
    check("wrong_call_fingerprint_target_blocks_ready_profile", [] {
        auto fixture = ReadyProfileFixture();
        fixture.profile.runtime.initialCallBytes = CallBytes(0);
        RequireProfileGuard(fixture, "direct-call fingerprints do not resolve");
    });
    check("known-profile visual RVA mismatch is rejected before file access",[]{auto fixture=ReadyProfileFixture();auto&r=fixture.profile.runtime;r.visualSuppressionAvailable=true;r.visualReaderRva=0x700;r.visualLookupRva=0x800;r.visualFieldOffset=0x8C;r.visualBaseRegister=aoe::X64RegisterId::Rdx;r.visualReaderBytes={0x8B};fixture.located.visualReady=true;fixture.located.visualReader.rva=0x701;fixture.located.visualReader.lookupRva=0x800;fixture.located.visualReader.fieldOffset=0x8C;fixture.located.visualReader.baseRegister=aoe::X64RegisterId::Rdx;RequireProfileGuard(fixture,"VisualSfxReader locator/profile semantic mismatch");});
    check("null_live_handle_fails_before_process_access", [] {
        const auto fixture = ReadyProfileFixture();
        aoe::TargetInfo target;
        target.verified = true;
        target.size = fixture.located.image.imageSize;
        target.image.sha256 = fixture.located.image.sha256;
        std::string error;
        Require(!aoe::ValidateAoeLive(nullptr, target, fixture.located, error), "Null live handle accepted");
        Require(error == "Invalid AOE live session/build identity", "Null handle reached process identity API");
    });

    if (currentImage) {
        try {
            const std::filesystem::path source = LR"(C:\Games\4Unity\TClient.exe)";
            const auto original = ReadFixtureSource(source);
            const FixturePe pe(original);
            MutatedImageFixture temporary;
            aoe::AoeLocatorResult located;
            aoe::BuildProfile profile;
            std::filesystem::path selected;
            std::string error;
            check("current_image_locator_and_canonical_profile_match", [&] {
                Require(aoe::LocateAoeImage(source, located, error), error);
                Require(located.ready && located.visualReady && located.visualReader.rva!=0 && located.visualReader.baseRegister==aoe::X64RegisterId::Rdx && located.visualReader.fieldOffset==0x8C && !located.signatures.empty() && !located.liveEvidence.empty(), "Locator evidence incomplete");
                auto profiles=temporary.write(original).parent_path()/L"recovery-profiles";
                Require(aoe::RecoverAndSaveAoeProfile(source,profiles,profile,selected,error),error);
                aoe::BuildProfile loaded;Require(aoe::LoadBuildProfile(selected,loaded,error),error);profile=loaded;
                Require(aoe::ValidateAoeProfile(profile, located, error), error);
            });
            Require(located.ready, "Positive locator result is required before mutation fixtures");
            const auto prepCall = Signature(located, "AoeLifecycleCaller").rva;
            Require(prepCall >= located.prepDistance, "Lifecycle call cannot contain preparation prefix");
            const uint64_t prep = prepCall - located.prepDistance;
            check("recovered_profile_field_and_fingerprint_tampering_rejected",[&]{
                auto bad=profile;bad.runtime.actorIdOffset+=8;
                Require(!aoe::ValidateAoeProfile(bad,located,error),"Wrong actor field accepted");
                bad=profile;bad.runtime.actorTreeOffsets[2]+=8;
                Require(!aoe::ValidateAoeProfile(bad,located,error),"Wrong typed tree accepted");
                bad=profile;bad.runtime.liveValidationRequired=false;
                Require(!aoe::ValidateAoeProfile(bad,located,error),"Live gate removed");
                bad=profile;bad.runtime.sharedWorkerBytes.resize(1);
                Require(!aoe::ValidateAoeProfile(bad,located,error),"Truncated fingerprint accepted");
            });
            check("same_sha_aoe_profile_uses_local_semantics_without_full_scan",[&]{
                aoe::AoeLocatorResult cached;Require(aoe::LocateAoeImage(source,cached,error,&profile),error);
                Require(cached.cacheUsed&&aoe::ValidateAoeProfile(profile,cached,error),error);
            });
            check("corrupt_aoe_cache_replaced_atomically",[&]{
                std::ofstream(selected)<<"{}";
                Require(aoe::RecoverAndSaveAoeProfile(source,selected.parent_path(),profile,selected,error),error);
                Require(aoe::LoadBuildProfile(selected,profile,error),error);
                Require(aoe::ValidateAoeProfile(profile,located,error),error);
            });
            check("mutated_dos_header_rejected", [&] {
                auto bytes = original;
                bytes[0] = bytes[1] = 0;
                aoe::AoeLocatorResult rejected;
                Require(!aoe::LocateAoeImage(temporary.write(bytes), rejected, error) && !rejected.ready,
                        "Malformed DOS signature was accepted");
            });
            check("truncated_file_backed_section_rejected", [&] {
                size_t requiredEnd = 0;
                for (const auto& section : pe.sections)
                    if (section.SizeOfRawData) requiredEnd = (std::max)(requiredEnd,
                        static_cast<size_t>(section.PointerToRawData) + section.SizeOfRawData);
                Require(requiredEnd > 1 && requiredEnd <= original.size(), "Invalid fixture section extent");
                auto bytes = original;
                bytes.resize(requiredEnd - 1);
                aoe::AoeLocatorResult rejected;
                Require(!aoe::LocateAoeImage(temporary.write(bytes), rejected, error) && !rejected.ready,
                        "Truncated section was accepted");
            });
            check("changed_stable_prep_opcode_has_zero_lifecycle_matches", [&] {
                auto bytes = original;
                const auto start = pe.offset(prep, 1);
                bytes[start] ^= 1;
                aoe::AoeLocatorResult rejected;
                Require(!aoe::LocateAoeImage(temporary.write(bytes), rejected, error) && !rejected.ready,
                        "Changed preparation opcode was accepted");
                const auto& signature = Signature(rejected, "AoeLifecycleCaller");
                Require(signature.matches.empty() && !signature.valid, "Stable opcode mutation did not remove lifecycle match");
            });
            check("moved_rel32_to_wrong_executable_worker_rejected", [&] {
                auto bytes = original;
                const auto wrongTarget = Signature(located, "SharedOperation020A").rva + 1;
                Require(pe.executable(wrongTarget, 1), "Wrong target fixture must remain executable");
                const auto displacement = static_cast<int64_t>(wrongTarget) - static_cast<int64_t>(prepCall + 5);
                Require(displacement >= INT32_MIN && displacement <= INT32_MAX, "Fixture CALL displacement does not fit rel32");
                const auto call = CallBytes(static_cast<int32_t>(displacement));
                const auto offset = pe.offset(prepCall, call.size());
                std::copy(call.begin(), call.end(), bytes.begin() + offset);
                aoe::AoeLocatorResult rejected;
                Require(!aoe::LocateAoeImage(temporary.write(bytes), rejected, error) && !rejected.ready,
                        "Mismatched executable worker target was accepted");
                Require(Signature(rejected, "AoeLifecycleCaller").valid && !Signature(rejected, "SharedOperation020A").valid,
                        "Masked caller should remain unique while CALL relationship rejects the worker");
            });
            check("duplicate_semantically_valid_lifecycle_is_ambiguous", [&] {
                const size_t copiedLength = located.prepDistance + 6 + (located.prepDistance==28?26:16);
                const auto originalOffset = pe.offset(prep, copiedLength);
                uint64_t destination = 0;
                for (const auto& function : pe.functions) {
                    if (function.EndAddress < function.BeginAddress || function.EndAddress - function.BeginAddress < copiedLength ||
                        (prep >= function.BeginAddress && prep < function.EndAddress) ||
                        !pe.executable(function.BeginAddress, copiedLength)) continue;
                    destination = function.BeginAddress;
                    break;
                }
                Require(destination != 0, "No independent file-backed unwind range fits duplicate fixture");
                auto bytes = original;
                const auto destinationOffset = pe.offset(destination, copiedLength);
                std::copy_n(original.begin() + originalOffset, copiedLength, bytes.begin() + destinationOffset);
                // Relocate the copied CALL to the same real worker so the
                // duplicate is semantically plausible, not just matching text.
                const auto worker = Signature(located, "SharedOperation020A").rva;
                const auto delta = static_cast<int64_t>(worker) - static_cast<int64_t>(destination + located.prepDistance + 5);
                Require(delta >= INT32_MIN && delta <= INT32_MAX, "Duplicate CALL displacement does not fit rel32");
                const auto relocatedCall = CallBytes(static_cast<int32_t>(delta));
                std::copy(relocatedCall.begin(), relocatedCall.end(), bytes.begin() + destinationOffset + located.prepDistance);
                aoe::AoeLocatorResult rejected;
                Require(!aoe::LocateAoeImage(temporary.write(bytes), rejected, error) && !rejected.ready,
                        "Duplicate valid lifecycle pattern was accepted");
                const auto& signature = Signature(rejected, "AoeLifecycleCaller");
                Require(signature.matches.size() >= 2 && !signature.valid &&
                            signature.evidence.find("semantic_matches=2") != std::string::npos,
                        "Duplicate must be rejected by semantic ambiguity, not by another failure");
            });
            check("source_image_unchanged_by_all_mutations", [&] {
                Require(ReadFixtureSource(source) == original, "Original target file changed during integration tests");
            });
        } catch (const std::exception& e) {
            ++failed;
            std::cerr << "[FAIL] current_image_fixture_setup: " << e.what() << '\n';
        }
    }

    std::cout << "AOE_LOCATOR_TESTS passed=" << passed << " failed=" << failed
              << " total=" << passed + failed << '\n';
    return failed ? 1 : 0;
}

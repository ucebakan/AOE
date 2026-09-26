# TClient patch recovery — 2026-09-18

## Build identity

The live executable selected by its running process is `C:\Games\4Unity\TClient.exe`.

| Field | Old build | New build |
|---|---:|---:|
| SHA-256 | `CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E` | `D0ECBBA10685D94E7CA632D6A9CEB9B3C2C420D609DB7CC4243AFF2F493A62C1` |
| File size | unavailable: old binary not present | `15,374,336` bytes |
| PE timestamp | unavailable | `0x6AAD0059` (`2026-09-18 09:11:53 UTC`) |
| SizeOfImage | historical Manager fixture value `0xF3B000`; not re-read from the absent file | `0xF40000` |
| Entry RVA | unavailable | `0xAAFD7C` |
| `.text` RVA | old disassembly shows code beginning at `0x1000` | `0x1000` |
| `.text` size | old dump summary rounded to `0xCB8000`; exact VirtualSize unavailable | VirtualSize `0xCBBCF8`, raw size `0xCBBE00` |
| File/product version | unavailable | `3.8.9.1` |

These are different builds. No executable was modified. The exact old executable was not found in the project, checkpoints, golden tracer project, related research project, game folder, or matching temporary backup names. The retained old `dumpbin` disassembly supported normalized instruction comparison, but an exact binary diff was impossible.

The pre-change checkpoint is:

`C:\Users\Public\Documents\4UnityAOEManager-checkpoints\manager-1.1.3-research-pre-new-tclient-recovery.zip`

Checkpoint SHA-256: `57990AA4B955DA1BD0083261F3193E883CC2162C545E010B413C63415CCAA3B2`.

## Recovered code anchors

| Semantic role | Old RVA | New RVA | Static evidence |
|---|---:|---:|---|
| initial target handler/builder | `0x6C950` | `0x6C950` | same parse, allocate, store, and vector append data flow |
| target vector observation | `0x6CC60` | `0x6CC60` | `lea rcx,[rdi+660h]` after ID/type record construction |
| dispatch to handler | `0x7D292E` | `0x7D35AE` | direct `E8 9D 93 89 FF` to `0x6C950` |
| template lookup | `0xA61090` | `0xA65AF0` | word key, ordered-tree sentinel `+0x19`, key `+0x20`, payload `+0x28` |
| typed actor resolver | `0x7B9BD0` | `0x7BA720` | ID rejection, type jump table, type-specific ordered-tree lookup |
| shared worker | `0x7BB160` | `0x7BBDE0` | vector iteration, typed resolution, actor filters, direct producer call |
| shared-worker producer call | `0x7BB4B7` | `0x7BC137` | direct `E8 04 D1 8D FF` to `0x99240` |
| producer | `0x99240` | `0x99240` | normalized prologue and unchanged ID/type serialization data flow |
| ID serialization | `0x993DC` | `0x993DC` | passes record ID in `EDX` to serializer |
| type serialization | `0x993E6` | `0x993E6` | `movzx edx,bl` passes record type |
| initial wrapper | `0x7F09B0` | `0x7F19F0` | template lookup, owner vector formation, shared path |
| initial replay preparation | `0x7F0900` | `0x7F1940` | `41 8B 46 24` and preserved argument preparation sequence |
| initial shared call | `0x7F091D` | `0x7F195D` | direct `E8 7E A4 FC FF` to `0x7BBDE0` |
| initial return | `0x7F0922` | `0x7F1962` | immediate post-call `90` boundary |
| linked activation path | `0x7A9740` | `0x7AA300` | reads linked word, stores operation at owner `+0x824`, looks up template, calls new wrapper |
| periodic updater | `0x79C980` | `0x79D540` | delta accumulation, period comparison/subtraction, template lookup, owner vector route |
| relationship/skill helper | `0x7AC960` | `0x7AD520` | same position between typed lookup and status filters |
| monster-only consumer | `0x7AF25B` | `0x7AFE1B` | compares record type `2`, searches context `+0x1120` tree |

The new initial shared-call block prepares `R9=RSI`, `R8=R14`, `RDX=R15`, and `RCX=R13`, with the remaining arguments at stack offsets `+0x20`, `+0x28`, and `+0x30`. The call target is decoded from the signed `rel32`; it is not accepted merely because the five bytes begin with `E8`.

## Structure and record layout

| Meaning | Old | New | Result and proof |
|---|---:|---:|---|
| owner vector begin | `+0x660` | `+0x660` | unchanged; builder forms it and shared initial path consumes it |
| owner vector end | `+0x668` | `+0x668` | unchanged; builder reads/updates header `+8` |
| owner vector capacity | `+0x670` | `+0x670` | unchanged; builder compares header `+0x10` |
| record size | `8` | `8` | unchanged; allocation requests 8 bytes |
| record ID | `+0` uint32 | `+0` uint32 | unchanged; builder store and producer serialization agree |
| record type | `+4` uint8 | `+4` uint8 | unchanged; builder store and producer serialization agree |
| padding | `+5..+7` | `+5..+7` | still not written or serialized by this path |
| maximum entries | `32` | `32` | unchanged; wrapper computes qword count and compares with `0x20` |
| actor ID | `+0x768` | `+0x768` | typed resolver and worker identity checks agree |
| actor category | `+0x7E1` | `+0x7E1` | concrete category writers and consumers agree |
| eligibility/lifecycle | `+0x7CA` | `+0x7CA` | shared worker compares it after resolution |
| status | `+0x7E4` | `+0x7E4` | shared worker compares it after relationship validation |
| monster tree | context `+0x1120` | context `+0x1120` | type-2 resolver helper and monster-only consumer agree |

The typed resolver at new `0x7BA720` dispatches on `R8B` after rejecting a zero ID. Its verified tree namespace offsets remain: type 1 `+0x1140`, type 2 `+0x1120`, type 7 `+0x11B0`, type 9 `+0x1130`, type 10 `+0xC6FF00`, type 11 `+0x11A0`, type 17 `+0x1150`, and type 19 `+0x11E0`. The ordered tree uses sentinel byte `node+0x19`, key `node+0x20`, and payload pointer `node+0x28`.

## Actor categories

Concrete new-build category writes are:

- player/character value `1`: new `0x780597` (old `0x78050E`)
- monster value `2`: new `0x83FB4F` (old `0x83D696`)
- NPC value `3`: new `0x8505CB` (old `0x84E120`)
- recall/pet namespace value `7`: new `0x8719B6` (old `0x86F50D`)

The new verified monster discriminator is `actor+0x7E1 == 2`. Type/category 11 remains a recall-related namespace whose exact subtype is not proven.

## Initial and periodic paths

The initial path is statically connected as:

`0x7D35AE -> 0x6C950 -> 0x7F19F0 -> 0x7F1720 -> 0x7F195D -> 0x7BBDE0 -> 0x7BC137 -> 0x99240`

The linked/periodic route is statically connected as:

`0x7AA300 -> 0x7F19F0` and `0x79D540 -> 0x7F1720 -> 0x7F195D -> 0x7BBDE0 -> 0x99240`

The code reads the operation word from runtime data. Static analysis proves the relocated operation flow and its data dependencies, but does not by itself re-observe values `0x0209` and `0x020A` on the new process.

## Profile, signatures, and runtime policy

New profile:

`profiles\D0ECBBA10685D94E7CA632D6A9CEB9B3C2C420D609DB7CC4243AFF2F493A62C1.json`

The old SHA profile remains present. Runtime addresses now come from the SHA-selected profile. Live attachment checks exact instruction fingerprints for the preparation, shared call, return, worker, producer call/target, typed resolver, vector builder, and ID/type serializers. It also decodes both direct calls and requires their targets to equal the semantic profile anchors. A missing profile, missing fingerprint, mismatched byte, wrong call target, out-of-range anchor, or ambiguous process fails closed.

`signatures\aoe_signatures.json` now records masked multi-instruction patterns, semantic relationships, known old/new RVAs, and an explicit zero/multiple-match ambiguity policy. These signatures aid future recovery; they are not treated as permanent proof.

The read-only Primary Target Inspector is available on the new profile because its vector layout, shared call, resolver, and live byte gates are defined. It reads process memory and uses one execution breakpoint. Its capture implementation contains no `WriteProcessMemory`, target-code patch, or RIP redirect.

Initial Nx is available only after same-session read-only validation. Version 1.1.6 observes the recovered prep/call/return path while replay is disarmed and requires the complete normal `1 × 0x0209 + 9 × 0x020A` cast before manual or automatic arming becomes eligible. Auto Arm can arm only the next cast.

## Live validation status

The TClient process was live as PID `37356`, but this non-elevated research process received Win32 error 5 while requesting the module snapshot needed for guarded live reads. No debugger was attached and no runtime event was fabricated.

Validated live: process existence only.

Pending live read-only validation:

1. module base and live profile bytes
2. initial operation `0x0209`
3. periodic operation `0x020A`
4. normal initial vector count
5. record ID/type readability
6. typed actor resolution
7. resolved actor category

These pending observations are the exact blocker for enabling Initial Nx on the new build.

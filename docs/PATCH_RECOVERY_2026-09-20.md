# TClient patch recovery — 2026-09-20

## Build identity

The recovered image is `C:\Games\4Unity\TClient.exe`, SHA-256 `FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7`, file size `15,400,448`, PE timestamp `0x6AAF1F33` (`2026-09-20 02:48:03 UTC`), `SizeOfImage 0xF46000`, and entry RVA `0xAB5ACC`.

| Section | RVA | Virtual size | Raw size |
|---|---:|---:|---:|
| `.text` | `0x1000` | `0xCC1A98` | `0xCC1C00` |
| `.rdata` | `0xCC3000` | `0x157D12` | `0x157E00` |
| `.data` | `0xE1B000` | `0xAEC64` | `0x1B000` |
| `.pdata` | `0xECA000` | `0x43E24` | `0x44000` |
| `.fptable` | `0xF0E000` | `0x100` | `0x200` |
| `.rsrc` | `0xF0F000` | `0x1DE08` | `0x1E000` |
| `.reloc` | `0xF2D000` | `0x18D20` | `0x18E00` |

The previous image was 15,374,336 bytes with `.text` VirtualSize `0xCBBCF8`, raw size `0xCBBE00`, entry RVA `0xAAFD7C`, and `SizeOfImage 0xF40000`. File growth is `0x6600`, while recovered anchor deltas range from `-0x120` to `+0x5EC0`; no uniform RVA shift exists.

## Recovered semantic anchors

| Role | Previous RVA | Current RVA | Evidence |
|---|---:|---:|---|
| target/action handler | `0x6C950` | `0x6C830` | same parse, 8-byte allocation, ID/type stores and vector append |
| vector boundary | `0x6CC60` | `0x6CB40` | `lea rcx,[rdi+660h]`, then end/capacity operations |
| parsed observation | `0x6CC93` | `0x6CB73` | unique `66 47 39 74 6F 3C` comparison |
| dispatch | `0x7D35AE` | `0x7D3E3E` | direct call decodes to `0x6C830` |
| template lookup | `0xA65AF0` | `0xA6B9B0` | word key and ordered-tree sentinel/key/payload layout |
| typed resolver | `0x7BA720` | `0x7BAF60` | zero-ID rejection and type jump table; type 2 routes to `context+0x1120` |
| shared worker | `0x7BBDE0` | `0x7BC4C0` | record iteration, typed resolution, actor filters and producer call |
| producer call | `0x7BC137` | `0x7BC817` | direct `E8 34 C9 8D FF` decodes to `0x99150` |
| producer | `0x99240` | `0x99150` | record-vector iteration and unchanged ID/type data flow |
| ID serialization | `0x993DC` | `0x992EC` | record `+0` loaded into `EDX` before serializer call |
| type serialization | `0x993E6` | `0x992F6` | record `+4` byte passed in `EDX` |
| linked activation | `0x7AA300` | `0x7AA8F0` | linked word propagation, template lookup and wrapper call |
| periodic updater block | `0x79D540` | `0x79DF69` | delta accumulation, period compare/subtract, template lookup and shared preparation call |
| Initial Nx wrapper | `0x7F19F0` | `0x7F2810` | template check, owner vector formation and inner shared preparation |
| initial prep | `0x7F1940` | `0x7F2760` | operation-derived value and the same argument preparation sequence |
| initial shared call | `0x7F195D` | `0x7F277D` | direct call to recovered shared worker |
| initial return | `0x7F1962` | `0x7F2782` | immediate post-call NOP boundary |

The initial call bytes are `E8 3E 9D FC FF`; signed `rel32` decoding gives `0x7BC4C0`. The producer call bytes are `E8 34 C9 8D FF`; decoding gives `0x99150`.

The statically proven initial chain is:

`0x7D3E3E -> 0x6C830 -> 0x7F2810 -> 0x7F2540 -> 0x7F277D -> 0x7BC4C0 -> 0x7BC817 -> 0x99150`

The linked and periodic entrances are `0x7AA8F0 -> 0x7F2810` and `0x79DF69 -> 0x7F2540`. At `0x7F2725` the operation word is read from `[RSI]`; the call setup preserves that record pointer as `R9=RSI`. This preserves the decoder’s `R9` semantics. Static analysis does not re-observe the runtime values `0x0209` and `0x020A`, so the profile requires same-session live validation.

## Layout validation

All requested offsets are **CONFIRMED** in their prior semantic roles: vector `+0x660/+0x668/+0x670`; actor ID `+0x768`; eligibility `+0x7CA`; category/type `+0x7E1`; status `+0x7E4`; and the type-2 monster tree at context `+0x1120`. Record size remains 8 bytes with ID at `+0` and typed namespace at `+4`.

Concrete category writes moved to character `0x7807C5` (`1`), monster `0x8417E6` (`2`), NPC `0x852B60` (`3`), and recall/pet namespace `0x8740BD` (`7`).

## Profile policy

The new profile is `profiles\FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7.json`. It validates exact bytes at prep, call, return, worker, producer, producer call, typed resolver, vector builder, parsed boundary, and both serializers. Both direct calls must decode to their recovered targets. Unknown hashes and any mismatch remain fail closed.

Static recovery restores normal attachment for this exact SHA. It does not arm Initial Nx. The required first runtime action remains one normal AOE with Auto Arm off, used only to observe one `0x0209`, about nine `0x020A` calls and returns, prep/call/return coverage, and preserved register semantics.

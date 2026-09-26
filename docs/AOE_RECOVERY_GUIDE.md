# 4Unity AOE Recovery Guide

## New build recovery — 2026-09-18

Manager 1.1.10-research keeps separate SHA-selected profiles for the `CD772F3F...`, `D0ECBBA1...`, and `FB13C125...` builds. On the current build, the read-only Primary Target Inspector uses profile-specific `+0x7F277D` (shared call), `+0x7BC4C0` (worker), `+0x7BAF60` (typed resolver), and owner `+0x660` layout. The shared call is accepted only when its exact live bytes decode to the profile worker. Initial Nx becomes eligible only after same-session, read-only `0x0209`/`0x020A` prep/call/return validation completes. See [PATCH_RECOVERY_2026-09-20.md](PATCH_RECOVERY_2026-09-20.md).

The sections below retain the old-build evidence and addresses as historical reference.

## Multikill / Target Selection Research

The current 1.1.3-research build includes a read-only **AOE Primary Target Inspector** under **Research / Recovery**. On the relevant initial path, `TClient.exe+7D0D6C | 0x7FF69DD40D6C` forms `entity+0x660`, and `TClient.exe+7F091D | 0x7FF69DD6091D` passes that vector to `TClient.exe+7BB160 | 0x7FF69DD2B160`. The count validation at `TClient.exe+7F073F | 0x7FF69DD6073F` through `TClient.exe+7F0750 | 0x7FF69DD60750` bounds it to 32 entries.

Each slot in `entity+0x660` points to an 8-byte record containing a dword live-object ID at `+0x00` and a byte object type at `+0x04`. `TClient.exe+7B9BD0 | 0x7FF69DD29BD0` resolves this pair through a type-partitioned live-object tree. Type 2 uses the context `+0x1120` tree and is `CTClientMonster`. The surviving ID and type are serialized unchanged by `TClient.exe+99240 | 0x7FF69D609240`.

`entity+0x688` is closed as a direct target-vector hypothesis. It contains allocated configuration/effect descriptors populated from `entity+0x258`; the old 1/3/4 count correlation did not measure nearby monsters. Details are in [ENTITY_660_TARGET_ANALYSIS.md](ENTITY_660_TARGET_ANALYSIS.md), [MULTIKILL_TARGET_RESEARCH.md](MULTIKILL_TARGET_RESEARCH.md), [ENTITY_688_STATIC_ANALYSIS.md](ENTITY_688_STATIC_ANALYSIS.md), and [AOE_SOURCE_COLLECTION_ANALYSIS.md](AOE_SOURCE_COLLECTION_ANALYSIS.md).

The remaining question is whether the normal one-entry actor record is the exact UI-selected mob. The inspector captures only `RDX+0x660`; it no longer scans R12 for candidates. It reports raw records, decoded IDs/types, and actors resolved through the game’s read-only lookup structure.

Use this workflow:

1. Attach to the known VALID profile. If Initial Nx is armed, press **PREPARE PRIMARY TARGET INSPECTOR**; this releases only the Initial Nx hardware-breakpoint state and keeps the attached validated session.
2. Press **START PRIMARY TARGET INSPECTOR**. The saved Auto Arm preference is temporarily suppressed for this research session.
3. Select Mob A, press **MARK AOE CAST**, and cast one normal AOE.
4. Repeat for Mob B and Mob C. For D, clear the selection first if the client permits a cast without one.
5. Press **STOP**, then **EXPORT JSON**. Compare count, raw 8 bytes, actor ID/type, resolved pointer, and changes from capture A.

Initial Nx replay remains inactive throughout the inspector session. The inspector uses hardware execution breakpoints and guarded process reads only; it performs no `WriteProcessMemory` call and no RIP redirect.

## OVERVIEW

This document preserves the relationships needed to recover AOE Initial Nx after a future `TClient.exe` patch. Addresses below are RVAs for one verified build. Never reuse old absolute addresses or heap/register state across sessions.

## KNOWN BUILD

- Target: `C:\Games\4Unity\TClient.exe`
- SHA-256: `CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E`
- Runtime address rule: `current live module base + profile RVA`
- Known profile: `profiles/CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E.json`

## NORMAL AOE BEHAVIOR

Normal captures showed one initial operation `0x0209` (521), followed by approximately nine linked periodic `0x020A` (522) operations at roughly one-second intervals. Both reach the shared callsite at `TClient.exe+7F091D | 0x7FF69DD6091D`, which calls `TClient.exe+7BB160 | 0x7FF69DD2B160` and returns at `TClient.exe+7F0922 | 0x7FF69DD60922`. At shared-worker entry, `word [R9]` identifies `0x0209` for the initial operation and `0x020A` for the periodic operation.

## INITIAL 0x0209 PATH

Verified recovery chain:

`TClient.exe+7D292E | 0x7FF69DD4292E` dispatch → `TClient.exe+6C950 | 0x7FF69D5DC950` serialized action/event handler → operation ID read near `TClient.exe+6C9A8 | 0x7FF69D5DC9A8` → `TClient.exe+A61090 | 0x7FF69DFD1090` template lookup → operation ID written to entity state near `TClient.exe+6CDCF | 0x7FF69D5DCDCF` → `TClient.exe+6CE46 | 0x7FF69D5DCE46` calls `TClient.exe+7F09B0 | 0x7FF69DD609B0` → wrapper → `TClient.exe+7F06E0 | 0x7FF69DD606E0` → `TClient.exe+7F0900 | 0x7FF69DD60900` argument preparation → `TClient.exe+7F091D | 0x7FF69DD6091D` calls `TClient.exe+7BB160 | 0x7FF69DD2B160` → producer chain `TClient.exe+7BB4B7 | 0x7FF69DD2B4B7` calls `TClient.exe+99240 | 0x7FF69D609240`.

## `ENTITY+0x258` SOURCE RECOVERY CHAIN

Recover the descriptor-source side independently from the outgoing `entity+0x660` path:

```text
registry/manager tree at +0xF0
  -> TClient.exe+9F4CA0 | 0x7FF69DF64CA0
  -> configuration record
  -> configuration tree at configRecord+0x58
  -> TClient.exe+A08CE0 | 0x7FF69DF78CE0
  -> insertion at TClient.exe+A0930A | 0x7FF69DF7930A
  -> ordered tree at entity+0x258, count at entity+0x260
  -> TClient.exe+865AD0 | 0x7FF69DDD5AD0
  -> source-record +0x24 admission at TClient.exe+865BF8 | 0x7FF69DDD5BF8
  -> allocated descriptor appended to entity+0x688
```

At `TClient.exe+865BC7 | 0x7FF69DDD5BC7`, the builder loads the tree header/sentinel. `TClient.exe+865BCE | 0x7FF69DDD5BCE` loads the first node, and the successor traversal ends at the sentinel comparison at `TClient.exe+865DBF | 0x7FF69DDD5DBF`. Each 0x30-byte node stores its dword key at `+0x20` and source-record pointer at `+0x28`.

For recovery after a target update, confirm this structure and producer before interpreting `entity+0x688` counts. The current source is configuration/effect data; no candidate actor, range predicate, or actor-category predicate exists in this chain. Do not use `entity+0x688` as a target insertion point.

## LINKED 0x020A PATH

State activation starts at `TClient.exe+7A9740 | 0x7FF69DD19740`. The linked record is reached through approximately `[entity+0x12E0]`, then linked record `+0x20`. The linked operation word was observed as `0x020A`. Relevant locations are `TClient.exe+7A97BD | 0x7FF69DD197BD`, `TClient.exe+7A97C9 | 0x7FF69DD197C9`, `TClient.exe+7A97D2 | 0x7FF69DD197D2`, `TClient.exe+7A97DE | 0x7FF69DD197DE`, and `TClient.exe+7A9828 | 0x7FF69DD19828`, followed by the same wrapper/shared-worker system. `0x020A` is a separate template object and is not produced by incrementing `0x0209`.

## PERIODIC UPDATE PATH

The periodic updater is `TClient.exe+79C980 | 0x7FF69DD0C980`. State validation occurs near `TClient.exe+79CEF1 | 0x7FF69DD0CEF1`, delta accumulation near `TClient.exe+79CF1A | 0x7FF69DD0CF1A`, period comparison near `TClient.exe+79CF40 | 0x7FF69DD0CF40`, period subtraction near `TClient.exe+79CF52 | 0x7FF69DD0CF52`, template lookup near `TClient.exe+79CF63 | 0x7FF69DD0CF63`, and shared execution near `TClient.exe+79CFBA | 0x7FF69DD0CFBA`. The observed period is derived from linked-record state, including linked record field `+0x60`.

## WORKING INITIAL NX MECHANISM

The manager lets the original `0x0209` call execute, stops at `TClient.exe+7F0922 | 0x7FF69DD60922`, validates the same thread, RSP, R12, R13, R14, R15, RSI, and RDI, and redirects that thread's RIP to `TClient.exe+7F0900 | 0x7FF69DD60900` while the configured total has not been reached. The game rebuilds its own arguments and naturally reaches `TClient.exe+7F091D | 0x7FF69DD6091D` again. No call arguments, operation IDs, packet data, list contents, gameplay fields, or code bytes are written. The selected N includes the original call, so the maximum redirect count is `N-1`.

## CRITICAL ANCHORS

| Semantic anchor | Address |
|---|---|
| Initial preparation | `TClient.exe+7F0900 | 0x7FF69DD60900` |
| Initial/shared callsite | `TClient.exe+7F091D | 0x7FF69DD6091D` |
| Shared-call return | `TClient.exe+7F0922 | 0x7FF69DD60922` |
| Shared worker | `TClient.exe+7BB160 | 0x7FF69DD2B160` |
| Typed actor resolver | `TClient.exe+7B9BD0 | 0x7FF69DD29BD0` |
| Normal target-record construction boundary | `TClient.exe+6CC60 | 0x7FF69D5DCC60` |
| Producer | `TClient.exe+99240 | 0x7FF69D609240` |
| Template lookup | `TClient.exe+A61090 | 0x7FF69DFD1090` |
| Descriptor-source producer | `TClient.exe+A08CE0 | 0x7FF69DF78CE0` |
| Source-tree insertion | `TClient.exe+A0930A | 0x7FF69DF7930A` |
| Descriptor builder | `TClient.exe+865AD0 | 0x7FF69DDD5AD0` |
| Source-record admission | `TClient.exe+865BF8 | 0x7FF69DDD5BF8` |
| Wrapper | `TClient.exe+7F09B0 | 0x7FF69DD609B0` |
| Initial handler | `TClient.exe+6C950 | 0x7FF69D5DC950` |
| Dispatch | `TClient.exe+7D292E | 0x7FF69DD4292E` |
| Linked activation | `TClient.exe+7A9740 | 0x7FF69DD19740` |
| Periodic updater | `TClient.exe+79C980 | 0x7FF69DD0C980` |

## KNOWN NEGATIVE / DEAD-END EXPERIMENTS

Changing candidate scheduler immediates at `TClient.exe+853C43 | 0x7FF69DDC3C43`, `TClient.exe+8540E7 | 0x7FF69DDC40E7`, and `TClient.exe+8540F2 | 0x7FF69DDC40F2` from 1000 to 100 produced verified writes and restoration, but the observed `0x020A` producer remained approximately 997 ms periodic. Later caller tracing proved `TClient.exe+854049 | 0x7FF69DDC4049` was not the runtime caller producing the observed sequence. Do not present the old `TClient.exe+853B10 | 0x7FF69DDC3B10` scheduler hypothesis as the real AOE frequency path.

## PATCH RECOVERY PROCEDURE

1. Capture normal AOE behavior again.
2. Identify initial `0x0209` and linked/periodic `0x020A` operations.
3. Find the equivalent typed operation/message producer.
4. Find the equivalent shared worker.
5. Trace the shared worker and read its real runtime return address.
6. Determine the shared caller used by both operations.
7. Trace template/operation argument provenance backward.
8. Recover template lookup and the initial serialized action path.
9. Recover linked state activation and the periodic updater.
10. Identify the safest argument-preparation block and return point.
11. Validate an exactly-2x replay first.
12. Generalize to Initial Nx only after 2x succeeds.

Do not guess addresses when a signature or profile fails. Preserve relationship evidence and derive a new profile from the patched binary and fresh runtime observations.

## CURRENT BUILD LIMITATIONS

Only the listed SHA-256 profile is supported. Signature records are scaffolding marked `pending-analysis`; they do not resolve unknown builds. Producer counts establish client execution/serialization attempts, not independent server-accepted damage. The manager never casts AOE automatically.

## 1.1.7 Initial Nx reconfiguration

After a completed Nx cast, edit **Initial Calls** directly. The completed result remains visible as **Last Nx result**, while the selected value becomes **Next Nx**. Same-session live validation stays valid. With Auto Arm enabled, the new target arms for the next cast after experiment-only breakpoint reset; with Auto Arm disabled it remains ready for manual ARM. See [NX_EXPERIMENT_LIFECYCLE.md](NX_EXPERIMENT_LIFECYCLE.md).

## 1.1.5 differential capture

Use the Research page's AOE Target Differential Inspector for Mob A/B/C comparisons. It captures only the first 0x0209 after each marker and keeps 0x020A validation events separate. See [TARGET_DIFFERENTIAL_RESEARCH.md](TARGET_DIFFERENTIAL_RESEARCH.md).

## 1.1.8 sequential differential markers

One Inspector session now owns four explicit slots: A, B, C, and optional D. **MARK A** arms only A; the first valid `0x0209` stores A and immediately makes **MARK B** available. Periodic `0x020A` observations never hold the next marker disabled. **CANCEL CURRENT MARK** returns only the armed slot to empty and preserves every completed slot. See [DIFFERENTIAL_INSPECTOR_MARKER_LIFECYCLE.md](DIFFERENTIAL_INSPECTOR_MARKER_LIFECYCLE.md).


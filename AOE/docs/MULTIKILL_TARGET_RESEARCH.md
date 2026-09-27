# Multikill Target Research

## New build status — 2026-09-20

The 2026-09-20 `FB13C125...` profile relocates the read-only path to shared call `+0x7F277D`, worker `+0x7BC4C0`, producer call `+0x7BC817`, typed resolver `+0x7BAF60`, and producer `+0x99150`. Layout semantics remain statically confirmed. Multikill behavior is unchanged and was not tested or modified during recovery. See [PATCH_RECOVERY_2026-09-20.md](PATCH_RECOVERY_2026-09-20.md).

The previous `D0ECBBA1...` recovery remains documented in [PATCH_RECOVERY_2026-09-18.md](PATCH_RECOVERY_2026-09-18.md). Its profile and old profile remain supported.

The historical conclusions below remain the old-build baseline.

Status: static analysis and read-only observation support only. Multikill is not implemented.

Known target SHA-256: `CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E`

Reference module base: `0x7FF69D570000`.

## Current conclusion

The `entity+0x688` branch is closed for direct target selection. Its entries are allocated configuration/effect descriptors, and its source is an ordered configuration tree at `entity+0x258`. The old 1/3/4 count correlation does not measure nearby monsters. `TClient.exe+A0930A | 0x7FF69DF7930A` remains configuration provenance and is no longer a Multikill research anchor.

The active path is `entity+0x660`. Static analysis proves:

- `entity+0x660`, `+0x668`, and `+0x670` are begin, end, and capacity pointers.
- Each vector slot is an 8-byte pointer to a separately allocated 8-byte record.
- Record `+0x00` is a 32-bit live-object ID and `+0x04` is an 8-bit object-type namespace. Bytes `+0x05..+0x07` are uninitialized/unused by all proven code.
- `TClient.exe+7B9BD0 | 0x7FF69DD29BD0` resolves the pair through type-partitioned live-object trees and returns an actor pointer.
- Type 2 uses the context `+0x1120` tree and is `CTClientMonster`.
- `TClient.exe+7BB160 | 0x7FF69DD2B160` resolves and validates each actor record, removes rejected records, and passes survivors to the producer.
- `TClient.exe+99240 | 0x7FF69D609240` serializes the same dword ID and byte type unchanged.

The full proof is in [ENTITY_660_TARGET_ANALYSIS.md](ENTITY_660_TARGET_ANALYSIS.md). The eliminated descriptor branch remains documented in [ENTITY_688_STATIC_ANALYSIS.md](ENTITY_688_STATIC_ANALYSIS.md) and [AOE_SOURCE_COLLECTION_ANALYSIS.md](AOE_SOURCE_COLLECTION_ANALYSIS.md).

## Proven normal path

```text
TClient.exe+7D292E | 0x7FF69DD4292E
  -> TClient.exe+6C950 | 0x7FF69D5DC950
  -> parse serialized target ID/type at
     TClient.exe+6CC0F | 0x7FF69D5DCC0F and
     TClient.exe+6CC1B | 0x7FF69D5DCC1B
  -> allocate and append 8-byte record at
     TClient.exe+6CC31 | 0x7FF69D5DCC31 through
     TClient.exe+6CC7F | 0x7FF69D5DCC7F
  -> TClient.exe+6CE46 | 0x7FF69D5DCE46
  -> TClient.exe+7F09B0 | 0x7FF69DD609B0
  -> TClient.exe+7F091D | 0x7FF69DD6091D
  -> TClient.exe+7BB160 | 0x7FF69DD2B160
  -> TClient.exe+7BB4B7 | 0x7FF69DD2B4B7
  -> TClient.exe+99240 | 0x7FF69D609240
```

At the shared callsite, static data flow proves `R12 == RDX+0x660`. The caller’s count check at `TClient.exe+7F073F | 0x7FF69DD6073F` through `TClient.exe+7F0750 | 0x7FF69DD60750` limits the vector to 32 entries.

## What the normal one-entry record proves

One valid record corresponds to one live actor. It is not a list of all actors damaged by the AOE. The observed initial producer count of one is the same one actor identity serialized by `TClient.exe+99240 | 0x7FF69D609240`; the periodic `0x020A` producer count of zero means no actor identity record is serialized in that vector.

Among the proposed models, Model B is best supported: the client sends one actor anchor and the remaining area effect is determined elsewhere in skill/server logic. Model A is inconsistent with normal count one, and Model C is inconsistent with the typed actor lookup and gameplay validation. Confidence is high for “one actor handle,” medium for “primary/anchor target,” and currently insufficient for “the exact actor selected in the UI.”

## Why runtime comparison remains necessary

The normal writer receives the ID/type pair from a serialized action/event target-list field. The first header pair in the same handler resolves an event-subject actor, but that is a distinct input field. No static chain from controller/current-selection storage into the target-list record was verified.

The 1.1.3 **AOE Primary Target Inspector (READ ONLY)** captures only the proven `entity+0x660` vector at `TClient.exe+7F091D | 0x7FF69DD6091D`. It does not scan nearby R12 offsets. For captures A through D it exports record count, raw 8 bytes, decoded ID/type, resolved actor pointer, and changes from capture A. It uses guarded reads to reproduce `TClient.exe+7B9BD0 | 0x7FF69DD29BD0`; it performs no gameplay write and no RIP redirect.

Suggested observation sequence:

1. Select Mob A, mark Capture A, and cast one normal AOE.
2. Select Mob B, mark Capture B, and cast one normal AOE.
3. Select Mob C, mark Capture C, and cast one normal AOE.
4. If possible, clear the selected target, mark Capture D, and cast once.
5. Stop and export JSON. Initial Nx must remain inactive during this comparison.

If IDs and resolved actor pointers change with A/B/C and D becomes empty or changes consistently, that supplies the missing selected-target evidence. Until then, the documentation does not claim equivalence.

## One future controlled-experiment point

The one preferred future point is `TClient.exe+6CC60 | 0x7FF69D5DCC60`, exact instruction `lea rcx,[rdi+0000000000000660h]`. RAX is the completed 8-byte actor identity record immediately before append. A future two-target experiment would present two separately validated actor identity records at this construction boundary. No patch or target modification was made.

## 1.1.5 target differential mode

The old +0x660 hypothesis was contradicted by live evidence: ID 9534/type 1 resolved to the owner. The read-only AOE Target Differential Inspector now compares the first initial 0x0209 after A/B/C markers at +7F195D and a bounded parse snapshot at +6CC93. Periodic 0x020A events are stored separately. Full field and workflow details are in [TARGET_DIFFERENTIAL_RESEARCH.md](TARGET_DIFFERENTIAL_RESEARCH.md).

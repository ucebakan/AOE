# entity+0x660 Target Record Analysis

## New build status — 2026-09-18

The new `D0ECBBA1...` build independently reproduces this layout: vector header at owner `+0x660/+0x668/+0x670`, pointers to 8-byte records, uint32 ID at record `+0`, uint8 namespace at `+4`, and a 32-entry caller bound. The relocated typed resolver is `+0x7BA720`; type 2 still uses context `+0x1120`; actor ID/category remain `+0x768/+0x7E1`. Construction at `+0x6CC60`, shared filtering at `+0x7BBDE0`, and serialization at `+0x993DC/+0x993E6` provide independent producer/consumer agreement. Runtime record and selected-target observations remain pending. See [PATCH_RECOVERY_2026-09-18.md](PATCH_RECOVERY_2026-09-18.md).

The original analysis below describes the old `CD772F3F...` build unless a section says otherwise.

Status: static analysis complete for layout, construction, typed actor lookup, filtering, and serialization. The relation to the UI-selected target still requires the read-only A–D comparison described below. No target value was changed.

Known target SHA-256: `CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E`

Reference module base for every absolute address in this document: `0x7FF69D570000`.

## Result

`entity+0x660` is a Microsoft-style vector whose elements are 8-byte pointers. Each pointer addresses a separately allocated 8-byte identity record. A readable record contains a 32-bit live-object ID at `+0x00` and an 8-bit object-type namespace at `+0x04`. Bytes `+0x05..+0x07` have no proven semantics: constructors do not initialize them and all identified consumers ignore them.

The pair `(record.id, record.type)` is passed to `TClient.exe+7B9BD0 | 0x7FF69DD29BD0`, which dispatches by type and performs a dword-key lookup in a type-partitioned live-object tree. A successful node returns its actor pointer from node `+0x28`. Type `2` resolves through the monster tree at context `+0x1120` and is statically tied to `CTClientMonster`.

This proves that one valid record identifies one live actor object. It does not yet prove that the normal one-entry record is the same actor currently selected in the UI. The normal builder obtains the pair from a serialized action/event target-list field; no verified data flow reaches it from a local controller target pointer. Version 1.1.3 therefore adds the read-only **AOE Primary Target Inspector** for that final comparison.

## Exact vector layout and ownership

| Entity field | Structure | Evidence |
|---|---|---|
| `entity+0x660` | begin pointer | initialized to zero at `TClient.exe+85077F | 0x7FF69DDC077F` |
| `entity+0x668` | end pointer | initialized to zero at `TClient.exe+850786 | 0x7FF69DDC0786` |
| `entity+0x670` | capacity pointer | initialized to zero at `TClient.exe+85078D | 0x7FF69DDC078D` |
| `end-begin` | used bytes | every consumer divides this span by 8 |
| vector slot | one qword | points to a separately allocated 8-byte record |

The common growth path calls `TClient.exe+1B750 | 0x7FF69D58B750` with RCX pointing at the three-qword vector header and R8 pointing at the new qword value. The normal builder’s fast path stores the record pointer at `[end]` and adds 8 to `end`; its full-capacity path calls this helper at `TClient.exe+6CC7F | 0x7FF69D5DCC7F`.

`TClient.exe+8595C0 | 0x7FF69DDC95C0` is the owning clear operation. It reads end at `TClient.exe+8595CF | 0x7FF69DDC95CF` and begin at `TClient.exe+8595DE | 0x7FF69DDC95DE`, computes the number of qword slots, frees each non-null pointee as an 8-byte allocation at `TClient.exe+859600 | 0x7FF69DDC9600` through `TClient.exe+859609 | 0x7FF69DDC9609`, zeros the slot at `TClient.exe+859615 | 0x7FF69DDC9615`, and finally assigns `end=begin` at `TClient.exe+85963E | 0x7FF69DDC963E`.

The state-reset block at `TClient.exe+850C21 | 0x7FF69DDC0C21` through `TClient.exe+850C31 | 0x7FF69DDC0C31` also assigns `end=begin`; this block alone is not the owning element destructor. The enclosing object destructor passes `&entity+0x660` to vector-storage cleanup `TClient.exe+1CBE0 | 0x7FF69D58CBE0` at `TClient.exe+851D79 | 0x7FF69DDC1D79` after element cleanup.

The initial wrapper validates `([begin+8]-[begin])/8 <= 0x20` at `TClient.exe+7F073F | 0x7FF69DD6073F` through `TClient.exe+7F0750 | 0x7FF69DD60750`. The proven callsite limit is therefore 32 records. The worker also has a separate defensive limit of 64 at `TClient.exe+7BB298 | 0x7FF69DD2B298`; this does not raise the caller’s 32-record limit.

## Exact 8-byte record

| Offset | Width | Proven interpretation |
|---|---:|---|
| `R+0x00` | 4 | unsigned live-object/actor ID |
| `R+0x04` | 1 | object-type namespace used by `+7B9BD0` |
| `R+0x05` | 1 | uninitialized/unused by every identified constructor and consumer |
| `R+0x06` | 1 | uninitialized/unused by every identified constructor and consumer |
| `R+0x07` | 1 | uninitialized/unused by every identified constructor and consumer |

The normal writer allocates exactly 8 bytes at `TClient.exe+6CC31 | 0x7FF69D5DCC31` through `TClient.exe+6CC36 | 0x7FF69D5DCC36`. It writes the parsed dword at `TClient.exe+6CC57 | 0x7FF69D5DCC57` after loading it at `TClient.exe+6CC54 | 0x7FF69D5DCC54`, then writes the parsed byte at `TClient.exe+6CC5D | 0x7FF69D5DCC5D`. It never writes bytes 5–7.

The self-fallback builder at `TClient.exe+82668 | 0x7FF69D5F2668` allocates 8 bytes at `TClient.exe+82678 | 0x7FF69D5F2678`, copies `entity+0x768` to record `+0x00` at `TClient.exe+8269E | 0x7FF69D5F269E`, copies `entity+0x7E1` to record `+0x04` at `TClient.exe+826A6 | 0x7FF69D5F26A6`, and appends it at `TClient.exe+826B0 | 0x7FF69D5F26B0` through `TClient.exe+826CB | 0x7FF69D5F26CB`. This is direct structural proof that the two record fields are the same identity pair stored on an actor object.

## Meaningful writers and accessors

Four append sites construct this exact record type:

- Normal action/event handler: `TClient.exe+6CC31 | 0x7FF69D5DCC31` through `TClient.exe+6CC7F | 0x7FF69D5DCC7F`.
- Serialized handler: `TClient.exe+82197 | 0x7FF69D5F2197` through `TClient.exe+821F3 | 0x7FF69D5F21F3`; its ID/type parsers are at `TClient.exe+82172 | 0x7FF69D5F2172` and `TClient.exe+8217F | 0x7FF69D5F217F`.
- Empty-vector self fallback: `TClient.exe+82678 | 0x7FF69D5F2678` through `TClient.exe+826CB | 0x7FF69D5F26CB`.
- Serialized handler: `TClient.exe+828EE | 0x7FF69D5F28EE` through `TClient.exe+82949 | 0x7FF69D5F2949`; its ID/type parsers are at `TClient.exe+828CA | 0x7FF69D5F28CA` and `TClient.exe+828D7 | 0x7FF69D5F28D7`.

Meaningful consumers include:

- `TClient.exe+79CFA2 | 0x7FF69DD0CFA2`, `TClient.exe+7D0D6C | 0x7FF69DD40D6C`, and `TClient.exe+7F0A18 | 0x7FF69DD60A18`, which pass `&entity+0x660` toward the shared worker.
- `TClient.exe+7AF235 | 0x7FF69DD1F235` through `TClient.exe+7AF267 | 0x7FF69DD1F267`, which iterate the vector, require record type 2 at `TClient.exe+7AF25B | 0x7FF69DD1F25B`, require a nonzero ID at `TClient.exe+7AF261 | 0x7FF69DD1F261`, and search the context `+0x1120` monster tree directly.
- `TClient.exe+7EFC85 | 0x7FF69DD5FC85` and `TClient.exe+7F0219 | 0x7FF69DD60219`, which iterate it on helper paths.
- `TClient.exe+85A2EF | 0x7FF69DDCA2EF`, which tests whether it is nonempty before invoking `TClient.exe+7D0C30 | 0x7FF69DD40C30`.

## Normal builder and source provenance

The normal direct chain begins when `TClient.exe+7D292E | 0x7FF69DD4292E` calls the serialized action/event handler `TClient.exe+6C950 | 0x7FF69D5DC950` with the stream in R8.

The handler first parses an event-subject identity pair at `TClient.exe+6C989 | 0x7FF69D5DC989` and `TClient.exe+6C99C | 0x7FF69D5DC99C`, then resolves that distinct header pair through `TClient.exe+7B9BD0 | 0x7FF69DD29BD0` at `TClient.exe+6C9C4 | 0x7FF69D5DC9C4`. This yields an event-subject actor pointer, but it is not proof that any later target-list entry is the same actor.

The handler parses a target-record count at `TClient.exe+6CB3E | 0x7FF69D5DCB3E`, caps it to `0x40` at `TClient.exe+6CBD1 | 0x7FF69D5DCBD1` through `TClient.exe+6CBDC | 0x7FF69D5DCBDC`, clears the old `+0x660` entries at `TClient.exe+6CBC4 | 0x7FF69D5DCBC4`, and loops over serialized pairs. Each target ID is parsed at `TClient.exe+6CC0F | 0x7FF69D5DCC0F`, each type byte at `TClient.exe+6CC1B | 0x7FF69D5DCC1B`, and zero values are skipped at `TClient.exe+6CC25 | 0x7FF69D5DCC25` through `TClient.exe+6CC2F | 0x7FF69D5DCC2F`. The record is then allocated and appended.

The completed action invokes the normal AOE wrapper at `TClient.exe+6CE46 | 0x7FF69D5DCE46`, which reaches `TClient.exe+7F09B0 | 0x7FF69DD609B0`, `TClient.exe+7F091D | 0x7FF69DD6091D`, and `TClient.exe+7BB160 | 0x7FF69DD2B160`.

Therefore, the normal target record originates in a serialized action/event target-list field. The first proven actor pointer for that specific target record appears when the pair is resolved during consumption in `TClient.exe+7BB160 | 0x7FF69DD2B160`. Static analysis did not find a controller/current-selection pointer feeding the builder.

## `TClient.exe+7B9BD0 | 0x7FF69DD29BD0` typed live-object resolver

`TClient.exe+7B9BD0 | 0x7FF69DD29BD0` has the effective signature:

```cpp
ActorObject* ResolveTypedObject(
    ClientContext* context, // RCX
    uint32_t id,            // EDX
    uint8_t type);          // R8B
```

It rejects ID zero, converts type to a zero-based index, rejects types outside 1–19, and dispatches through a jump table. Supported cases are:

| Type | Resolver | Context source |
|---:|---|---|
| 1 | `TClient.exe+7B9E70 | 0x7FF69DD29E70` | first compare local actor at context `+0x2710` using actor ID `+0x768`; otherwise tree header at context `+0x1140` |
| 2 | `TClient.exe+7B9B80 | 0x7FF69DD29B80` | tree header at context `+0x1120` |
| 7 | `TClient.exe+7B9F60 | 0x7FF69DD29F60` | tree header at context `+0x11B0` |
| 9 | `TClient.exe+7BAE30 | 0x7FF69DD2AE30` | tree header at context `+0x1130` |
| 10 | `TClient.exe+7B98F0 | 0x7FF69DD298F0` | tree header at context `+0xC6FF00` |
| 11 | `TClient.exe+7B98A0 | 0x7FF69DD298A0` | tree header at context `+0x11A0` |
| 17 | `TClient.exe+7BADE0 | 0x7FF69DD2ADE0` | tree header at context `+0x1150` |
| 19 | `TClient.exe+7BAD00 | 0x7FF69DD2AD00` | tree header at context `+0x11E0` |

Types 3–6, 8, 12–16, and 18 return null in this dispatcher.

Each supported helper performs the same ordered-tree lower-bound search. The header/sentinel’s `+0x08` points to the root. A node contains a left link at `+0x00`, right link at `+0x10`, sentinel flag at `+0x19`, dword ID key at `+0x20`, and object pointer at `+0x28`. Equality is required before the object pointer is returned. This is a typed live-object registry lookup, not a template/effect lookup.

The trees are iterable, but categories are partitioned. Players, monsters, recalls/pets, and other supported namespaces do not coexist in one universal container. Any future loaded-object inventory would need to respect those partitions and the type-1 local-actor fast path. Version 1.1.3 does not enumerate them.

## Existing actor-category discriminator

The existing game discriminator is byte `actor+0x7E1`, not a new vtable heuristic:

- Type 1: `CTClientChar`; constructor `TClient.exe+7803B0 | 0x7FF69DCF03B0` stores type 1 at `TClient.exe+78050E | 0x7FF69DCF050E`.
- Type 2: `CTClientMonster`; constructor `TClient.exe+83D600 | 0x7FF69DDAD600` stores type 2 at `TClient.exe+83D696 | 0x7FF69DDAD696`.
- Type 3: `CTClientNpc`; constructor `TClient.exe+84E050 | 0x7FF69DDBE050` stores type 3 at `TClient.exe+84E120 | 0x7FF69DDBE120`, although `+7B9BD0` has no type-3 case.
- Type 7: `CTClientRecall` base/`CTClientPet` namespace; base constructor `TClient.exe+86F470 | 0x7FF69DDDF470` stores type 7 at `TClient.exe+86F50D | 0x7FF69DDDF50D`, and the `CTClientPet` constructor at `TClient.exe+86D500 | 0x7FF69DDDD500` retains it.
- Type 11: another `CTClientRecall` namespace. The handler at `TClient.exe+46336 | 0x7FF69D5B6336` constructs the recall base then writes type 11 at `TClient.exe+46388 | 0x7FF69D5B6388`; the exact gameplay subtype name is not proven.

The existing direct consumer at `TClient.exe+7AF25B | 0x7FF69DD1F25B` uses record type 2 and then searches the context `+0x1120` container. A safe monster-only predicate is therefore the game’s own type value 2 together with successful resolution in that type-2 container.

## `TClient.exe+7BB160 | 0x7FF69DD2B160` filtering

At `TClient.exe+7BB160 | 0x7FF69DD2B160`, RCX is the client context, RDX is the owner/entity, R8 is a position pointer, R9 is an operation/template object, and stack argument 5 is the `+0x660` vector pointer.

The worker first requires the key arguments and context local actor to be non-null at `TClient.exe+7BB1B2 | 0x7FF69DD2B1B2` through `TClient.exe+7BB1D5 | 0x7FF69DD2B1D5`. It compares the local actor ID with the owner’s virtual identity at `TClient.exe+7BB1DB | 0x7FF69DD2B1DB` through `TClient.exe+7BB1FE | 0x7FF69DD2B1FE`; mismatch exits. A type-1 owner is canonicalized through the type-1 resolver at `TClient.exe+7BB236 | 0x7FF69DD2B236` through `TClient.exe+7BB25E | 0x7FF69DD2B25E`.

The vector count is calculated at `TClient.exe+7BB266 | 0x7FF69DD2B266` through `TClient.exe+7BB27B | 0x7FF69DD2B27B`. Each rejected record is removed in place: the 8-byte pointee is freed at `TClient.exe+7BB311 | 0x7FF69DD2B311` through `TClient.exe+7BB31A | 0x7FF69DD2B31A`, later qword slots are shifted at `TClient.exe+7BB323 | 0x7FF69DD2B323` through `TClient.exe+7BB332 | 0x7FF69DD2B332`, and end is reduced by 8 at `TClient.exe+7BB337 | 0x7FF69DD2B337`.

The verified rejection conditions are:

| Check | Address | Proven meaning |
|---|---|---|
| index reaches 64 | `TClient.exe+7BB298 | 0x7FF69DD2B298` | defensive worker cap |
| null record pointer | `TClient.exe+7BB29E | 0x7FF69DD2B29E` through `TClient.exe+7BB2A5 | 0x7FF69DD2B2A5` | invalid slot |
| `R+0x00 == 0` | `TClient.exe+7BB2A7 | 0x7FF69DD2B2A7` through `TClient.exe+7BB2AB | 0x7FF69DD2B2AB` | invalid ID |
| `R+0x04 == 0` | `TClient.exe+7BB2AD | 0x7FF69DD2B2AD` through `TClient.exe+7BB2B5 | 0x7FF69DD2B2B5` | invalid type |
| `+7B9BD0` returns null | `TClient.exe+7BB2BA | 0x7FF69DD2B2BA` through `TClient.exe+7BB2C5 | 0x7FF69DD2B2C5` | unsupported, missing, or stale typed ID |
| reference owner is null | `TClient.exe+7BB2C7 | 0x7FF69DD2B2C7` through `TClient.exe+7BB2CA | 0x7FF69DD2B2CA` | validation context missing |
| resolved actor byte `+0x7CA == 0` | `TClient.exe+7BB2CF | 0x7FF69DD2B2CF` through `TClient.exe+7BB2D6 | 0x7FF69DD2B2D6` | lifecycle/eligibility flag is clear; “dead” is not statically proven |
| `+7AC960` returns nonzero | `TClient.exe+7BB2D8 | 0x7FF69DD2B2D8` through `TClient.exe+7BB2EB | 0x7FF69DD2B2EB` | relationship/skill-target validation failed |
| candidate differs from reference and low byte at actor `+0x7E4` is nonzero | `TClient.exe+7BB2F0 | 0x7FF69DD2B2F0` through `TClient.exe+7BB2FB | 0x7FF69DD2B2FB` | status flag must be zero; exact gameplay label is not proven |

`TClient.exe+7AC960 | 0x7FF69DD1C960` is a gameplay target/relationship validator. It admits actor types 1, 2, 7, and 11 into its deeper logic, checks owner/candidate relationships and operation/template options, and returns zero on success. Its nonzero result must not be reduced to a single guessed label such as distance or network validity.

## Field-for-field serialization

The filtered vector is placed on the stack at `TClient.exe+7BB49D | 0x7FF69DD2B49D`, and `TClient.exe+7BB4B7 | 0x7FF69DD2B4B7` directly calls `TClient.exe+99240 | 0x7FF69D609240`.

The producer loads that same vector pointer at `TClient.exe+9928E | 0x7FF69D60928E`. It obtains begin and end at `TClient.exe+99296 | 0x7FF69D609296` and `TClient.exe+99299 | 0x7FF69D609299`, divides the span by 8 at `TClient.exe+9929D | 0x7FF69D60929D` through `TClient.exe+992A0 | 0x7FF69D6092A0`, and counts records having nonzero ID and type in the loop at `TClient.exe+992B0 | 0x7FF69D6092B0` through `TClient.exe+992D5 | 0x7FF69D6092D5`.

The count byte is serialized at `TClient.exe+9939E | 0x7FF69D60939E` through `TClient.exe+993A1 | 0x7FF69D6093A1`. The record loop begins at `TClient.exe+993AD | 0x7FF69D6093AD`, loads each record pointer at `TClient.exe+993C5 | 0x7FF69D6093C5`, serializes the unchanged dword ID at `TClient.exe+993DC | 0x7FF69D6093DC` through `TClient.exe+993E1 | 0x7FF69D6093E1`, and serializes the unchanged type byte at `TClient.exe+993E6 | 0x7FF69D6093E6` through `TClient.exe+993EC | 0x7FF69D6093EC`. It then calls `TClient.exe+A12A60 | 0x7FF69DF82A60` at `TClient.exe+99474 | 0x7FF69D609474`; the downstream send layer includes `TClient.exe+A129F0 | 0x7FF69DF829F0`.

There is no ID/type transformation between `entity+0x660` and serialization. The observed producer structural count of one for initial `0x0209` therefore represents exactly one serialized `(actorId,type)` pair. The periodic `0x020A` structural count of zero represents no such pair.

## Normal AOE model

- **Model A, every damaged AOE target:** rejected by the observed normal count of one while multiple nearby actors receive the effect.
- **Model C, non-target metadata:** rejected because the pair resolves to a live actor and is passed through gameplay target validation.
- **Model B, one primary/anchor actor plus separate skill/server area logic:** best supported. Confidence is high that the record is one actor handle and medium that its role is the primary/anchor target. Static analysis alone does not prove that this is the exact actor clicked in the UI.

## Read-only runtime comparison

The 1.1.3 inspector stops generic R12 scanning. At `TClient.exe+7F091D | 0x7FF69DD6091D` on an initial `0x0209`, it requires `R12 == RDX+0x660`, reads only that vector, bounds capture to 32 records, keeps all raw 8 bytes, decodes ID/type, and reproduces `TClient.exe+7B9BD0 | 0x7FF69DD29BD0` as guarded `ReadProcessMemory` tree traversal. It never invokes target code.

Use captures A, B, and C after selecting three different mobs, and capture D with no valid selection if possible. The UI and JSON compare count, raw bytes, ID, type, resolved actor pointer, and each field’s change from A. This is the remaining evidence needed to say whether the record equals the UI-selected mob.

## One future controlled-experiment point

The one preferred future point is `TClient.exe+6CC60 | 0x7FF69D5DCC60`, exact instruction `lea rcx,[rdi+0000000000000660h]`. At this point RAX is the fully constructed 8-byte record pointer: `[RAX]` is the parsed actor ID and `[RAX+4]` is the parsed type, immediately before the vector append. A future two-target experiment would change the append input at this construction boundary so that two separately validated actor identity records are presented to the existing vector logic. No patch or target change was made in this work.

## 2026-09-18 live correction

On the current D0ECBBA1 build, the initial 0x0209 capture contained one owner+0x660 record: actor ID 9534, type 1, resolving to the RDX owner itself. The collection is treated as caster/reference evidence, not selected-mob evidence. See [TARGET_DIFFERENTIAL_RESEARCH.md](TARGET_DIFFERENTIAL_RESEARCH.md).

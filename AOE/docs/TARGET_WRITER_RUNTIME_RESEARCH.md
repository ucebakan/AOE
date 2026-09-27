# Target Writer Runtime Research — 1.1.12-research

Target binary SHA-256: `FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7`.

## REJECTED live assumption

`CTClientGame+0x2318` failed the existing selected-target validation. It was NULL with (1) a monster selected, (2) that monster after a normal single-target attack, and (3) another monster after a normal single-target attack. It is therefore an **UNPROVEN static target-state candidate**, not the current selected target. No target manipulation or Multikill behavior uses it.

## STATIC writer context

Function `TClient.exe+7F62B0` saves its incoming `RCX` in `RBX` at `+7F62BD`. Static code alone does not prove the incoming object is the global game object. The runtime inspector therefore compares RBX to the qword at `moduleBase+E6C9C0` on every hit.

The relevant current-build instructions are:

```text
+7F63CF  mov dword ptr [rbx+C7AD48h],r8d
+7F63D6  mov word ptr [rbx+C7AD4Ch],r8w
+7F63DE  test edx,edx
+7F63E0  je +7F63F5
+7F63E2  movzx r8d,byte ptr [rbx+C7AD5Ch]
+7F63EA  mov rcx,rbx
+7F63ED  call +7BAF60
+7F63F2  mov r8,rax
+7F63F5  movsd xmm0,qword ptr [rbx+C7AD60h]
+7F63FD  movzx edx,di
+7F6400  mov eax,dword ptr [rbx+C7AD68h]
+7F6406  mov rcx,rbx
+7F6409  movzx r9d,byte ptr [rbx+C7AD4Fh]
+7F6411  mov qword ptr [rbx+2318h],r8
+7F6418  movzx r8d,byte ptr [rbx+C7AD4Eh]
+7F6420  movsd qword ptr [rbx+C7ACF0h],xmm0
+7F6428  mov dword ptr [rbx+C7ACF8h],eax
+7F642E  mov dword ptr [rsp+20h],esi
+7F6432  call +7FA100
```

On the nonzero `EDX` path, `+7F63ED` calls `+7BAF60` with `RCX=RBX`, the preexisting `EDX`, and `R8B=[RBX+C7AD5C]`; `+7F63F2` copies the return value from `RAX` to `R8`. On the zero branch, the resolver is skipped and the incoming/preexisting R8 reaches the store. These are data-flow facts; actor, selection, and class semantics remain candidates until runtime evidence supports them.

## STATIC other +0x2318 writers and clearers

- `+7E601A`: owner/base `RBX`; stores `RAX`. RAX begins as `[RBX+1E60]`. If `RDI` is non-null, `+7E6015` replaces RAX with the result of `+7C36A0(RBX, [RBX+1E60], RDI)`. This looks like a resolved target-state update, but the lifecycle meaning is unproven.
- `+7D7555`: owner/base `RSI`; copies `[RSI+1E60]` directly to `[RSI+2318]` under surrounding state checks.
- `+7D7581`: owner/base `RSI`; stores the return of `+7C36A0(RSI, [RSI+1E60], R14)`.
- `+7D759A`: owner/base `RSI`; retry path stores `+7C36A0(RSI, 0, R14)` after the first result was null. The resulting pointer is then supplied as `R8` to `+7A9580`. This supports a generic combat/action target-state role, not current click selection.
- `+7A9CB8`: owner/base `RBX`; conditionally stores zero from RSI, which was cleared at `+7A9C95`. It is part of a wider reset path.
- `+7F76E4` and `+7F79D5`: owner/base `RDI`; store zero from RBX in their surrounding reset/cleanup paths.

The writers and clearers show a shared transient state slot with several producers and reset routes. They do not distinguish selection, combat target, skill target, UI focus, or another generic target state without live timing and actor evidence.

## LIVE inspector implementation

The default mode installs exactly one hardware execution breakpoint per debugged thread at `moduleBase+0x7F6411`. It does not install reader/resolver breakpoints. A hit records timestamp, thread ID, RIP, RBX, R8, RDX, RCX, R9, RSP, the effective destination, the pre-store qword, the global storage qword, and RBX/global equality. The list is capped at 100 events and then reports `BUFFER FULL`.

The execution breakpoint fires before the store. Version 1.1.12 does not single-step or add a next-instruction breakpoint. It exports `expectedStoredValue=R8`, `postStoreObserved=false`, and does not claim an observed post-store value.

R8 validation uses guarded reads for actor ID `+0x768`, category `+0x7E1`, eligibility `+0x7CA`, and status `+0x7E4`. Category 2 position follows `actor+0x1390 -> link`, requires `[link+0x1380] == actor`, and reads XYZ at link `+0x70/+0x74/+0x78`. It never applies player `+0xB0` coordinates to monsters. When the global candidate is pointer-like, the typed category tree is also checked for exact pointer identity. No recursive pointer scan is performed because no small dereference relation between a differing RBX and the global value has been statically justified.

There are no writer observations bundled into a fresh build. Runtime results remain pending until the workflow below is performed.

## CANDIDATE conclusions

The analyzer reports only conservative states: `WRITER_NOT_OBSERVED`, `WRITER_OBSERVED_CONTEXT_UNKNOWN`, `WRITER_OBSERVED_NON_MONSTER_VALUE`, `WRITER_OBSERVED_MONSTER_VALUE`, `GLOBAL_OBJECT_MATCHED`, `GLOBAL_OBJECT_MISMATCHED`, `SELECTION_WRITER_CANDIDATE`, `COMBAT_TARGET_WRITER_CANDIDATE`, and `INSUFFICIENT_DATA`. Multiple distinct category-2 R8 values during `SELECTION` can create a selection-writer candidate. Attack-only observations can create a combat-target-writer candidate. One event never becomes `PROVEN_SELECTED_TARGET`.

## Read-only and mode safety

The mode performs guarded `ReadProcessMemory` reads and debugger hardware-breakpoint observation. It contains no `WriteProcessMemory`, `VirtualProtectEx`, code/data modification, actor or target modification, packet change, or RIP replay redirect. Starting it releases the current Initial Nx breakpoint state through the existing research transition, retains the validated attached session, and suppresses Auto Arm only for the active research session. Differential Inspector and Provenance mode are unavailable while it runs. Stopping restores debug-register state; saved Auto Arm configuration is unchanged.

## JSON

The `targetWriterRuntime` object contains writer RVA/instruction, read-only and breakpoint metadata, bounded events, registers, destination pre-store/expected/post-store fields, global readability/equality, validated R8 actor identity/category/eligibility/status/XYZ, and a summary with hit counts, global match/mismatch counts, classifications, and reason.

## Exact live workflow

1. Attach to TClient and wait for Profile `VALID`.
2. Open Research and press Target Writer `START`.
3. Set `SELECTION`; click Wolf A, Wolf B, and Wolf C.
4. Set `NORMAL ATTACK`; select/attack one Wolf, then another.
5. Optionally set `AOE CAST`; cast one normal AOE.
6. Press `STOP`, then `EXPORT JSON`.

No Initial Nx replay or target modification is involved. The live question is whether `+7F6411` runs in each labelled phase and what RBX/R8 actually contain.

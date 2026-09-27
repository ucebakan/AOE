# Selected Target Provenance Research — 1.1.12-research

Target binary: SHA-256 `FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7`.

## Static evidence

The reference client source names `CTClientGame::m_pTARGET` as the persistent selection field. `ResetTargetOBJ` changes it, target UI code reads it, and `ResetActOBJ` passes it through `GetSkillTarget` into the combat target.

The current x64 binary contains the following structurally similar candidate path. The failed live result prevents transferring the source-level name to this binary field:

- `TClient.exe+E6C9C0`: global `CTClientGame*`. The constructor writes the instance at `TClient.exe+78D2B7`.
- `incoming/global-correlated context+0x2318`: unproven target-state actor-pointer candidate.
- `TClient.exe+7F6411`: `mov qword ptr [rbx+0x2318],r8`. Here `RBX` is the function's incoming `RCX`, preserved at function entry; its game-object identity is not live-proven. When the incoming ID in `EDX` is nonzero, `+7F63ED` calls typed resolver `+7BAF60` with `RCX=RBX`, `EDX=actorId`, and `R8B=actorType`; `+7F63F2` moves the returned actor pointer into `R8` before the store. A zero-ID route reaches the same store without that resolver call and supplies the value already in `R8`.
- `TClient.exe+7E601A`, `TClient.exe+7D7581`, and `TClient.exe+7D759A`: other direct assignments from selection/pick helpers.
- `TClient.exe+7A9CB8`, `TClient.exe+7F76E4`, and `TClient.exe+7F79D5`: clear paths that store zero.
- `TClient.exe+799891`: `mov rax,qword ptr [rdi+0x2318]`, followed by a null check and a category read at actor `+0x7E1`; this is a UI/gameplay consumer.
- `TClient.exe+7D7C77`: `mov r8,qword ptr [rsi+0x2318]`. The following setup passes `RCX=RSI` (game), `RDX=[RSI+0x2710]` (local actor), `R8=selected candidate`, and `R9=R14` to direct call `TClient.exe+A13A0`.

This is a **static direct-pointer candidate**, not yet a runtime-proven selected-target field. The Inspector reads actor ID at `actor+0x768` and category/type at `actor+0x7E1`. It independently walks the profile's category-specific actor tree using the same ID/type semantics as `TClient.exe+7BAF60`; a capture validates only when that lookup returns the identical pointer. Only independent live A/B/C switching evidence can promote the candidate to `PROVEN_DIRECT_POINTER`.

## Live correction

The candidate failed manual live validation in three independent states: monster selected only, the same monster after a normal single-target attack, and another monster after a normal single-target attack. `[game+0x2318]` was NULL in all three. The field must not be called the current selected target. It remains an **UNPROVEN static target-state candidate**; the UI now says exactly this. Future A/B/C logic remains available only as a way to test whether new evidence can overturn the failed result.
## Runtime evidence

Runtime A/B/C/NONE evidence is intentionally empty in a new session. The UI cannot label the result `PROVEN_DIRECT_POINTER` until A, B, and C are three different readable category-2 actors with different IDs and pointers, and each resolves back to the same pointer through the typed tree. A `CAPTURE NONE` null value strengthens deselection evidence when the client supports clearing selection.

The basic capture path uses `ReadProcessMemory` only and installs zero additional execution breakpoints. Initial Nx and the Differential Inspector are mutually excluded while this mode is active. The saved Auto Arm preference is unchanged and is only suppressed for the active research session.

After A/B/C proof, the existing Differential Inspector can perform the temporal phase without adding another breakpoint: at MARK time it snapshots the same profiled field, and at the already-observed initial `0x0209` it reads `RCX+0x2318`, validates category 2 and typed-tree pointer identity, and exports both snapshots under `spatialCaptures[].markerTime.selectedTarget` and `initial0209Time.selectedTarget`. This compares identity directly; coordinates are supplementary and proximity never establishes identity.

## Monster XYZ

For a validated category-2 pointer, position follows the previously recovered accessors `TClient.exe+781D80`, `TClient.exe+781DB0`, and `TClient.exe+781DE0`:

1. read `actor+0x1390` as the optional world link;
2. require `link+0x1380 == actor`;
3. read X/Y/Z from the validated base at `+0x70/+0x74/+0x78`.

Player `+0xB0` coordinates are not assumed for monsters. Spatial proximity is never used as target identity.

## Consumer chain

The statically supported chain is:

`CTClientGame global +E6C9C0` → static `m_pTARGET` candidate `+0x2318` → combat reader `+7D7C77` → direct call `+A13A0`.

The later edge from that decision into the verified `+6C830` target/action parser and initial `0x0209` construction remains a candidate until runtime temporal correlation is collected. No unknown edge is presented as proven.

## parsedInputs diagnosis

`TClient.exe+6CB73` is a real reachable instruction: `cmp word ptr [r15+r13*2+0x3C], r14w`. The earlier implementation incorrectly treated `[RBP-0x40]` as an operation code and filtered the observation to `0x0209`. Static data flow shows `[RBP-0x40]` is a parsed skill/template word used by the table lookup at `+6C91D`; therefore valid events were discarded.

At `+6CB73`, `RDI` is the actor resolved from the parsed ID/type pair, `R12` is that actor's `+0x818` state block, and the `CTClientGame*` is saved at `[RBP+0x40]`. Version 1.1.12 records the semantic parsed ID/type boundary without requiring the word to equal `0x0209`, while retaining the old JSON fields for compatibility.

## Rejected paths

- Operation record `+0/+4/+8` remains plausible float XYZ only. The Wolf-following test is spatially confounded.
- Operation record `+0x24` remains `candidateField24`; values changed while the same Wolf remained selected.
- Entity `+0x660` is caster/local-player reference evidence and is not selected-target storage.
- Entity `+0x688` remains closed as a target hypothesis.
- Generic operation-record ID/type probes remain legacy export data and can no longer promote a selected-target conclusion.

## Remaining unknowns

- Live A/B/C/NONE captures are still required to turn the static direct-pointer candidate into runtime proof.
- The exact downstream edge from the combat reader to initial `0x0209` needs temporal runtime correlation.
- Target-death and out-of-range lifetime behavior should be observed naturally; the Inspector never manipulates either state.

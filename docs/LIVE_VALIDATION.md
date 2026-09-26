# Live validation — current profiles

The original 1.1.5 wiring had a circular dependency. The Differential Inspector decoded the operation word at the shared call directly from `R9`, while the Initial Nx call and producer counters entered replay experiment functions that returned immediately unless `Initial2xExperiment::armed` was already true. Validation therefore could not collect the evidence required to make arming safe. Inspector STOP also detached the debugger, and the status-owned evidence was discarded.

Version 1.1.6 separates read-only operation observation from replay. `operation_observer.cpp` is the canonical decoder for the Inspector and normal validation paths. Validation runs at the profile prep, shared-call, and return anchors while replay is disarmed. The producer breakpoint at `TClient.exe+99240` remains a distinct, optional fourth hardware breakpoint and its counts are reported separately.

## Event-path comparison

| Property | Differential Inspector | Normal live validation |
|---|---|---|
| Main breakpoint | profile `initialCallRva` (`+7F277D` on FB13C125) | profile prep/call/return (`+7F2760`, `+7F277D`, `+7F2782`) |
| Breakpoint type | Per-thread hardware execution breakpoint | Per-thread hardware execution breakpoint |
| DR slots | Dynamically selected free slots; shown per thread in Diagnostics | Dynamically selected free slots; shown per thread in Diagnostics |
| Register source | Integer/control context at the shared-call breakpoint | Integer/control context at prep/call/return breakpoints |
| Operation address | `R9` at the shared call | `R9` at the shared call |
| Decoder | `DecodeOperation` | `DecodeOperation` |
| Armed-state guard | None | None for observation; replay remains guarded by `Initial2xExperiment::armed` |
| Counter | Shared-call operation counters | Same session shared-call operation counters |
| Producer evidence | Not reused as producer evidence | Separate optional `+99240` producer counters |

The first meaningful old divergence was `ObserveInitialCall`: it ignored the event when `e.armed` was false. `RecordInitial2xProducer` had the same replay-state dependency. The new observer updates before either replay-specific function is considered.

## State and lifetime

Live evidence is keyed by PID, process creation time, target SHA-256, and profile identity. A change to any key, process exit, failed attachment, or real detach clears it. Inspector STOP preserves scalar evidence only when the process/session identity is unchanged. Research/Play page changes, Diagnostics visibility, and the saved Auto Arm setting do not clear it.

For the D0ECBBA1 and FB13C125 profiles, replay-critical validation requires validated profile fingerprints, prep/call/return hits, an initial `0x0209`, preserved replay-critical registers on its return, and nine `0x020A` call/return observations. The validation cast stays normal. Auto Arm is evaluated only after all nine periodic returns, so it can arm only the next cast.

Diagnostics lists every attached thread and every owned DR slot. Each thread gets an explicit validation coverage `YES/NO (n / 3)` line, followed by the prep/call/return, replay, producer, and Inspector breakpoint mappings.

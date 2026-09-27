# AOE Target Differential Research — 1.1.5-research

This mode is strictly read-only. It uses hardware execution breakpoints and guarded `ReadProcessMemory` snapshots. It does not call `WriteProcessMemory`, does not change page protection, does not patch TClient, and does not redirect RIP.

## Current live conclusion

The latest normal AOE produced one initial `0x0209` followed by nine periodic `0x020A` operations. At the initial shared call, `RDX` was the owner/entity and `R12 == RDX + 0x660`. The one `+0x660` record was actor ID `9534`, typed namespace `1` (`CTClientChar`), and its resolved actor pointer equaled the RDX owner. The `+0x660` collection is therefore caster/reference evidence. It is not selected-mob evidence unless later differential captures contradict this result.

## Static capture boundaries

Reference absolute addresses below use the last reported base `0x7FF76CB80000`; the program always computes `liveBase + RVA` for the current process.

- `TClient.exe+7F195D | 0x7FF76D37195D`: shared direct call. For the first `0x0209` after each marker, capture RCX, RDX, R8, R9, R12, R13, R14, R15, RSP, operation word, thread, timestamp, and marker.
- `TClient.exe+6CC93 | 0x7FF76CBECC93`: parse-complete observation boundary. Exact instruction: `cmp word ptr [r15+r13*2+3Ch], r14w` (`66 47 39 74 6F 3C`). At this boundary R12 is the operation record and RDI is the owner actor. The parser-frame fields are still live.

Static reads from the `TClient.exe+7F1720` path prove these later-consumed operation fields: `+0x00`, `+0x04`, `+0x08`, `+0x0C`, and `+0x24`. The `TClient.exe+6C950` parser also populates `+0x0E`, `+0x14`, `+0x18`, `+0x1C`, `+0x20`, `+0x28`, `+0x29`, `+0x2A`, `+0x2B`, `+0x2C`, and `+0x2E`. The inspector therefore snapshots exactly `0x30` bytes, offsets `0x00..0x2F`; it does not perform a broad memory scan.

At the parse boundary it also records the bounded frame values: operation/template word at `[RBP-0x40]`, parsed actor ID at `[RBP-0x30]`, typed namespace at `[RBP-0x3C]`, mode byte at `[RBP-0x3B]`, outer field copied to operation `+0x24` at `[RBP-0x2C]`, and parsed pair-list count at `[RBP+0x58]`. These labels describe provenance and width only; they do not assert selected-target semantics.

The read-only candidate resolver tests bounded uint32 fields at operation offsets `0x00,0x04,0x08,0x14,0x18,0x1C,0x20,0x24` only when a supported typed namespace byte is present at `0x28,0x29,0x2A,0x2B,0x2C,0x2E`. A candidate is proven only when A, B, and C resolve to three different actors and each resolved `actor+0x7E1` category is `2`.

## Capture workflow

1. Attach and wait for the profile to show VALID.
2. If Initial Nx is armed, press **PREPARE TARGET DIFFERENTIAL INSPECTOR**. This releases only the Initial Nx hardware breakpoint state and keeps the attached validated process session.
3. Press **START TARGET DIFFERENTIAL INSPECTOR**. Saved Auto Arm configuration is unchanged and Auto Arm is suppressed for this active research session.
4. Select Mob A, press **MARK NEXT MOB**, cast one normal AOE, and wait until the initial `0209` count increases.
5. Repeat for Mob B and Mob C. The next marker is refused until the preceding marker has captured its first initial `0x0209`.
6. Optionally mark D with no valid mob selected.
7. Press **STOP**, then **EXPORT JSON**.

The comparison table and JSON use only the first initial `0x0209` after each marker. Periodic `0x020A` events are retained in `periodic020AEvents` only and never enter the differential result.

No A/B/C captures are present in the repository yet. Consequently no selected-mob field, actor, source stage, UI-selection provenance, full data-flow chain, or future controlled-experiment point is claimed.

## Parser-to-record provenance recovered statically

`TClient.exe+6C950 | 0x7FF76CBEC950` parses the action stream through typed helper calls. The following destinations are direct, statically verified writes into the operation record:

| Destination | Population site | Width |
|---|---:|---:|
| `operation+0x2B` | `TClient.exe+6CA95 | 0x7FF76CBECA95` | byte |
| `operation+0x0E` | `TClient.exe+6CAA0 | 0x7FF76CBECAA0` | word |
| `operation+0x2A` | `TClient.exe+6CAAB | 0x7FF76CBECAAB` | byte |
| `operation+0x14` | `TClient.exe+6CAB6 | 0x7FF76CBECAB6` | dword |
| `operation+0x18` | `TClient.exe+6CAC1 | 0x7FF76CBECAC1` | dword |
| `operation+0x1C` | `TClient.exe+6CACC | 0x7FF76CBECACC` | dword |
| `operation+0x20` | `TClient.exe+6CAD7 | 0x7FF76CBECAD7` | dword |
| `operation+0x2E` | `TClient.exe+6CAE2 | 0x7FF76CBECAE2` | byte |
| `operation+0x28` | `TClient.exe+6CAF3 | 0x7FF76CBECAF3` | byte |
| `operation+0x29` | `TClient.exe+6CB00 | 0x7FF76CBECB00` | byte |
| `operation+0x2C` | `TClient.exe+6CB0D | 0x7FF76CBECB0D` | byte |
| `operation+0x00` | `TClient.exe+6CB18 | 0x7FF76CBECB18` | dword |
| `operation+0x04` | `TClient.exe+6CB25 | 0x7FF76CBECB25` | dword |
| `operation+0x08` | `TClient.exe+6CB32 | 0x7FF76CBECB32` | dword |
| `operation+0x24` | `TClient.exe+6CB4A | 0x7FF76CBECB4A` | dword copied from `[RBP-0x2C]` |
| `operation+0x0C` | `TClient.exe+6CDCF | 0x7FF76CBECDCF` | word copied from `[RBP-0x40]` |

The byte count at `[RBP+0x58]` controls a bounded loop. Each iteration parses a dword ID and byte namespace, allocates an 8-byte record, and appends its pointer to owner `+0x660` at `TClient.exe+6CC60 | 0x7FF76CBECC60`. Live evidence identifies this list as caster/reference data in the observed cast. The table records mechanical data flow only; no row is named as the selected mob until A/B/C actor resolution proves it.

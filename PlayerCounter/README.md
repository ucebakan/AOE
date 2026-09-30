# PlayerCounter 1.1 — Counter + Exit

Native Windows x64, C++20/Win32 status badge. The deliverable is `PlayerCounter/releases/1.1.0/PlayerCounter.exe`; it needs no .NET or separate Visual C++ runtime installation (static CRT).

Close the previous badge, launch this EXE and accept the Windows administrator permission prompt. It automatically searches for the unique `C:\Games\4Unity\TClient.exe` process. Drag the counter with the left mouse button. The red **Exit** button requests a return to character selection; it does not close PlayerCounter. Right-click the counter and select **PlayerCounter'ı kapat** to close the badge. There is no console, title bar, or taskbar button. The badge stays above other ordinary desktop windows; exclusive fullscreen games may cover desktop windows.

- `Player : 0` through `Player : 4`: white background, black text.
- `Player : 5` and above: red background, white text.
- `Player : --`: unavailable, unreadable, or incompatible; light gray background.

The value represents the supplied **other-player/PC collection count**, not total server population. A worker polls approximately every 300 ms; discovery retries every 2 seconds while disconnected. The worker keeps the read-only handle while the process lives, re-reads the context pointer for every sample, and clears state on process exit. UI painting and dragging never perform process discovery or hashing.

## Profile and patch protection

The approved profile is embedded in `reader.hpp` and `exit_profile.hpp`, so no editable sidecar or runtime profile download is required:

- SHA-256: `9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28`.
- AMD64 PE32+, timestamp `1790155782`, image size `16023552`.
- Resolver: `[module + 0xE6E9C0]`, then unsigned 64-bit count at `context + 0x1148`.
- Protected getter at RVA `0x7C1A20`: `48 8B 05 99 CF 6A 00 C3` (RIP-relative load of the approved context slot, then return).

Attachment hashes the game file and compares its exact approved identity. Every sample validates the loaded PE header and the getter's exact live bytes before reading the counter. A modified getter or mismatching header hides the count. A failed/partial read, null/invalid pointer, or context replacement during the sample also hides it. Unrelated runtime patches are outside the scope of the getter guard.

Unlike MobTP's broader resolver, this deliberately does **not** guess offsets or automatically approve a new game build. A new SHA requires a newly verified profile and rebuild. An AOB match alone never approves an unknown build. It performs no code patching or restoration. Counter polling uses only `PROCESS_QUERY_INFORMATION | PROCESS_VM_READ` and `ReadProcessMemory`.

## Exit action and protection

Exit is manual only; the player-count threshold never triggers it. The disabled button uses a darker red. It becomes available only when the counter and Exit guards pass. One click queues one action on the worker; double clicks are rejected. The button stays disabled while the action thread runs and for at least five seconds after dispatch. No automatic retry is performed.

Each accepted click rechecks the exact file SHA, PE identity and a unique full-function AOB match in executable sections at `0x95F200`. It opens a temporary action handle and compares process creation times to reject a replaced session. The in-memory getter, the entire 112-byte Exit function, and eight callee-entry guards must match the embedded profile. Root/context vtables and the root virtual call slot must also match. Finally, the function must be executable image memory belonging to the verified module, and the context slot is read again immediately before dispatch.

The fresh call plan is:

```text
context = Read<uint64_t>(moduleBase + 0xE6E9C0)
root = context - 0x19368
function = moduleBase + 0x95F200
```

Checked object identities: root `CTClientWnd` vtable RVA `0xCE9150`; context `CTClientGame` vtable RVA `0xCDC970`; root virtual slot `+0x2F0` points to RVA `0x89BDD0`. The old C7D70C/C7D740/C7EF94 triggers are not used. No absolute heap or module address is stored in the profile.

The button uses `CreateRemoteThread` to enter the existing native routine with `root` as its argument (RCX). The inspected routine accepts this pointer and returns zero. There is no allocated code stub, DLL injection, WriteProcessMemory, hook, or code patch. The native game routine itself changes game state. For this action only, the temporary handle includes the thread creation and VM rights required by [CreateRemoteThread](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createremotethread); the parameter follows the [Windows x64 calling convention](https://learn.microsoft.com/en-us/cpp/build/x64-calling-convention). Thus the **counter is read-only, but Exit is an active game action**.

These guards cover the listed code ranges and identities, not every possible game patch or internal state. Reads and dispatch are not atomic with game activity. The user's supplied runtime test established the native function/root relation; invoking it from this EXE's worker-created remote thread has not been live-tested. The badge never terminates a running action thread, including on application exit.

## Build and verification

From the workspace root, using Visual Studio Build Tools with CMake and the Windows SDK:

```powershell
cmake -S PlayerCounter -B build-player-counter -G 'Visual Studio 18 2026' -A x64
cmake --build build-player-counter --config Release --parallel
ctest --test-dir build-player-counter -C Release --output-on-failure
```

Standalone output: `build-player-counter/Release/PlayerCounter.exe`. Only the badge EXE needs distribution. Developer utilities:

- `PlayerCounterTests.exe`: deterministic counter and Exit guard tests.
- `PlayerCounterTests.exe --verify-profile`: read-only disk AOB verification against the installed game.
- `PlayerCounterTests.exe --probe`: read-only live counter and Exit-readiness check; it never calls Exit.
- `PlayerCounterUiTests.exe`: isolated render/click test; no worker or game access. Writes `PlayerCounter-ui.bmp` alongside itself.

On 2026-09-30 the x64 Release build and both CTest targets passed, as did the installed-file unique AOB verification. Tests cover counter/color transitions, failed reads, null/invalid pointers, context replacement, all nine Exit code guards, root/context identity, virtual-slot modifications, root underflow, module-relative function resolution, missing/ambiguous/non-executable AOBs, truncated PE data, and disconnected dispatch rejection. The UI test verifies normal/red/unavailable rendering, the red Exit button, one queued click and duplicate-click suppression. The rendered result was visually inspected. No live Exit call was made during development.

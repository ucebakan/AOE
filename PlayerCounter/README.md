# PlayerCounter

Native Windows x64, C++20/Win32 status badge. The deliverable is `PlayerCounter/releases/1.0.0/PlayerCounter.exe`; it needs no .NET or separate Visual C++ runtime installation (static CRT).

Launch the EXE and accept the Windows administrator permission prompt. The game on this machine denies non-elevated memory reads (Windows error 5), so the badge requests elevation through its application manifest. It still requests only query/read access to the game. It automatically searches for the unique `C:\Games\4Unity\TClient.exe` process. Drag the badge with the left mouse button; right-click and select **Exit** to close it. There is no console, title bar, or taskbar button. The badge stays above other ordinary desktop windows; exclusive fullscreen games may cover desktop windows.

- `Player : 0` through `Player : 4`: white background, black text.
- `Player : 5` and above: red background, white text.
- `Player : --`: unavailable, unreadable, or incompatible; light gray background.

The value represents the supplied **other-player/PC collection count**, not total server population. A worker polls approximately every 300 ms; discovery retries every 2 seconds while disconnected. The worker keeps the read-only handle while the process lives, re-reads the context pointer for every sample, and clears state on process exit. UI painting and dragging never perform process discovery or hashing.

## Profile and patch protection

The approved profile is embedded in `reader.hpp`, so no editable sidecar or runtime profile download is required:

- SHA-256: `9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28`.
- AMD64 PE32+, timestamp `1790155782`, image size `16023552`.
- Resolver: `[module + 0xE6E9C0]`, then unsigned 64-bit count at `context + 0x1148`.
- Protected getter at RVA `0x7C1A20`: `48 8B 05 99 CF 6A 00 C3` (RIP-relative load of the approved context slot, then return).

Attachment hashes the game file and compares its exact approved identity. Every sample validates the loaded PE header and the getter's exact live bytes before reading the counter. A modified getter or mismatching header hides the count. A failed/partial read, null/invalid pointer, or context replacement during the sample also hides it. Unrelated runtime patches are outside the scope of the getter guard.

Unlike MobTP's broader resolver, this deliberately does **not** guess offsets or automatically approve a new game build. A new SHA requires a newly verified profile and rebuild. It performs no patching or restoration. The only target-process permissions are `PROCESS_QUERY_INFORMATION | PROCESS_VM_READ`; all target-memory access uses `ReadProcessMemory`.

## Build and verification

From the workspace root, using Visual Studio Build Tools with CMake and the Windows SDK:

```powershell
cmake -S PlayerCounter -B build-player-counter -G 'Visual Studio 18 2026' -A x64
cmake --build build-player-counter --config Release --parallel
ctest --test-dir build-player-counter -C Release --output-on-failure
```

Standalone output: `build-player-counter/Release/PlayerCounter.exe`. Only the badge EXE needs distribution; `PlayerCounterTests.exe` is a development utility. Run that utility with `--probe` for a single read-only live attachment check.

Automated tests cover 0/1/4/5/127 and full-width uint64 values, both threshold directions, unavailable states, read failure, invalid pointers, context replacement, modified getter rejection and PE timestamp mismatch. On 2026-09-30 the x64 Release build and tests passed. A live probe could not attach (game unavailable or access denied), so live population changes and a real game restart have not been verified.

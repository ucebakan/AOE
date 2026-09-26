# Current Build

- Manager: **4Unity AOE Manager 1.1.12-research**
- Manager EXE SHA-256: `1BFC1025DD188114BBD832D8EB81D7BFA5A42FDAEBD1EA069AC409D49938CC5A`
- Manager EXE: `build\Release\4UnityAOEManager.exe`
- Build date: **2026-09-21**
- Old supported TClient SHA-256: `CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E`
- Previous statically recovered TClient SHA-256: `D0ECBBA10685D94E7CA632D6A9CEB9B3C2C420D609DB7CC4243AFF2F493A62C1`
- Current statically recovered TClient SHA-256: `FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7`
- Current profile: `profiles\FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7.json`
- Current-build AOE Target Differential Inspector: two read-only execution breakpoints at `+0x7F277D` and `+0x6CB73`; explicit A/B/C/D slots capture the first `0x0209` after each marker, with periodic `0x020A` stored separately. A completed slot immediately enables the next marker, and a pending marker can be cancelled without losing completed captures.
- Target Writer Runtime Inspector: one read-only execution breakpoint per thread at `+0x7F6411`; automatically records up to 100 phase-labelled pre-store events, compares RBX with `[moduleBase+E6C9C0]`, validates R8 actor/category/monster XYZ, and exports conservative classifications. Post-store is expected from R8 and is explicitly not reported as observed.
- Selected Target Provenance Inspector: zero-breakpoint, read-only A/B/C/NONE snapshots of the static `CTClientGame+0x2318` direct-pointer candidate, with category-2 identity, typed-tree resolution, and validated monster XYZ. Prior manual validation returned NULL in all three tested states, so this remains an UNPROVEN static target-state candidate. Future A/B/C evidence is required before the UI reports `PROVEN_DIRECT_POINTER`.
- New-build Initial Nx: **available after same-session live validation**. The first normal AOE must prove prep/call/return, `0x0209`, nine `0x020A` call/returns, and replay-critical register semantics. Auto Arm then arms the next cast only.
- Initial Nx lifecycle: changing **Initial Calls** creates a separate next experiment without clearing same-session validation. Completed results remain visible, and changes made during replay are queued until that cast finishes.

The current initial replay anchors are preparation `+0x7F2760`, shared call `+0x7F277D`, return `+0x7F2782`, shared worker `+0x7BC4C0`, producer call `+0x7BC817`, and producer `+0x99150`. All three SHA-selected profiles remain available.

The current relocation proof is in [PATCH_RECOVERY_2026-09-20.md](PATCH_RECOVERY_2026-09-20.md). The observer/replay separation and validation lifetime are documented in [LIVE_VALIDATION.md](LIVE_VALIDATION.md). Configuration and per-cast state are documented in [NX_EXPERIMENT_LIFECYCLE.md](NX_EXPERIMENT_LIFECYCLE.md). Target-writer static context, runtime capture fields, and the live workflow are documented in [TARGET_WRITER_RUNTIME_RESEARCH.md](TARGET_WRITER_RUNTIME_RESEARCH.md). Current differential fields and live `+0x660` correction are in [TARGET_DIFFERENTIAL_RESEARCH.md](TARGET_DIFFERENTIAL_RESEARCH.md); the sequential A/B/C/D workflow is in [DIFFERENTIAL_INSPECTOR_MARKER_LIFECYCLE.md](DIFFERENTIAL_INSPECTOR_MARKER_LIFECYCLE.md).

The golden `C:\Users\Public\Documents\4UnityAOETracer` project is a separate read-only reference. Manager builds and outputs never target that directory.




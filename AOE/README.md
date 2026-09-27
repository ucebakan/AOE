# 4Unity AOE Manager

## 2026-09-25 locator onarımı

Güncel `9CD77CD0...70E5C28` TClient için ayrı SHA profili ve runtime semantik
AOB/çağrı ilişkisi doğrulaması eklendi. Initial Nx repetition/restore motoru
değişmedi; aynı-session live-validation hâlâ zorunlu.

- `--aoe-diagnostic --offline`: yalnız dosya/profil doğrulaması.
- `--aoe-diagnostic`: query/read-only canlı byte doğrulaması; debugger, HWBP,
  settings/AutoArm veya gameplay yazısı yok. Sonuç: `logs/aoe-diagnostic.txt`.
- `--safe-manual`: GUI'yi bu açılış için AutoAttach/AutoArm kapalı açar.
- Testler: `build/Release/aoe_locator_tests.exe --current-image` ve
  `build/Release/tracer_tests.exe --analysis-only`.

Tam desenler, RVA eşlemeleri, sayaç sınırlaması ve count3/5 manuel akışı:
[AOE_LOCATOR_REPAIR_2026-09-25.md](docs/AOE_LOCATOR_REPAIR_2026-09-25.md).
`Linked020BCount` bağımsız observer olmadığı için `UNAVAILABLE`; sıfırmış gibi
gösterilmez. Yeni profilde bu görev dışındaki target-inspector/provenance
modülleri etkinleştirilmedi. Aşağıdaki bölümler önceki sürümün tarihî bağlamıdır.

Version **1.1.10-research** is a separate manager-style application built around the proven Initial Nx debugger engine. It provides Research / Recovery, Profiles, and Play tabs; SHA-selected build profiles; session-safe auto attach and auto arm; persistent per-user Windows startup configuration; DPI-aware resizing; and frozen/copyable diagnostics snapshots.

Profiles support the three statically recovered TClient hashes documented in `docs/CURRENT_BUILD.md`. Unknown builds remain disabled until a verified profile is created. The current `FB13C125...` profile requires a same-session read-only validation cast before Initial Nx can arm.

Build with:

```powershell
& 'C:\Users\Public\Documents\4UnityAOEManager\build.ps1'
```

Release output:

`C:\Users\Public\Documents\4UnityAOEManager\build\Release\4UnityAOEManager.exe`

Settings are stored at `%LOCALAPPDATA%\4UnityAOEManager\settings.json`. Optional startup uses only `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `4UnityAOEManager`.

See `docs/AOE_RECOVERY_GUIDE.md` before attempting recovery after a TClient patch.


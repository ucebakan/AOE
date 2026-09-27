# İki düğmeli Invisible / Aggro EXE

Tarih: 2026-09-28. Kaynak: `tools/InvisibleAggro`; dağıtım: `dist/4UnityInvisibleAggro/4UnityInvisibleAggro.exe`.
Eski KNOWN_GOOD projesi değiştirilmedi.

UI yalnız Invisible ve Aggro düğmeleri ile durum metni içerir. Açık moda tekrar basmak kapatır;
modlar birbirini dışlar. Eski NPC AGGRO TEST davranışı Aggro adıyla taşındı.

## Güncel eşleştirme

- SHA: `9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28`.
- Player çözümü: `module+E6E9C0 → context+2710`; güncel MonsterList aracındaki yol.
- Vtable `CDBCC0`, MSVC RTTI `CTClientChar`, type=1, ID!=0, XYZ/W doğrulanır.
- +7E2 erişimleri `86DE04`, `7A09B7`; +47E writer'ları `86DEB7`, `86E000`.
- Build SHA, dört benzersiz statik imza ve canlı instruction byte'ları kontrol edilir.
- Eski XYZ writer imzası, broad heap scan, debugger, hook veya DLL injection kullanılmaz.

## Davranış ve geri alma

Invisible yalnız +7E2=1 isteğidir. Aggro iki writer'ın 6/7 byte NOP'u, +47E=255 ve +7E2=1 isteğidir.
Kod NOP'ları global writer'ları etkiler; data yazımları seçili ana player'a aittir.
Geri alma önceki byte değerlerini saklar; özgün KNOWN_GOOD'daki her zaman stealth=0 yaklaşımından farklıdır.
Oyunun değiştirdiği data ezilmez. Yazma/readback başarısızlığında denenen yazım da dahil reverse rollback yapılır.
İlk mutasyondan önce oturum kimliğiyle journal yazılır; normal çıkış ve uygun yeniden açılışta restore edilir.
Canlı thread'ler yalnız kısa commit/rollback aralığında duraklatılır; patch ortasındaki RIP'de işlem reddedilir.
Beklenmeyen code byte'ları zorla restore edilmez; süreç/oyuncu değişirse eski data adresleri kullanılmaz.

## Testler ve sınır

- `logs/invisible_aggro_tests.json`: 10 test grubu, 0 başarısızlık; partial write ve commit-journal failure rollback dahil.
- `logs/invisible_aggro_ui.png`: kendi WinForms render'ından görsel kontrol; tam 2 button kontrolü.
- `logs/invisible_aggro_probe.json`: güncel oturumda bellek okuma Win32 5, writes=0.
- Sıfır derleyici uyarısı/hatası. Self-contained x64 single-file dağıtım.

Canlı player kimliği ve gerçek oyun etkisi bu çalıştırıcıdan erişim alınamadığı için doğrulanamadı.
Disk doğrulaması ve simüle bellek testleri gameplay başarısı iddiası değildir.

## Yeniden derleme

```powershell
dotnet publish tools/InvisibleAggro/InvisibleAggro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o dist/4UnityInvisibleAggro
```

`--self-test <output.json>` ve `--probe <output.json>` GUI açmadan çalışır; probe yalnız okur.
Geliştirme sırasında DLL'yi `dotnet` ile çalıştırmak yükseltilmiş EXE manifestini uygulamaz;
dağıtım EXE'si Windows'tan yönetici izni ister.

## 1.1.0 — 2026-09-28

User confirmed the prior build works. Added per-SHA persistent profiles under
%LOCALAPPDATA%\4UnityInvisibleAggro\profiles. Known builds use direct RVA
verification; unknown builds require the visible scan approval button. Approved
recovery scans five unique code anchors and RTTI, derives relocated writer/context
and vtable addresses, and persists only after live player/code validation. The
player-member layout remains constrained to the established offsets; incompatible
layout or ambiguous/missing signatures fail closed and require further analysis.

Removed full disk hashing/scanning from toggles and cached code baselines per
session. Process/player identity, original-byte validation, rollback journal and
thread guards remain. Toggle events now include elapsedMs.

Validation: zero-warning Release build; 13 test groups passed, including a synthetic
unknown SHA with a relocated writer, persistence without rescanning, corrupt-profile
rejection and ambiguous/missing signatures. Normal/approval UI previews inspected.
No new live gameplay timing claim. Prior release saved in dist/4UnityInvisibleAggro-v1.0-backup.
Report: logs/invisible_aggro_v11_tests.json.

# 4Unity araçları

Bu depo önceki AOE Git geçmişini korur. Projeler ayrı klasörlerdedir:

| Proje | Kaynaklar | Hazır sürüm |
|---|---|---|
| AOE Manager 1.1.17 | [AOE](AOE/) | [EXE](AOE/releases/1.1.17-research/4UnityAOEManager-1.1.17-research.exe) |
| Invisible / Aggro 1.1.0 | [InvisibleAggro](InvisibleAggro/) | [ZIP](InvisibleAggro/releases/1.1.0/4UnityInvisibleAggro-1.1.0-win-x64.zip) |

Invisible/Aggro ZIP dosyasını çıkarıp EXE'yi çalıştırın. Kaynak kodu `InvisibleAggro/src`,
test raporu ve ekran görüntüleri `InvisibleAggro/evidence` içindedir.

## Derleme

AOE için Visual Studio C++ x64 Build Tools ve CMake gerekir:

```powershell
.\AOE\build.ps1
```

Invisible/Aggro için .NET 9 SDK gerekir:

```powershell
.\InvisibleAggro\build.ps1
```

AOE içindeki eski belgelerde geçen mutlak yollar tarihî çalışma klasörleridir.
Bu kopyada AOE proje kökü `AOE/` klasörüdür; derleme betiği kendi konumunu kullanır.
Oyun EXE'si, kullanıcı ayarları ve canlı oturum kayıtları bu depoya eklenmez.

## Speed / Jump 1.0.0

[Kaynaklar ve kullanım](SpeedJump/) · [Windows x64 ZIP](SpeedJump/releases/1.0.0/4UnitySpeedJump-1.0.0-win-x64.zip)

Paketin tamamını aynı klasöre çıkarıp `4UnitySpeedJump.exe` dosyasını çalıştırın.
SHA profili, imzalar ve README pakete dahildir. Arayüzde yalnız SPEED/JUMP vardır.
Derleme: `.\SpeedJump\build.ps1`. Statik/self-test kanıtları `SpeedJump/evidence` içindedir.
Canlı oyun testi bu teslim oturumunda doğrulanmadı; ayrıntılar proje README'sindedir.
## PlayerXYZ 1.1.0

[Kaynaklar ve kullanım](PlayerXYZ/) · [Windows x64 ZIP](PlayerXYZ/releases/1.1.0/PlayerXYZ-1.1.0-win-x64.zip)

Canlı local P ve iki XYZ grubu; tek seferlik koordinat yazımı. SHA profili, RTTI/AOB resolver ve imzalar pakete dahildir. ZIP'in tamamını çıkarın.
Derleme: `.\PlayerXYZ\build.ps1`. Testler: `.\PlayerXYZ\build.ps1 -SelfTest` (resolver testi mevcut referans TClient.exe gerektirir).
Test raporları ve UI görüntüsü `PlayerXYZ/evidence` içindedir. Gelecekteki bütün build'lerin çözüleceği garanti edilmez.
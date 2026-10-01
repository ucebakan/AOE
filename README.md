# 4Unity araçları

## Birleşik uygulama

[4UnityTools Control Center](Suite/) genel bakış düğmeleriyle araçları yönetir; PlayerCounter ayrı küçük pencere olarak açılır.
[Windows x64 ZIP · tek EXE](Suite/releases/0.5.0/4UnityTools-0.5.0-win-x64.zip) · Derleme: `.\Suite\build.ps1`.
Antrasit/lavanta arayüz, üst bilgi ve araç barı, sekmeler arasında korunan durum
ve Player XYZ alanlarında sayı doğrulaması içerir.

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
## MobTP 2.1.0

[Kaynaklar ve kullanım](MobTP/) · [Windows x64 ZIP](MobTP/releases/2.1.0/MobTP-2.1.0-win-x64.zip)

Oyuncuya mesafeye göre iki yönlü sıralama, atanabilir oyun içi kısayol, uygun mob sayaçları ve SHA/AOB/RTTI profil doğrulaması.
.NET 9 Windows Desktop runtime gerekir. Derleme: `./MobTP/build.ps1`.
Paylaşılan kaynaklar, 31 testin raporları ve arayüz görüntüsü MobTP klasöründedir.

## PlayerCounter 1.1.0

[Kaynaklar ve kullanım](PlayerCounter/) · [Windows x64 EXE](PlayerCounter/releases/1.1.0/PlayerCounter.exe)

Salt okunur oyuncu sayacı ve kırmızı Exit butonu; 300 ms yenileme, 5 ve üzeri kırmızı gösterge. Exit yalnız tıklamayla çağrılır; SHA/build profili, tekil AOB, canlı kod ve root/context kimlik kontrolleri bulunur. Windows yönetici izni ister; harici runtime gerekmez. Derleme ve koruma/arayüz testleri geçti; canlı Exit çağrısı bu sürümle henüz denenmedi.

## SafeMode ve Multikill JE

Birleşik 0.3 sürümünde SafeMode, Player Count >= 1 olduğunda etkin işlemleri
geri alır. [Multikill](Multikill/) aynı uygulamaya eklenmiştir;
[bağımsız EXE paketi](Multikill/releases/1.2.0/4UnityMultikill-1.2.0-win-x64.zip) de kullanılabilir.
AOB, SHA/profil ve oturum doğrulaması ile patch recovery içerir.


0.4 sürümünde ana Player XYZ kartında X/Y/Z + Işınlan bulunur. Kartlar ↕
tutamacından sürüklenerek sıralanır, düzen saklanır. Multikill 1.1, oyun
patch'inden sonra SHA değişimini AOB + semantik fingerprint taramasıyla karşılar.

0.5 sürümünde onaylı açılış taraması 1–9 işlevin yalnız yollarını kontrol eder; aynı SHA için kalıcı profiller kullanılır. Oyun oturumu değişince canlı pointer’lar yeniden çözülür, AOE manuel doğrulaması ve tüm işlevlerin kapalı başlangıcı korunur.

# 4Unity Multikill JE 1.2.0

Hazır uygulama: [Windows x64 ZIP](releases/1.2.0/4UnityMultikill-1.2.0-win-x64.zip). ZIP içindeki `4UnityMultikill.exe` doğrudan çalıştırılır. SHA-256 değerleri aynı klasördeki `SHA256SUMS.txt` dosyasındadır. Tek dosya Windows x64;
.NET kurulumu gerekmez. Her build `requireAdministrator` UAC manifesti taşır.
Birleşik 4UnityTools 0.5 içinde de aynı modül bulunur.

Önce **Profili doğrula**, ardından **Multikill · AÇ**. Kapat düğmesi ve normal
uygulama kapanışı değişikliği geri alır. Doğrulama kendiliğinden patch açmaz.

## Doğrulanmış köken

Önceki **Multikill semantiğini netleştir** konuşmasında PID **14788**, SHA-256
`9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28`
üzerinde `TClient+0x7DA93E: 0F 86 78 01 00 00 → 0F 84 78 01 00 00`
uygulaması ve restore doğrulanmış. Eski CLI:
`C:/Users/Public/Documents/4UnitySkillProfiler/tools/LegacyJePatcher/bin/Release/net9.0-windows/LegacyJePatcher.exe`
(gerçek konumu kardeş 4UnitySkillProfiler çalışma klasörüdür).

2026-10-01 disk SHA'sı aynı. Mevcut görünen PID 67164 için salt okunur erişim
Win32 5 ile reddedildi; canlı oturum/patch durumu bu geliştirme turunda doğrulanmadı.
Bu araç client hedef filtresinin JE değişikliğini uygular. Önceki araştırmada
bütün hedeflere hasar sağlandığı kanıtlanmamıştır; ek packet/range mutasyonu içermez.

## Doğrulama ve kurtarma

- Gömülü `profiles/known.json`: exact SHA-256, x64 PE timestamp/image size,
  `.text` içinde tekil 80-byte AOB ve doğrulanmış RVA. İlk 48-byte aday iki yerde
  eşleştiğinden reddedildi; 80-byte imza tekildir.
- Salt okunur bağlantı: exact oyun yolu, PID + creation time + module base,
  canlı PE, executable MEM_IMAGE sayfası ve canlı AOB kontrol edilir.
- Yalnız branch opcode'unun tek byte'ı `86 ↔ 84` değişir; çevredeki instruction
  byte'ları öncesinde/sonrasında doğrulanır. Instruction cache flush ve sayfa
  korumasının geri alınması zorunludur.
- `%LOCALAPPDATA%/4UnityMultikill/patch-recovery.json` yazımdan ÖNCE kalıcı
  olarak kaydedilir. Kesinti sonrası **Profili doğrula**
  aynı oturumun patch'ini geri alır. Kayıt yalnız doğrulanmış adrese/byte'lara izin verir.
- Farklı oturumun kaydı arşivlenir; PID yeniden kullanıldıysa eski adres yazılmaz.
  Sahiplik kaydı olmayan JE veya beklenmedik canlı byte'lar üzerine yazılmaz.
  Geri alma başarısızsa kayıt korunur ve normal kapanış engellenir.
- Birleşik ve bağımsız uygulama aynı sahiplik kilidini kullanır; aynı anda iki
  Multikill denetleyicisi bağlanamaz. SafeMode, birleşik uygulamadaki modülü yönetir;
  ayrıca başlatılmış bağımsız EXE'leri yönetmez.
- SHA değişince `BuildRecovery` yürütülebilir PE bölümlerini tarar. Maskeli SHUFPS
  AOB çıpasından başlayan 18 komutluk fingerprint; karekök/mesafe, range load ve
  COMISS/JBE, ID sıfır kontrolü, 8-byte vektör uzunluğu ve 64 sınırı, 8-byte
  allocation, ID + type 2 descriptor yazımını register/dataflow ilişkileriyle
  doğrular. Üç ret dalı aynı yürütülebilir hedefe gitmeli; blok ve ret hedefi aynı
  x64 unwind fonksiyonunun içinde olmalıdır. Type 7 benzeri kabul edilmez.
- Yer değiştirme, relative displacement/alan offset değişiklikleri ve sınırlı
  NOP eklemeleri desteklenir. Bu genel amaçlı bir yeni kod çözücü değildir:
  kontrol akışı, komut dizisi veya yapısal sabitler değişirse yeniden analiz gerekir.
- Yalnız TEK aday kabul edilir. Canlı PE/oturum + tam yeni kod byte'ları ayrıca
  doğrulanır. Yeni SHA'ya ait profil `profiles/<SHA>.json` olarak saklanır;
  kaydedilmiş profil aynı SHA için yerel semantik/byte kontrolleriyle yüklenir; bozuk cache veya yeni SHA tam tarama gerektirir.
  Bulunan profil kendiliğinden JE açmaz. **Patch Recovery · Yeniden tara**,
  etkin patch'i önce geri alıp güncel dosya için bu doğrulamayı yeniden yapar.

Derleme: `./Multikill/build.ps1`. Testler sahte bellek üzerinde apply/restore,
kesinti kurtarması, yanlış kayıt, PID reuse ve hata yollarını sınar. Disk profili
salt okunur doğrulanır; canlı oyuna yazılmaz. Rapor: `evidence/tests.json`.
UI/DPI testleri birleşik uygulamanın `Suite/evidence/suite-tests.json` raporundadır.


1.1 recovery testleri disk kopyaları üzerinde değişmiş SHA, yeniden konumlandırma,
branch/alan displacement değişiklikleri, NOP ekleme, type 7 impostor, eksik/çift
aday, ayrılan ret hedefleri ve yeni profile ait apply/restore günlüğünü kapsar.
Gerçek TClient dosyası değiştirilmez; oyun belleğine yazılmaz.

## SHA önbelleği (1.2)

İlk çözümlemede tekil AOB/semantik tarama sonucu sürümlü JSON olarak kaydedilir. Aynı SHA'da sonraki bağlantılar tüm kodu taramaz: kayıtlı RVA'da sınırlandırılmış semantik akış, branch, operandlar, PE ve byte'lar tekrar doğrulanır. Eski/bozuk cache otomatik tam doğrulamaya düşer. Mutlak oturum adresleri kaydedilmez; her bağlantı yeni PID, creation time, modül tabanı ve canlı AOB doğrulaması yapar. Suite hazırlığı bekleyen crash journal için yazma yapmaz, manuel Profili doğrula adımına yönlendirir.

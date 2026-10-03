# 4UnityTools 0.6.0 — oyun patch recovery

Yeni oyun sürümü SHA-256 ile ayırt edilir. Yeni SHA için XYZ, Speed/Jump,
Invisible/Aggro, MobTP, AOE ve Counter/Exit yolları kod imzalarından yeniden
çözülür. Multikill'in mevcut semantik recovery yolu korunur.

Tarama yalnız adreslerin yerini bulmaz: alan operandlarını, sınıf RTTI'sini,
metot bağlantılarını ve gerekli çağrı ilişkilerini de doğrular. Önceki build'in
RVA ve alan offsetleri yeni SHA'ya taşınmaz. Eksik veya birden fazla geçerli
aday varsa ilgili işlev hazır sayılmaz.

Disk profilleri SHA'ya göre tutulur. Aynı SHA'da yerel kod ve semantik
kontrollerden geçen profil yeniden kullanılır; bozuk profil yeniden üretilir.
PID, modül tabanı ve heap pointer'ları profil olarak kaydedilmez. Her oyun
oturumunda canlı yol ve beklenen kod yeniden doğrulanır.

Açılıştaki “Tarama başlasın mı?” kabul edildiğinde dokuz işlev sırasıyla
kontrol edilir. Bu kontrol hiçbir işlevi açmaz. AOE'nin adres profili otomatik
oluşturulur; ATTACH NOW ve aynı oturumdaki canlı cast doğrulaması korunur.
Invisible/Aggro adayı canlı doğrulama tamamlandıktan sonra kalıcılaştırılır.
UAC yönetici gereksinimi, patch sahipliği ve geri alma kayıtları korunur.

## Doğrulama

- Güncel oyun SHA'sı:
  `98FAFD5A7616A4A33959713093B46E425C883B6D00CC1475EEB82BC50F78C77A`.
- `Suite/evidence/recovery-tests-2026-10-03.json`: tüm modüllerin disk çözümü,
  SHA önbelleği, değişen alanlar ve eksik/çoklu imza reddi.
- `Suite/evidence/aoe-recovery-tests.txt`: 54 AOE tarama/profil testi.
- `Suite/evidence/suite-tests.json`: 197 uygulama kontrolü.
- XYZ, Speed/Jump, Invisible/Aggro ve Counter testleri: bozuk profil,
  karşılaştırma/yazma uyuşmazlığı, canlı kod değişimi ve oturum değişimi.

Bu kontroller oyuna yazım veya Exit çağrısı yapmaz. Güncel EXE'nin gerçek
oyun oturumundaki etkileri ayrıca kullanıcı tarafından denenmelidir. Gelecek
patch kodun yapısını desteklenen imzalardan farklı değiştirirse yeni bir
recovery kuralı gerekir; böyle bir durumda eski adresle devam edilmez.

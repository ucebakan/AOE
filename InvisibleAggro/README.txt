4Unity Invisible / Aggro — 1.1.0

Kullanım
1. Oyuna karakterinizle giriş yapın.
2. Eski aracı normal şekilde kapatıp 4UnityInvisibleAggro.exe dosyasını açın.
3. Hazır durumunda Invisible veya Aggro düğmesine basın.
4. Açık düğmeye tekrar basmak kapatır; diğer düğme moda geçiş yapar.
5. Normal çıkışta uygulamanın değişiklikleri geri alınır.

Profil ve güncelleme
- Başlangıçta TClient.exe SHA256 hesaplanır. Bilinen SHA için kayıtlı RVA'lar
  kullanılır; tam imza taraması yapılmaz. Mevcut çalışan sürüm dahili profildir.
- Bilinmeyen/doğrulanamayan sürümde iki mod düğmesi kapalı kalır ve
  "Onaylıyorum · Yeni sürümü tara" düğmesi görünür.
- Yalnız bu düğme taramayı başlatır. Beş kod imzası ve CTClientChar RTTI
  aranır; writer adresleri, bağlam globali ve oyuncu vtable adresi çözülür.
- Canlı oyuncu ve kod doğrulanmadan yeni profil kalıcı kaydedilmez.
- Profil dosyası: %LOCALAPPDATA%\4UnityInvisibleAggro\profiles\<SHA256>.json
- İmzalar kaybolur/çoğalırsa veya oyuncu yapısı değişirse yeni analiz gerekir.
  Her gelecek patch için otomatik uyumluluk garantisi yoktur. Oyuncu zincirindeki
  +2710 ve mevcut alan yerleşimi bu sürümde korunmuş olmalıdır.

Hızlı aç/kapat
Adresler aynı oturumda tekrar kullanılır. Aç/kapat sırasında EXE okunmaz,
hash hesaplanmaz ve tam AOB taraması yapılmaz. Canlı oyuncu kimliği ve kısa
kod bölgeleri yine kontrol edilir. Oturum/oyuncu değişince tekrar bağlanılır.
Gerçek işlem süreleri events.jsonl dosyasında elapsedMs alanına yazılır.

İki mod birbirini dışlar. Aggro önceki NPC AGGRO TEST davranışını korur;
Invisible alanını da açar. Freeze yapılmaz. Kod yazılırken thread'ler kısa
süre duraklatılır. Yazma hatası geri alınır; önceki alan değerleri kaydedilir.
Başka işlemlerin değiştirdiği kod zorla ezilmez; eski oyuncuya yazılmaz.

Hedef: C:\Games\4Unity\TClient.exe
.NET çalışma zamanı EXE içindedir. Yönetici izni gerekir.
Log: %LOCALAPPDATA%\4UnityInvisibleAggro\events.jsonl
Geri alma kaydı: %LOCALAPPDATA%\4UnityInvisibleAggro\owned-state.json

Doğrulama — 28 Eylül 2026
Önceki sürümün çalıştığı kullanıcı tarafından doğrulandı. 1.1.0 derlemesi
uyarısız tamamlandı; 13 test grubu geçti. Testler bilinmeyen SHA/onay kapısı,
taşınmış adresin bulunması, profilin taramasız yüklenmesi, bozuk profil,
tekil olmayan/eksik imza, mod geçişi ve yazma hatalarının geri alınmasını kapsar.
Normal ve onay ekranları görüntülenerek kontrol edildi. Yeni sürümün canlı
oyun içi aç/kapat süresi bu geliştirme oturumunda ölçülmedi.

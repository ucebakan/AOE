# Salesman açılışı — 0.7.4

Genel bakıştaki Salesman butonu aynı tuşsuz açılış yolunu kullanır. 0.7.3'teki
MobTP düzeltmeleri korunur. UAC `requireAdministrator`, `uiAccess=false` kalır.

Her basışta 119 MB FileMerger paketinin tekrar hash edilmesi kaldırıldı.
İlk kontrolde SHA hesaplanır. Sonrakilerde dosya kimliği, disk kimliği, boyut,
oluşturma zamanı, yazılma zamanı ve Windows ChangeTime karşılaştırılır. Dosya
değiştiğinde veya açıkça yeniden tarama istendiğinde hash yeniden hesaplanır.
Kontrol sırasında açık read handle başka bir yazma/değiştirme işlemini engeller;
handle kontrolden hemen sonra kapatılır, oyun dosyası sürekli kilitlenmez.
Bu Windows bilgileri alınamazsa her seferinde hash hesaplamaya dönülür.

Aynı EXE SHA ve aynı profil JSON metni için bu uygulama sürecinde daha önce
doğrulanan semantik sonuç tekrar kullanılır. EXE SHA her bağlantıda hesaplanır;
JSON değişirse semantik kontroller yeniden çalışır. Profil nesnesi her kullanımda
yeniden oluşturulur. Canlı kod/RTTI/oturum/owner/player/shop ve NPC kontrolleri,
ayrı guardian ve geri alma denetimleri devam eder. Fonksiyon sınırları için
aynı PE exception table'ı tekrar tekrar kopyalamak yerine bir indeks kullanılır;
sırasız/çakışan tablo kabul edilmez.

Ölçümde eski kaynak SHA kontrolü 592 ms sürdü. Yeni sürümde ilk kontrol 455 ms,
dosya değişmediyse sonraki kontrol 0,19 ms sürdü. Bu tüm pencere açılış süresi
değildir; ilk kullanımda doğrulama ve guardian başlangıcı yine vardır.
Oyunun önceki başarılı açılışlarında gönderimden pencereye yanıt yaklaşık
80–185 ms idi. Yeni loglar hazırlık, guardian başlangıcı, gönderim ve yanıt
sürelerini ayrı kaydeder.

23 Salesman/recovery kontrolü ve tüm modül recovery testleri geçti. Dosyanın
aynı boyutta değiştirilmesi ve LastWriteTime'ın geri getirilmesi, dosya değiştirme,
bozuk profil JSON, SHA/AOB uyuşmazlıkları ve beş guardian geri alma senaryosu
kontrol edildi. Değişmeyen arayüz için 0.7.3'teki 213 UI kontrolü geçerlidir;
dağıtılan modül DLL'leri bu test edilmiş sürümle birebir aynı özetlere sahiptir.
Bu tur yeni hızlandırılmış butonla oyun içi açılış henüz ölçülmedi.

Collection araştırması için ayrıca salt okunur bir CLI snapshot bulunur;
Collection açan/kullanan bir UI butonu bu sürümde yoktur.

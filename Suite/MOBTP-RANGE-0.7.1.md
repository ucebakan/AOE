# MobTP Home range — 4UnityTools 0.7.1

MobTP ayarlarındaki **Home range (XZ)** alanı, oyuncu→Home ve hedef→Home mesafeleri
için kullanılır. Varsayılan değer 50'dir; kullanıcı 50'nin altında veya üzerinde
pozitif bir değer girebilir (en az 0.1, bir ondalık basamak). Çevre mesafesi, mobların
oyuncu etrafında yerleşeceği halka mesafesidir ve ayrı kalır.

Değer `%LOCALAPPDATA%/MobTP/settings.json` içinde saklanır. Eski ayarlarda alan
yoksa 50 kullanılır. Liste sayıları ve uygunluk durumları değer değiştiğinde hemen
yenilenir. Düğme, genel bakış ve oyun içi kısayol aynı tek seferlik teleport yolunu
kullanır. İşlem başlayınca seçilen mesafe sabitlenir; tüm taze Home/kimlik kontrolleri
aynı değere göre yapılır. Batch kaydı seçilen `home_radius` değerini içerir.

Home koordinatlarına yazılmaz. Kimlik, oturum, oyuncu değişimi, B→A yazım sırası
ve geri okuma kontrolleri korunur. Bu ayar oyunun sunucu tarafındaki menzilini değiştirmez.

Doğrulama: küçük/büyük ve tam sınır mesafeleri, Home değişince ikinci yazının
engellenmesi, eski ayar uyumluluğu ve ayar serileştirmesi; sentetik birleşik arayüzde
sayaç/düğme yenilenmesi ve sayfa geçişi; dar/geniş pencere ve 125/150/200% ölçek
kontrolleri. Testlerde canlı oyuna teleport yazımı yapılmaz.

EXE `requestedExecutionLevel="requireAdministrator"`, `uiAccess="false"` manifestiyle
üretilir. Açık eski uygulamayı normal şekilde kapatıp yeni EXE'yi açın.

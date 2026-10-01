# Kalıcı ürün gereksinimi

- Arayüz dinamik ve DPI uyumlu olmalı. Yazılar/kontroller üst üste binmemeli, metin arka planla kaybolmamalı; normal, dar ve tam ekran geçişlerinde düzen korunmalı. Yüksek DPI ve yeniden boyutlandırma kontrollerini her UI değişikliğinde çalıştır.
- İş mantığı, araç adaptörleri ve yerleşim ayrı modüllerde kalmalı. Sabit piksel koordinatlarını ana yerleşim mekanizması olarak kullanma; içerik ölçümü, akış, oranlı kolonlar ve gerektiğinde kaydırma kullan.
- Genel bakış kartlarının asıl düğmeleri gerçek işlevleri açıp kapatır/çalıştırır; yalnız sayfa açmaz. Ayarlar ayrı bağlantıdan açılır. XYZ ve MobTP tek seferlik işlemlerdir; kullanıcı istemeden sürekli yazıcıya dönüştürme.
- Gerekli doğrulama tamamlanmadan işlemi uygulama/arm etme. Kısa uyarıyla ilgili fonksiyon sayfasına yönlendir; arka planda açılma isteği kuyruğa alınmaz.
- PlayerCounter genel bakıştan açılıp kapanan, eski görünümünde ayrı küçük topmost penceredir; ana pencere küçültülünce görünür kalır. Ana uygulama kapanınca sayaç da kapanır.
- SafeMode açıkken doğrulanmış Player Count >= 1 ise birleşik uygulamanın etkin işlemlerini kapanıştaki gibi geri al. Sayaç overlay'den bağımsız izlenir. Eksik okuma sıfır değildir. Başarısız geri alma durumunda işlemleri engelli tut; sayı tekrar 0 olduğunda işlevleri otomatik açma. Tamamlanmış XYZ/MobTP tek seferlik taşıma geçmişini geri alma vaadi verme.
- Multikill modülünde tekil AOB, exact SHA/profil/canlı oturum doğrulaması ve patch recovery kaydı zorunlu. UAC ve aynı oturumda sahiplik/geri alma denetimlerini her sürümde koru.
- Kullanıcının “Patch Recovery” dediği, OYUN güncellemesi sonrası kod yerini AOB + semantik fingerprint ile yeniden bulmaktır; uygulama çökmesi sonrası geri alma ile karıştırma. SHA değişince otomatik tarama yap, tekil semantik aday ve canlı profil doğrulaması olmadan patch açma. Bilinmeyen/çoklu eşleşmede eski RVA'yı kullanma. Oturum geri alma kaydını ayrıca koru.
- Player XYZ genel bakış kartında doğrudan X/Y/Z girişleri ve Işınlan düğmesi bulunur. Detay sayfasıyla aynı hedef taslağını kullanır, sayı filtresi ve tek seferlik yazım korunur.
- Genel bakış kartları kullanıcı tarafından sürüklenerek sıralanabilir; sıra kalıcıdır, yeni sürümlerde yeni kartlar eklenirken mevcut düzen korunur.
- Bu birleşik build'de AOE Research/Recovery ve Profiles sayfaları görünmez. Çalışma profili/oturum doğrulaması iç işleyişte korunur.

- Kullanıcı 4UnityTools EXE'sinin her sürümde Windows UAC ile yönetici yetkisi istemesini açıkça istedi.
- `src/app.manifest` içindeki `requestedExecutionLevel="requireAdministrator"` ayarını koru. Kullanıcı açıkça değiştirmedikçe `asInvoker` veya `highestAvailable` kullanma.
- Projedeki `RequireAdministratorManifest` denetimini ve `build.ps1` içindeki yayımlanmış EXE manifest doğrulamasını koru.
- Otomatik arayüz testleri geliştirme ortamındaki yönetilen DLL üzerinden çalışabilir; kullanıcıya dağıtılan EXE'nin UAC gereksinimini test kolaylığı için kaldırma.

- Açılışta “Tarama başlasın mı?” penceresi sun; kabul edilirse XYZ, Speed, Jump, Invisible, Aggro, MobTP, AOE, Counter, Multikill sırasıyla yalnız yolları doğrulasın. Tarama işlev açamaz, arm edemez, hotkey kaydedemez veya bekleyen patch geri almasını çalıştıramaz. Manuel doğrulama sırayı engellemez.
- Aynı SHA için kalıcı profili yerel imza/semantik kontrollerle yeniden kullan. SHA değişirse eski RVA’yı kullanma. PID, modül tabanı ve heap pointer’larını diske profil olarak kaydetme; yeni oturumda canlı yolu yeniden çöz. Bilinen build XYZ ve Speed/Jump için doğrulanmış global root yolunu kullanır; diğer build’lerde güvenilir yol yoksa heap çözümlemesi gerekebilir.

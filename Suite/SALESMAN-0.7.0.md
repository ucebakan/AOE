# Salesman — 4UnityTools 0.7.0

Genel bakışa **Salesman** kartı ve doğrudan çalışan **Salesman** butonu eklendi.
Tuş seçimi ve tuş gönderimi yoktur. Buton, doğrulanmış oyun penceresinin kendi
iş parçacığında oyunun NPC mağaza listesi isteğini Salesman kimliğiyle çalıştırır.
Satış ekranındaki **SELL TRASH**'a kullanıcı basar; uygulama otomatik eşya satmaz.

**Ayarlar ve doğrulama** bağlantısı ve Salesman sayfası, **Profili doğrula**,
**Salesman**, **Durdur / geri al**, **Patch Recovery · yeniden tara** kontrollerini
sunar. İlk kontrol eksikse işlem arka plana kuyruğa alınmaz. Yeniden doğrulandıktan
sonra butona tekrar basılır. Mevcut kart sırası korunur; yeni kart sona eklenir.

## Profil ve patch recovery

- Profiller `%LOCALAPPDATA%/4UnityTools/Salesman/profiles/<SHA>.json` altında.
- Aynı SHA için AOB/operand, RTTI ve metot bağlantıları tekrar doğrulanır; tam
  imza taraması yapılmaz. SHA değişirse tekil AOB adaylarından yeni profil üretilir.
- Root ve player yolu mevcut XYZ resolver'ından gelir. Session/shop/context/cash
  alanları, visibility metot slotu, sender, NPC resource lookup ve kapatma çağrısı
  kod operandlarından/bağlantılarından türetilir. Eski RVA yeni SHA'da kullanılmaz.
- Sender `0x506c` ve WORD NPC payload fingerprint'i; cleanup call ilişkileri;
  shop RTTI'si; visibility getter'ı; NPC ağacının desteklenen şeması doğrulanır.
- Eksik, çoklu, bozuk veya semantik olarak uyumsuz adayda işlev açılmaz.
- Salesman NPC kimliği 22631, mevcut doğrulanmış FileMerger paketine bağlıdır.
  Kaynak paketinin SHA'sı değişirse NPC kimliği yeniden analiz edilmeden işlev
  açılmaz. Her olası gelecekteki kod/kaynak değişimini kurtarma garantisi yoktur.
- PID, modül tabanı ve heap pointer'ları profil dosyasına yazılmaz. Oturuma ait
  bu bilgiler yalnız ayrı geri alma journal'ında tutulur.

## Açılış ve yaşam döngüsü

`UnitySalesman.dll`, birleşik EXE'ye gömülür ve mevcut içerik özetiyle çıkarma
mekanizmasını kullanır. Tek istek için WH_CALLWNDPROC pencere hook'u kurulup özel
mesaj gönderilir. Callback, PID/oluşturulma zamanı, son kullanım zamanı, image
kimliği, exact sender/finder kodu ve owner/player/session/shop bağlantılarını
oyunun UI thread'inde tekrar doğrular. Ardından NPC kaydını bulur ve oyunun kendi
istek metodunu çağırır. Hook istek sonrası kaldırılır. Klavye girişi, breakpoint,
debugger, thread duraklatma ve başka thread'de oyun metodunu çağırma yoktur.

Zaman aşımında callback hâlâ çalışıyor olabilir; küçük bridge DLL bu nedenle
oyun süreci içinde pin edilir ve oyun kapanana kadar yüklenmiş kalır. Aktif hook
bu süre boyunca tutulmaz. Uygulamanın native DLL'leri yine kendi işleminde bulunur.

Pencerenin hedef değişiminde kapanmasını önleyen tek UI frame sabiti geçici
değiştirilir (`11 → 12`; frame 12 zaten aynı cleanup yolunda ayrıca kapatılır).
NPC açma sabiti, skill davranışı, envanter veya koordinatlar değiştirilmez.
Pencere kapanınca/başka shop açılınca, world değişince, SafeMode devreye girince
veya uygulama kapanınca özgün kod ve RX koruması geri alınır. Ayrı gizli koruma
süreci hazır olmadan değişiklik yapılmaz. Parent ani kapanırsa koruma geri alır;
üst sınır 10 dakikadır. Başarısız geri alma durumunda sahiplik korunur ve yeni
işlem engellenir. Kapanış kuyruğu Multikill → Salesman → XYZ → Counter → MobTP →
Speed/Jump → Invisible/Aggro → AOE şeklindedir.

Açılış taramasına Salesman onuncu salt okunur kontrol olarak eklenmiştir.
Tarama pencere açamaz ve guardian/journal geri alma işlemi çalıştıramaz.

## Doğrulama

- Release derleme: sıfır hata / sıfır uyarı.
- 208 birleşik UI/güvenlik/yerleşim kontrolü geçti. Salesman dahil dar, normal,
  tam ekran ve %125/%150/%200 ölçek düzenleri kontrol edildi.
- Salesman disk testleri: aynı SHA cache, değişen SHA'da operandların yeniden
  türetilmesi, bağlı alanlarda uyuşmazlık, bozuk profil, eksik/çoklu AOB ve
  yanlış istek opcode'u reddi.
- Native bridge, uygulamanın kendi test penceresinde doğru thread'de bir kez
  çağrı ve yanlış PID/oturum/son kullanım zamanı reddi için sınanır. Ayrı süreç
  testi yalnız oluşturulan kendi fixture sürecine mesaj yollar.
- Aynı guardian yolu ayrı fixture süreçlerinde normal kapanış, hata, ani
  kapanış, sayfa koruması değiştikten sonraki ani kapanış ve süre dolumu için
  sınandı. Beş senaryoda özgün baytlar ve RX sayfa koruması geri alındı.
- Teknik testler oyun belleğine yazmaz ve oyunun metotlarını çağırmaz.
- Paketlenmiş EXE'nin salt okunur canlı kontrolü geçti: açık oyunun SHA,
  sender/finder, shop yolu ve Salesman NPC kaynak kaydı doğrulandı.
- Önceki ayrı araçtaki K üzerinden açılış ve satış kullanıcı tarafından başarılı
  bildirilmişti. Bu sürümün tuşsuz bridge yolunun canlı oyun sonucu ayrıca
  doğrulanmalıdır; teknik fixture başarısı canlı satış sonucu değildir.

Kayıtlar `%LOCALAPPDATA%/4UnityTools/Salesman/logs/salesman.log` altındadır.
Yeni EXE'yi açmadan önce eski 4UnityTools'u normal kapatma yoluyla kapat.
Windows yönetici UAC manifesti korunur.

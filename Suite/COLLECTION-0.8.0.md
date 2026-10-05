# Collection · 0.8.0

Genel bakıştaki **Collection · AÇ** düğmesi önce SHA/profil ve canlı oyun
yolunu doğrular, ardından otomatik toplamayı başlatır. **Collection · KAPAT**
durdurur. Tuş ataması yoktur. Ayarlar ve doğrulama bağlantısı ayrı sayfayı açar.

V7'de kullanıcı tarafından doğrulanan normal loot sender kullanılır. İzleme
50 ms aralıklarla canlı mob registry'sini okur. Yeni gözlenen canlı → ölü
geçişleri 64 üyelik gruplar halinde aynı oyun UI callback'inde işlenir. Hazır
kuyruk 64'ten fazlaysa sonraki grup araya timer beklemesi koymadan gönderilir.
Native ağ paketi her mob için tek DWORD kimliği taşımaya devam eder; yeni bir
çok-mob sunucu paketi icat edilmez. Mesafe sınırı ve hedef seçme şartı yoktur.

Araç öldüren oyuncuyu ayırmaz: kullanıcı tercihi doğrultusunda yeni ölen
gözlenebilir moblara normal loot isteği gönderir, loot hakkını sunucu kontrol
eder. Açılışta zaten ölü olan moblar atlanır. Client registry'sinde görülmeyen
veya iki gözlem arasında tamamen kaybolan moblar için toplama garantisi yoktur.
Gerçek Collection 9909 eşya buff'ı aktifken birlikte çalıştırılmaz.

## Profil ve oyun patch recovery

`%LOCALAPPDATA%/4UnityTools/Collection/profiles/<SHA>.json`, yalnız disk SHA,
RVA, operand, imza ve kod fingerprint bilgilerini tutar. PID, modül tabanı ve
heap adresleri profil olarak kaydedilmez. Aynı SHA profili doğrulanarak yeniden
kullanılır. SHA değiştiğinde tekil AOB, GetAll'ın chained-unwind method
bağlantısı, sender çağrıları, root/actor/registry/parent/buff şeması yeniden
çözülür. Kayıp, çoklu veya semantik olarak tutarsız adayda eski RVA kullanılmaz.

Collection sayfasındaki **Patch Recovery · yeniden tara**, gerekirse çalışan
Collection'ı önce durdurur ve yeniden çözümleme yapar. Ardından kendiliğinden
açılmaz. Başlangıç taraması da yalnız doğrular; loot isteği göndermez.

## Durdurma ve oturum

Her native üye çağrısından önce oturum, kod, ölüm, registry üyeliği ve buff
guard'ları tekrar okunur. Duplicate ID ve karma oturum grupları baştan
reddedilir. Stale hedef atlanır; oturum/kod/native hata kalan grubu durdurur.
Belirsiz/kısmi gruba otomatik retry yapılmaz. Harita, karakter veya process
kimliği değişince otomasyon durur; tekrar açıkça başlatmak gerekir.

SafeMode yeni istekleri keser. Kapanışta Collection çağrısı ve hook temizliği
tamamlanmadan sonraki modülün geri alması başlamaz. Bekleyen callback boyunca
mapping ve oturum mutex'i tutulur. Host process kapanmışsa callback yeni
üyeleri çağırmaz. V6/V7 ile ortak otomasyon mutex'i ikinci aracı engeller.

Log: `%LOCALAPPDATA%/4UnityTools/Collection/logs/collection.log`. Log'daki
"sent" native dispatch sayısıdır, sunucu loot başarı onayı değildir.

## Doğrulama

Fixture testleri gerçek oyuna istek göndermez. Native ABI, 64 üye, iptal,
deadline, host identity, duplicate/mixed session ve kısmi hata; canlı alan
guard'ları; cache bozulması; sender'ın yeni SHA'da başka RVA'ya taşınması;
150 ölümün 64/64/22 boşaltılması; corpse/respawn/despawn/ID reuse; SafeMode
ve bekleyen cleanup testleri kapsanır. Birleşik arayüz testleri Collection
kartı/sayfası, tarama ve kapanış sırası, dar/tam ekran ve DPI düzenini kapsar.
Dağıtılan EXE `requireAdministrator`, `uiAccess=false` manifestini korur.

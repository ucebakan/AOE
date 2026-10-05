# PlayerCounter Exit · 0.8.1

Oyun güncellemesinden sonra Exit hazır göründüğü halde tıklama reddediliyordu.
`CanExit()` güncel SHA için çözülmüş profili kullanırken `RequestExit()` son
canlı planı profil argümanı vermeden kuruyordu. Bu çağrı eski gömülü build'in
RVA ve kod guard'larına dönüyordu; güncel oyunda dispatch öncesinde reddedildi.

Tıklama yolu artık aynı `profile_.get()` ile Exit planını kurar. `PrepareExit`
API'sinde varsayılan profil argümanı kaldırıldı: üretim çağrısı profili açıkça
vermek zorundadır. Eski onaylı build desteği yalnız açık `nullptr` seçimiyle
korunur. SHA, canlı kod, root/context/vtable, process creation, executable image
ve son context kontrolü korunur. Otomatik Exit veya otomatik retry eklenmedi.

Native fixture testleri güncel dosyadan profili çözüp yerel sahte bellek üzerinde
doğru Exit RVA/root planını doğrular. Aynı güncel görüntünün eski profille
reddedilmesi hatayı yeniden üretir. Kod, vtable slotu, context değişimi ve
bağlantısız çağrı reddi de test edilir. Fixture'lar oyuna thread/Exit göndermez.
`--counter-exit-verify <report>` yalnız gerçek sayaç/Exit hazır olma yolunu okur;
karakter seçim ekranına geçiş yapmaz.

Yeni paket UAC `requireAdministrator`, `uiAccess=false` manifestini korur.

# Player XYZ ve ana görünüm — 0.9.1

**Kendi konumum · canlı** altında salt okunur X/Y/Z alanları bulunur. Ayrı
**Hedef konum · gideceğim yer** alanları ilk açılışta boştur. Canlı yenilemeler
hedefi doldurmaz veya değiştirmez. Hedefi elle girip **Işınlan** ile tek seferlik
işlem yapabilir, ad verip **Listeye ekle** ile saklayabilirsin. **Konumumu hedefe
kopyala** canlı konumu hedefe aktarır; kendi başına ışınlanma yapmaz. Eski JSON
nokta listeleri, otomatik son liste kaydı ve nokta yanındaki Git düğmesi korunur.

Ana pencerenin **Genel bakış** sayfası kaldırıldı; **Butonlar** ana görünümdür.
Araçlar menüsü, doğrulama, SafeMode ve PlayerCounter erişimi korunur.

XYZ'nin her açılışında büyük EXE'yi yeniden kopyalama/hash etme ve yeni .NET
süreci başlatma maliyeti normal kapatmada kaldırıldı. Kapat/X pencereyi gizler;
sonraki Aç aynı HWND/süreci görünür yapar. Hedef ve liste bu sırada korunur.
Gizliyken çocuk pencerenin koordinat sorgu zamanlayıcısı durur. Ana uygulamadaki
XYZ adaptörü yalnız istek geldiğinde okur. Ana uygulama kapanınca çocuk da çıkar;
ebeveyn süreç takibi beklenmeyen kapanışta da gizli çocuğun kalmasını engeller.
İlk açılış hâlâ süreç başlatır; pencere canlı oturum çözümünün bitmesini beklemeden
görünür olur. Açmak veya göstermek oyun belleğine yazmaz.

Child HWND kayıtları oturum token'ı ve gerçek süreç sahibiyle doğrulanır. Çocuk
oyun handle'ı açmaz; bütün teleport işlemleri ana uygulamadaki SHA/profil,
canlı oturum ve OperationGate kontrollerinden geçer. İki EXE de aynı
requireAdministrator / uiAccess=false manifestini kullanır.

Sentetik kontroller: başlangıçta boş hedef ve ayrı canlı kutular, hedefin canlı
okumadan etkilenmemesi, liste saklama/yükleme, tek seferlik ışınlanma ve SafeMode,
gizle/tekrar göster ve gerçek ayrı süreçte aynı PID ile yeniden açma, dar/geniş
pencere ve 125/150/200% ölçek. Testlerde canlı oyuna koordinat yazılmaz.

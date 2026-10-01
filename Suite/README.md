# 4UnityTools · Control Center 0.5

Genel bakıştaki düğmeler gerçek işlevleri çalıştırır. Player XYZ, Speed / Jump,
Invisible / Aggro, MobTP ve AOE aynı uygulamada yönetilir. PlayerCounter eski
görünümüyle ayrı, küçük, taşınabilir ve üstte kalan pencere olarak açılır.

Hazır uygulama: [Windows x64 ZIP](releases/0.5.0/4UnityTools-0.5.0-win-x64.zip). ZIP içindeki `4UnityTools.exe` doğrudan çalıştırılır. SHA-256 değerleri aynı klasördeki `SHA256SUMS.txt` dosyasındadır.
Dosya kendi .NET çalışma zamanını içerir; kurulum gerekmez.
İlk açılışta genel bakış ve kullanım bilgisi görünür. Üst araç barından bir araç
seçildiğinde mevcut kontrolleri içerik alanına yerleşir. Sekme değişimi araçları
kapatmaz; girilen değerler, açık özellikler ve atanmış kısayollar korunur.
Genel bakışta Speed, Jump, Invisible, Aggro, AOE ve PlayerCounter açılıp kapatılır.
XYZ ve MobTP düğmeleri tek seferlik işlem yapar. Ayarlar ayrı bağlantıdan açılır.
Doğrulama eksikse kısa bilgi penceresi ilgili sayfaya yönlendirir; işlem kuyruğa
alınmaz ve doğrulamadan sonra düğmeye yeniden basılması gerekir.

Kartlar pencere genişliğine göre 1–3 sütuna geçer; metinler ve kontrol satırları
içeriğe göre ölçülür. Dar/yüksek DPI ekranlarında kaydırma ile bütün içerik erişilebilir.
AOE'nin Research/Recovery ve Profiles sayfaları bu build'de kaldırılmıştır;
çalışma profili doğrulaması içeride korunur.

XYZ alanları ASCII rakam, başta eksi ve tek ondalık ayırıcı (`.` veya `,`) kabul
eder. Geçersiz yapıştırma eski değeri korur. Boş/geçici girişler düzenlenebilir;
yazma işleminden önce üç alanın da sonlu float32 sayı olması gerekir.

Dağıtılan EXE her sürümde `requireAdministrator` manifestiyle yönetici yetkisi
ister. Normal masaüstünden açıldığında Windows'un UAC ayarlarına göre onay veya
yönetici kimlik bilgisi ekranı gösterilir. Yönetici yetkisi zaten bulunan bir
işlemden başlatıldığında Windows tekrar onay göstermeyebilir.
PlayerCounter içindeki **Exit** oyuna yöneliktir; ana pencerenin kapatma düğmesi
4UnityTools'u ve küçük sayacı kapatır. Sayaç genel bakıştan veya kendi sağ tık
menüsünden kapatılabilir; ana pencere küçültülünce görünür kalır.
AOE sekmesinde bağlantı ve arm otomatik başlatılmaz.
İlk kez açılan diğer paneller mevcut araçların bağlantı/okuma davranışını kullanır.

## Derleme ve doğrulama

Windows x64, .NET 9 SDK, Visual Studio C++ Build Tools ve CMake gerekir:

```powershell
.\Suite\build.ps1
```

Betik iki yerel DLL modülünü derler, .NET uygulamasını tek dosya olarak yayımlar,
oyuna bağlanmadan yönetilen DLL üzerinden arayüz/doğrulama testlerini çalıştırır ve başarılı sonucu
projedeki sürüme ait `releases` klasörüne kopyalar. `-SkipTests` UI testlerini atlar.
Test raporu `evidence/suite-tests.json`; ekran görüntüleri aynı klasördedir.
Her derlemede kaynak manifestin yönetici yetkisi şartı denetlenir. Yayımlama ve
teslim kopyasının gömülü EXE manifesti `verify-uac.ps1` ile ayrıca doğrulanır;
şart kaldırılmışsa derleme/teslim betiği hata verir. `-SkipTests` bu denetimi atlamaz.

## Yapı

- `src/SuiteForm.cs`: ortak pencere, navigasyon ve kontrollü kapanış.
- `src/OverviewPanel.cs`: dinamik kartlar, bağımsız işlev düğmeleri ve durumları.
- `src/FeatureActions.cs`: doğrulama, eylem yönlendirme ve araç adaptörleri.
- `src/ToolWorkspace.cs`: araç sayfalarının yaşam döngüsü.
- `src/CounterOverlay.cs`: küçük sayaç penceresinin yaşam döngüsü.
- `Controls`: paylaşılan ölçüme dayalı WinForms yerleşim bileşenleri.
- Her C# aracının `SuiteIntegration.cs` dosyası: mevcut işlevleri ortak arayüze bağlayan API ve yerleşim.
- `src/Palette.cs`: antrasit/platin, lavanta ve mavi tasarım.
- `src/NativeTool.cs`: C++ panellerinin aynı işlem içinde barındırılması.
- `native/CMakeLists.txt`: AOE ve PlayerCounter kaynaklarından DLL üretimi.
- `native/aoe_layout.inc`: birleşik build için ölçüme dayalı AOE yerleşimi.
- C# araçları proje referansları ile kullanılır; ayrı EXE başlatılmaz.
- Yerel DLL'ler ve AOE profilleri tek EXE'ye gömülüdür. DLL'ler Windows tarafından
  yüklenebilmek için `%LOCALAPPDATA%/4UnityTools/native` altına içerik özetiyle çıkarılır.
- AOE profil/belgeleri ve Suite'e ait ayarları `%LOCALAPPDATA%/4UnityTools/AOE`
  altında bulunur. XYZ ve Speed/Jump önbellekleri kendi alt klasörlerindedir.
  Invisible/Aggro ve MobTP mevcut kullanıcı ayar/geri-alma konumlarını kullanır.

Ortak pencere ve çevrimdışı testler, canlı oyunda bütün özelliklerin birlikte
çalıştığının doğrulanması anlamına gelmez. Canlı oyun testi ayrıca yapılmalıdır.

## SafeMode ve Multikill

SafeMode genel bakıştan açılır. PlayerCounter penceresinden bağımsız salt okunur
sayaç yaklaşık 400 ms aralıkla örneklenir. Count 0 ise müdahale etmez; >=1 veya
okunamayan sayaç etkin araçları kapanıştaki geri alma yollarıyla durdurur. Yeni
UI/kısayol işlemleri engellenir. Bir geri alma başarısızsa diğer araçlar yine
kapatılır, başarısız işlem yeniden denenir ve engel korunur. Count tekrar 0
olduğunda araçları kullanıcı yeniden açar; otomatik etkinleştirme yapılmaz.
Tamamlanmış XYZ/MobTP taşıma geçmişi geri alınmaz. SafeMode bu uygulamanın
araçlarını yönetir; başka bağımsız EXE'lere kumanda etmez.

Multikill, genel bakış düğmesi ve ayrı fonksiyon sayfasıyla bütünleşiktir.
**Profili doğrula**, **AÇ/KAPAT** ve **Patch Recovery** kontrolleri bulunur.
Aynı kaynak ayrıca [Multikill 1.2.0 ZIP](../Multikill/releases/1.2.0/4UnityMultikill-1.2.0-win-x64.zip) olarak yayımlanır.
Profil/kurtarma ayrıntıları `../Multikill/README.md` içindedir.

## 0.4: Ana ekran koordinatları ve düzen

Player XYZ kartındaki X/Y/Z alanlarını doldurup **Işınlan** düğmesine bas.
Sayı filtresi ve mevcut canlı profil/oyuncu doğrulaması korunur. Detay sayfası
ile ana kart aynı hedef değerlerini kullanır; boş/geçersiz sayılarda düğme pasiftir.

Kartın sağ üstündeki **↕** tutamacını başka bir kartın üzerine sürükle.
Sağ tık menüsüyle öne/arkaya veya en başa/sona taşıma da yapılabilir.
Düzen `%LOCALAPPDATA%/4UnityTools/card-order.json` içinde saklanır.
Pencere genişliği değiştiğinde sütun sayısı değişir, kart sırası korunur.

Multikill **Patch Recovery**, artık oyun güncellemesinden sonra AOB/semantik
fingerprint ile adresi yeniden bulmayı ifade eder. SHA değişirse profil
kontrolü sırasında otomatik taranır; **Yeniden tara** düğmesi de aynı yolu çalıştırır.
Tarama oyuna yazmaz ve işlevi kendiliğinden açmaz. Yeni profil canlı olarak
onaylanınca kullanıcı AÇ düğmesine basar. Çoklu/eksik aday yeni analiz gerektirir.

## Açılış taraması ve kalıcı profiller (0.5)

Açılışta **Tarama başlasın mı?** penceresi gösterilir. **Şimdi değil** hiçbir kontrol başlatmaz. Kabul edilirse XYZ, Speed, Jump, Invisible, Aggro, MobTP, AOE, PlayerCounter ve Multikill sırasıyla kontrol edilir. **Tarama / durum** düğmesi sonuçları tekrar açar. İptal, mevcut salt okunur kontrolün tamamlanmasından sonra kalan sırayı durdurur.

Tarama yalnız okur: arm/toggle, teleport, overlay, hotkey veya crash journal geri alması çalıştırmaz. AOE için SHA eşleşen profil dosyası aranır; ATTACH NOW ve gerekiyorsa Live Validation işlev sayfasında tamamlanır. Bu satırın manuel beklemesi diğer kontrolleri durdurmaz. Açık modüller kendi oturumlarının mevcut durumunu bildirir.

Aynı SHA için kayıtlı AOB/offset profilleri yerel kod kontrolleriyle yüklenir. PID, module base ve heap pointer saklanmaz. Mevcut bilinen SHA'da XYZ ve Speed/Jump doğrulanmış global root yolunu kullanır; eski heap taraması tekrarlanmaz. Bilinmeyen build'de sabit yol kanıtlanamadığında heap çözümlemesi gerekebilir. Multikill yeni SHA'da tekil semantik tarama yapar, sonraki bağlantılarda kayıtlı konumu sınırlı semantik kontrolle doğrular.

Tarama kabul edilmişse ve araç panelleri henüz yüklenmemişse yeni oyun oturumu 10 saniyelik aralıklarla fark edilir. Oyun yüklenirken bekleyen canlı kontroller en fazla iki dakika yeniden denenir; sonra Tarama / durum ile yeniden denenebilir. Yüklenmiş araçlar kendi bağlantı kontrollerini kullanır. Fonksiyonlar bu akışta kendiliğinden açılmaz. AOE'nin oturuma özel manuel doğrulaması SHA aynı olsa da atlanmaz.

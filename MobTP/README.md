# MobTP 2.0 — 4Unity

MobTP.exe ayrı bir x64 WinForms uygulamasıdır. .NET 9 Windows Desktop runtime gerekir
(Observer ile aynı runtime). Managed bağımlılıklar tek EXE içine paketlenir.
Yönetici olarak açın; birden fazla görünür TClient varsa işlem reddedilir.

## Kullanım

1. Oyuna girin, MobTP.exe'yi açın. Oyuncu XYZ ve yüklenen mob sayısı otomatik güncellenir.
2. İsterseniz "Arka plandaki mob listesini göster" kutusunu açın.
3. Çevre mesafesi varsayılan 2 birimdir (1–10). Mob hedefleri bu yarıçapta XZ halkasına
   dağılır, Y oyuncudan alınır. Bu geometrik yerleşimdir; zemin/engel veya model boyutu testi yapılmaz.
4. "UYGUN MOBLARI YANIMA GETİR" düğmesine basın. Düğme bir batch yapar;
   otomatik/sürekli teleport yoktur. Atanabilir Windows kısayolu da aynı batch yolunu çağırır. Batch sırasında sabit durun.

Her haritada istemcinin yüklediği registry üyeleri yeniden okunur. Bu, sunucudaki bütün
harita moblarını keşfetmez. Observer araştırma capture'ı veya öğrenilmiş merkez gerektirmez.

## Filtre ve yazma

- Kullanıcı seçimiyle Home = Actor+12D8/12DC/12E0 güncel XYZ.
- Player = mevcut doğrulanmış PlayerXYZ profili, A koordinatları.
- Hem Player→Home XZ hem Target→Home XZ **<=50** olmalıdır.
- Ayrıca Current→Player 100 birim sınırı yoktur; kullanıcı yalnız Home filtresi istedi.
- Sınır içindeki oyuncuya rağmen halkanın hedefi sınırı aşıyorsa o mob atlanır.
- Mobları oyuncunun koordinatına yığmaz; hedef en az 0.9 XZ uzaklık kontrolünden geçer.
- Home alanına yazılmaz. Rapordaki Eternity sırası uyarlanır: profilde çözülen B alanına 12 byte XYZ,
  ardından A alanına 12 byte XYZ (mevcut build: +B0, +70). Her API sonucu/byte sayısı kontrol edilir.
- İki yazma atomik değildir. Kısmi hata veya B sonrasında doğrulama kaybında batch durur;
  eski bir pointer'a kör geri alma yapılmaz. Önceki başarılar geri alınmaz.

Salt-okunur izleme için ayrı handle, düğmenin batch'i için kısa ömürlü write handle açılır.
ALL_ACCESS, injection, koruma değiştirme, remote thread, paket gönderimi yoktur.
Paketteki başlangıç SHA: 9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28.
Dosya hash'i, live player signatures, owner/player vtable, monster RTTI/key/node/registry
bağlantısı kontrol edilir. Batch listesi başlangıçta sabitlenir; yeni gelen mob eklenmez.
Yazma öncesinde/iki yazma arasında/sonrasında üyelik, oyuncu ve Home tekrar okunur.
Oyuncunun pointer/ID'si değişirse veya 0.25 3D birimden fazla hareket ederse işlem durur.
Oyuncu/Home okunamazsa, sıfır veya nonfinite ise yazma yapılmaz. Session kapanması,
root/registry değişmesi ve iptal de durdurur. Read/check/write atomik değildir; ABA ve
son kontrolden sonraki yarışlar tamamen elenemez.

## Sonuçlar

%LOCALAPPDATA%\MobTP\Logs\tp-*.jsonl her batch'in seçimini, her mob için yazma niyetini,
API sonuçlarını ve anlık A/B geri okumasını saklar. READBACK_MATCH yalnız istemci belleğinin
hedefle eşleşmesidir; sunucunun kabulü veya sonraki saldırı/menzil davranışı kanıtı değildir.
4Unity'de canlı coordinate write henüz test edilmedi. A/B'ye birlikte yazma seçimi Eternity
raporunun uyarlamasıdır; güncel oyunun bunu koruyacağı henüz doğrulanmadı.

## Doğrulama

dotnet build tools/MobTP/MobTP.csproj -c Release
dotnet publish tools/MobTP/MobTP.csproj -c Release -o dist/MobTP

--self-test: 20 sentetik/Windows hotkey kayıt filtre/yerleşim/guard/partial-write/readback testi; process yazmaz.
--ui-test: sentetik arayüz resmi üretir; oyuna bağlanmaz.
--probe: yalnız salt-okunur bağlantıyı dener, probe.json üretir; teleport yapmaz.

Uygulama eski Observer'ı başlatmaz. Proje referansı doğrulanmış read-only resolver/identity
kodunu paylaşmak içindir; kaynakta bu erişim MobTP assembly'sine sınırlandırılmıştır.


## Oyun içi tuş atama

Tuş kutusuna tıklayıp istediğiniz tuşa veya Ctrl/Alt/Shift kombinasyonuna basın,
"Tuşu ata" düğmesine basın. "Kaldır" kaydı iptal eder. Ayarlar
%LOCALAPPDATA%\MobTP\settings.json içinde saklanır. Kısayol Windows RegisterHotKey ile
MOD_NOREPEAT kullanır; basılı tutmak otomatik tekrar üretmez. Başka uygulamayla çakışırsa
hata gösterilir ve önceki atama korunur. Kısayol Windows genelinde kaydedilir fakat TP
sadece seçilmiş 4Unity PID'sinin penceresi ön plandayken başlar. Oyun odağı işlem sırasında
kaybolursa sonraki yazma engellenir. Form küçültülebilir; tuş kaydı form kapanınca kaldırılır.
Aynı anda iki batch çalışmaz. Tuş/düğme kullanımı logda ayrı kaydedilir.

## Sayaçlar

Yüklenen mob, Home<=50 olan ve Teleport koşullarını sağlayan sayıları ayrı gösterilir.
Sonuncusu hem oyuncu-Home hem hesaplanan halka hedefi-Home mesafesini ve merkezden
uzaklığı kontrol eder. UI ve gerçek batch aynı Placement.Plan fonksiyonunu kullanır.
Çevre mesafesi değişince sayı da yenilenir. Sayaç son okumaya aittir; gerçek yazma öncesi
tekrar kontrol edildiğinden hareket eden veya kaybolan mob sonradan atlanabilir.

## Speed uygulamasından uyarlanan profil ve patch koruması

SPEED İNCELEME konuşması ve tools/SpeedJump/Profile.cs, Session.cs incelendi.
MobTP artık sabit SHA reddi ve Observer'ın sabit offset resolver'ına bağlı değildir.

- Kalıcı profiller: %LOCALAPPDATA%\MobTP\Profiles\<SHA256>.json. Runtime heap pointer
  saklanmaz. Ayarlar ile build profilleri ayrıdır; tanı logu Logs/profile.log dosyasındadır.
- Mevcut build profili EXE'ye gömülüdür. Kayıtlı/paket profilinde SHA, PE64 timestamp/image
  size, signature pencereleri, RTTI ve instruction operandları doğrulanır; AOB taranmaz.
- Yeni SHA: PlayerXYZ'nin owner/A/B imzaları ile MobTP root_a/root_b, host, constructor,
  home, factory imzaları executable bölümlerde tekil aranır. Registry lookup, host/factory
  çağrı hedeflerinin eşleşmesinden alınır; registry_shape layout'u sabit opcode deseniyle
  doğrulanır. İki root aynı global slota, factory aynı constructor'a ulaşmalıdır.
- Actor ID/type, Home, allocation size, registry, root ve player A/B alanları instruction
  operandlarından çıkarılır. Monster/Player coordinate setter vtable ilişkileri doğrulanır.
  Compiler/register/layout değişimi bu yapıyı bozarsa çözüm reddedilir; eski offsetler
  yeni SHA'ya körlemesine taşınmaz. Başarıda profil atomik kaydedilir.
- Canlı PE header, relocation-aware RTTI/vtable ve diskle birebir code-window kontrolü
  yapılır. Oturum/PID creation time ve root/registry/kimlik her işlemde doğrulanır.
- Başarısız çözüm aynı SHA için sürekli tekrarlanmaz; "Profili yeniden kontrol et"
  düğmesi başarısız çözüm cache'ini temizler. Doğrulanmış profil cache'i değişmez.
- Bu yapı yeni build davranışının eşdeğerliğini garanti etmez. Çözülemeyen veya belirsiz
  build'de buton/kısayol üzerinden yazma yapılmaz. Canlı doğrulama olmadan disk profilinin
  bulunması tek başına teleport izni değildir.
- Speed'in JUMP NOP patch'i ve recovery journal'ı aktarılmadı: MobTP kod patch'i yapmaz.
  Kısmi veri yazımını gelecekteki oturumda eski heap adresine restore etmek uygulanmaz.

Doğrulama: 20 işlev/Windows hotkey testi, 11 profil testi; mevcut binary doğrulaması,
yapay yeni SHA çözümü, bozuk/çift/root-çelişkili imzaların reddi, live-code uyuşmazlığı,
ASLR simülasyonu, bozulmuş profil ve cache kontrolü. --profile-test diskteki mevcut
TClient.exe'yi kullanır; oyuna yazmaz. UI sentetik olarak görüntülendi. Gerçek gelecekteki
patch veya oyun içi hotkey/teleport davranışı bu testlerle kanıtlanmış değildir.

## Depodan derleme

Bu klasörden `./build.ps1` ile derleyin; çıktı `build/MobTP.exe` olur.
`./build.ps1 -SelfTest` işlev ve Windows hotkey testlerini de çalıştırır.
.NET 9 SDK gerekir. Paylaşılan Observer/PlayerXYZ kaynakları bu klasörde yer alır.
Hazır paket: [MobTP 2.0.0](releases/2.0.0/MobTP-2.0.0-win-x64.zip).
Teslimdeki test raporları ve arayüz görüntüsü `evidence/` içindedir.

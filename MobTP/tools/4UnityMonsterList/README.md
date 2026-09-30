# 4Unity tek-mob gözlem kaydı

## 1.7 Seçilen Home alanı ve radius kontrolü

Kullanıcı kararıyla Actor+12D8/12DC/12E0 artık operasyonel Home XYZ kaynağıdır.
Güncel bellek değeri her örnekte okunur; dönüş beklenmez ve öğrenilmiş merkeze fallback
yapılmaz. Alan mesajlarla değişebilir; bu seçim kalıcı spawn semantiği kanıtı değildir.
Kayıt penceresinde radius XZ varsayılan 50'dir; kayıt başlamadan değiştirilebilir.
Oyuncu A konumu ile güncel Home arasındaki XZ mesafesi radius'a eşit veya küçükse
İÇERİDE, büyükse DIŞARIDA gösterilir. Eksik, sıfır/nonfinite veri veya geçersiz mob
kimliğinde sonuç BİLİNMİYOR olur. Öğrenilmiş dönüş merkezi ayrı gösterilmeye devam eder.

home-profile.json seçimi ve radius'u; timeline JSON Home alanı ve CSV home_json hücresi
her örneği; report.json OPERATIONAL_HOME son durumu saklar. Sürüm salt okunurdur;
bu mesafe kontrolü teleport veya server acceptance testi değildir.
62 sentetik kontrol geçti; canlı radius UI davranışı henüz denenmedi.

## 1.6 Tarama kapsamı düzeltmesi

v1.5'teki 64 blok/128 sorgu sınırı erken offsetlerde doluyordu. v1.6 her sample'da
Actor[0,1320) içindeki 612 adet 8-byte hizalı alanın tamamını değerlendirir; uygun
pointer adaylarının hepsi sorgulanır. En fazla 612 blok × 512 byte okunur. Her okunan
blok için region tekrar sorgulandığından toplam VirtualQueryEx çağrısı en fazla 1224'tür.
Tarama örnekler arasında döndürülmez: geç alanlar da aynı zaman çizelgesinde sınanır.
Region filtreleri, bağlantı kontrolleri ve her sample öncesi/sonrası mob kimlik kontrolü korunur.

Timeline `LinkedMemory.Slots` her uygun kaynak offset için adres ve sonuç içerir:
QUERY_FAILED, REGION_FILTERED, REGION_REMAINDER_TOO_SHORT, link/region değişimi,
UNREADABLE veya STABLE_LINK_CANDIDATE_ONLY. `Queries/EligibleSlots` UI'de gösterilir.
`linked-home-analysis.json/coverage` sorgulanan kapsamı ve offset başına sonuç sayılarını
raporlar. Eski kayıtlarda Slots yoksa kapsam tamamlandı sayılmaz. Sorgulanmış alan,
okunabilir alan veya moba ait nesne ile aynı şey değildir.

512-byte prefix, tek seviye ve aday doğrulama sınırları devam eder. Kayıt boyutu ve okuma
süresi artabilir; gerçek read_duration/interval verileri saklanır. Canlı performans henüz
doğrulanmadı. Bu düzeltme geçici sıfır oyuncu koordinatının kaynağını çözmez; mevcut sıfır
koordinatı mesafe hesabından dışlama davranışı korunur.

Doğrulama: 13 linked-memory, 20 capture, 14 learned-center, 5 file, 6 UI kontrolü geçti.
Yeni fixture 612 okunabilir slotun tamamını ve son +1318 alanını, erken 611 sorgu başarısız
olduğunda son alanın yine okunmasını ve geç alan adayının analizde korunmasını sınar.

Kullanım: v1.6 klasöründeki EXE'yi yönetici olarak açın. Offset kutusunu boş bırakın;
aynı mobla tek kayıtta iki farklı yönlü takip–dönüş yapıp kaydı bitirin.
Önceki kayıtlarda okunmamış bölgeler geriye dönük tamamlanamaz.

## 1.4 Öğrenilmiş dönüş merkezi

Listeden mob seçin → **Seçili mob — merkez öğren / kaydet** → offset kutusu boş → **Kaydı başlat**.
İlk tamamlanan dönüşten sonra merkez XYZ, mob→merkez XZ ve oyuncu→merkez XZ görünür.
İkinci tutarlı tamamlanmış dönüşte **TEKRAR TEYİT EDİLDİ** gösterilir.
Normal takip/goal değişimi öğrenilmiş değeri ezmez. Yeni dönüşte farklı hedef görülürse eski merkez
hemen silinir; yeni merkez tamamlanmış dönüşle yeniden öğrenilir. Kimlik/veri/süreklilik koparsa silinir.

Merkez yalnız bu açık kayıt boyunca kullanılır. Yeni kayıt/yeni process/yeni mob için sıfırdan öğrenilir;
dosyadaki eski merkezi canlı oyuna geri yükleme yoktur. Kaydı durdurunca görünen değerler son örnektir.
Oyuncu kaynağı doğrulanamazsa yalnız oyuncu mesafesi boş kalır. Koordinatlara yazım ve radius filtresi yoktur.

Kriterler: gözlenen 0→1→0, en az 3 flag=1 örneği, en az 500 ms dönüş, sıfır follow ID/type,
hedefin 0.1 3D içinde sabit kalması ve bitişte XZ<=1 / Y farkı<=3. İki örnek arası >2000 ms
veya geriye giden zaman sürekliliği keser. Bunlar ihtiyatlı öğrenme kriterleridir; leash eşiği değildir.
Durumlar otomatik gameplay olay etiketine dönüştürülmez. Öğrenilmiş merkez kalıcı Home offseti değildir.

CSV/JSON her sample'da `LearnedCenter` durumunu ve PlayerA koordinatını içerir; report.json sonunda
`LEARNED_RETURN_CENTER` saklanır. Eski kayıtlarda PlayerA olmadığından replay oyuncu mesafesi üretmez.

`--center-test`: 14 sentetik durum kontrolü.
`--center-replay <capture-directory>`: hedefe bağlanmadan eski zaman serisini aynı öğreniciyle oynatır;
çıktı `learned-center-replay.json`. Ham kayıt değiştirilmez, canlı cache doldurulmaz.

## 1.3 Home araştırması

Home araştırması kayıt başlatıldığında otomatik açıktır. Elle offset girmeniz gerekmez.
+12D8/+12DC/+12E0 hareket hedefi ve +12FC dönüş durumu ADAYLARI, takip alanları ve
Actor [0,1320) ham baytları her sample'ın identity kontrolleri arasında okunur.
JSON'daki Research alanı ham base64 veriyi içerir; CSV research_state_json ham blok hariç
aynı hedef/durum bilgisini içerir. Kalıcı Home bulunduğu iddia edilmez.
Kayıt bitince home-analysis.json son konuma yakın sabit inline XYZ adaylarını çıkarır.
Bu yalnız aday elemesidir; hareket hedefi veya sabit alan tek başına Home sayılmaz.
Önceki kayıtlar bu yeni alanları içermediği için yeni takip–dönüş kaydı gereklidir.

## 1.2 kayıt sonlandırma düzeltmesi

Writer handle'ları JSON dönüşümünden önce kapatılır. Kurtarma okuyucusu açık writer ile
uyumlu FileShare.ReadWrite kullanır. Final JSON geçici dosyada doğrulanıp tamamlanır;
başarısız sonlandırma yeniden denenebilir. Ham JSONL/CSV değiştirilmez.
`--file-test` eski paylaşım hatasını, açık writer kurtarmasını, kilitli hedefi, yeniden denemeyi
ve bozuk satır karşısında tamamlanmış çıktının korunmasını sentetik dosyalarla sınar.
Durmuş eski kaydı kurtarma: `dotnet 4UnityMonsterList.dll --recover "<kayıt klasörü>"`.

## 1.1 arayüz ve mesafe düzeltmesi

Liste artık her yenilemede silinip oluşturulmaz; satır kimlikleri, seçim ve kaydırma korunur.
Yenileme 750 ms; Listeyi duraklat / Bir kez yenile düğmeleri eklendi. Durum paneli kaydırılabilir.
Mesafeler A–A ve B–B olarak ayrı gösterilir; sıfır koordinat kaynağından mesafe üretilmez.
Oyuncu kaynağı PlayerXYZ'nin aynı SHA profiliyle disk ve canlı imza/vtable kontrollerinden geçer.
Profil doğrulama kodu paylaşılır; PlayerXYZ'nin writer/session kodu dahil değildir.
Tanı snapshot'ı oyuncunun A/B değerlerini, adreslerini ve ham 12-byte verilerini içerir.
Bu sürümde bilinmeyen build hâlâ reddedilir; mob profili için otomatik patch recovery yoktur.

30 Eylül 00:31 snapshot'ında P=0x2A5584B8030 ve B=(0,0,0) kaydedilmiştir.
29 mob için 7154..7255 değerleri oyuncuya değil, bu sıfır koordinatına uzaklıklardır.
Bu P, SPEED İNCELEME konuşmasındaki aynı PID kaydıyla eşleşir. B'nin o anda neden sıfır
olduğu eski snapshot'tan belirlenemez; yeni ham A/B snapshot'ı ile karşılaştırılmalıdır.

Bu araç hedef process belleğine yalnız okuma erişimi açar (`0x1010`: QUERY_LIMITED_INFORMATION | VM_READ).
Teleport, coordinate write, debugger, injection, thread suspension veya input otomasyonu içermez.
Mevcut uygulamanın administrator manifesti korunmuştur. Windows x64 ve .NET 9 Desktop runtime gerekir.

## Kullanım

1. Oyunda gözlemlemek istediğiniz mobun bulunduğu sahneyi açın.
2. `4UnityMonsterList.exe` içinde bir satır seçin ve **Seçili mob — salt okunur zaman kaydı** düğmesine basın.
3. Bilinen Home aday offsetleri varsa hex biçiminde virgülle girin. Bilinmiyorsa boş bırakın.
   Varsayılan veya keşfedilmiş bir Home offseti YOKTUR. Bu sürüm otomatik Home taraması yapmaz.
   Aday girişleri Actor'a göre offsettir; gerçek Home oldukları iddia edilmez.
4. **Kaydı başlat** düğmesine basın. PID/session, exact client path, bilinen SHA ve seçilmiş
   registry key/node/Actor bağı doğrulanır. Tam RTTI adı `.?AVCTClientMonster@@` olmalıdır.
   RTTI çözülemezse kayıt reddedilir; doğrulamayı atlayan seçenek yoktur.
5. Oyunda takip ve geri dönüş hareketlerini kendiniz gerçekleştirin. Gerçekten gözlediğiniz T0–T6
   olayını seçip **Gözlenen olayı işaretle** düğmesine basın. İsteğe bağlı not ekleyin.
   Her tıklama UTC ve monoton zamanla ayrı kaydedilir. Olaylar otomatik üretilmez, sonraki örneğe
   taşınan etiketin kendi olay zamanı korunur. Son örnekten sonraki etiketler events.jsonl içinde kalır.
6. **Kaydı bitir** düğmesine basın. Pencereyi kapatmak da kaydı sonlandırır.

Hedef 100 ms aralıktır; gerçek örnek aralıkları ve okuma süreleri kaydedilir. Üst sınır 36000 örnektir.
UI/Windows zamanlaması örnek atlamasına yol açabilir. Yeniden başlamak için yeni kayıt penceresi açın.

## Kimlik ve veri kuralları

- Her örneğin öncesinde ve sonrasında owner/registry binding, seçili key'in ağaçta bulunması,
  node/Actor eşleşmesi, actor ID, type=2 ve x64 RTTI doğrulanır.
- Arama yolu bounded/cycle-aware'dir; parent ve key sıralaması kontrol edilir. Bu, bütün registry'nin
  red-black renk/black-height/count doğrulaması değildir.
- Kimlik uyuşmazlığı veya session kaybında kayıt biter. Başka Actor'a rebind yapılmaz.
- A (+70/+74/+78) ve B (+B0/+B4/+B8) yalnız koordinat ADAYLARIDIR.
- A/B okunamaz veya sonlu/geçerli aralıkta değilse kayıt biter. Okunamayan Home adayları null ve
  açıklayıcı status ile tutulur; sıfır koordinat uydurulmaz.
- Son doğrulaması başarısız örnek `Valid=false` ile tanı amaçlı korunur. Kanıt olarak kullanılmaz.
- Okumalar atomik değildir. İki kontrol arasında nesnenin silinip aynı kimlikle aynı adreste yeniden
  oluşturulması (ABA) tümüyle dışlanamaz. RTTI tek başına ömür garantisi değildir.
- Sabit XYZ, Home kanıtı değildir. Birden fazla farklı yönlü takip–dönüş döngüsü gerekir.

## Çıktılar

Her kayıt ayrı klasördedir:
`%LOCALAPPDATA%\4UnityMonsterList\Captures\<UTC-time>-<unique-id>`

- `metadata.json`: build/session, sabitlenen kimlik, aday offsetler ve doğrulama sınırları.
- `timeline.jsonl`: her örnek anında flush edilir; beklenmedik kapanmada önceki satırlar korunur.
- `timeline.csv`: kimlik, üyelik, RTTI, A/B XYZ, A↔B 3D mesafesi, XYZ deltaları ve 3D değişim miktarı;
  her Home adayı için XYZ, okuma durumu, A/B'ye 3D ve XZ mesafeleri; zaman damgalı olay/not JSON'u.
- `events.jsonl`: yalnız manuel gözlem olayları; sample ile olay zamanı ayrı tutulur.
- `timeline.json`: normal sonlandırmada JSONL'den oluşturulan JSON dizisi.
- `report.json`: DIRECT_OBSERVATION / INFERENCE / UNTESTED ayrımı. Otomatik rol çıkarımı yapılmaz.

Ani kapanmada JSONL ve CSV kullanılabilir; final JSON/rapor oluşmamış olabilir.
JSON içindeki adresler ulong sayılardır; JavaScript Number'a dönüştürürken hassasiyet sınırına dikkat edin.
CSV adresleri hex metin olarak da saklar.

## Doğrulama

```powershell
dotnet build tools/4UnityMonsterList/4UnityMonsterList.csproj -c Release
dotnet tools/4UnityMonsterList/bin/Release/net9.0-windows/win-x64/4UnityMonsterList.dll --self-test
```

Self-test yalnız sentetik bellek fixture'ı kullanır, oyuna bağlanmaz. Sonuç executable yanına
`capture-self-test.json` olarak yazılır. Canlı mob hareketi, Home doğrulaması ve server acceptance
bu testlerle kanıtlanmaz.
# v1.5 — bağlı bellek adayları

Güncel SHA'da RVA 44CDA, allocation için 0x1320 byte ister; ardından 44CF0,
8422E0 CTClientMonster constructor'ını çağırır. Bu nedenle mevcut Actor[0,1320)
kaydı bu oluşturma yolunun nesne boyutunun tamamıdır. Eternity +8AFC bu nesnenin dışındadır.
Manuel XYZ offsetleri artık 0..1314 hex ile sınırlıdır.

Her sample, Actor içindeki 8-byte hizalı pointer adaylarından tek seviyeli ek okuma yapar.
Yalnız committed/private/readable, executable veya guard olmayan bölgeler kabul edilir.
En fazla 128 pointer adayı sorgulanır; en fazla 64 blok okunur. Kabul edilen bloklarda
bir ek region sorgusu yapılır. Her blok pointer'dan başlayan en fazla 512 byte veya kalan
region uzunluğudur; nesne sınırı kanıtı değildir. Aynı Actor içine işaret eden adresler atlanır.
Pointer ve region değişirse ham blok kabul edilmez; tüm okumalardan sonra mob kimliği
tekrar doğrulanır. Pointer hedefi okunamaması ana kaydı durdurmaz, hata olarak saklanır.

`LinkedMemory` alanı timeline.jsonl/json içindedir; CSV'ye büyük ham bloklar eklenmez.
`linked-memory-rules.json` sınırları, her örnekte `Truncated` kapsamın sınıra takıldığını belirtir.
Kayıt bitince `linked-home-analysis.json`, iki dönüşle teyit edilmiş son merkeze 0.5 birim
yakın, tüm geçerli örneklerde aynı linkte kalan ve 0.05'ten fazla değişmeyen XYZ adaylarını
listeler. Arama contiguous float32 XYZ için her byte hizasında yapılır. Kayıp veya değişen
link sabit kabul edilmez. Bu eşikler radius değildir. İki dönüş yoksa sonuç yetersiz veri olur.

Pointer gibi görünen sayı gerçek pointer olmayabilir; erişilebilirlik veya sabitlik,
o bölgenin moba ait olduğunu/Home olduğunu kanıtlamaz. Derin pointer zincirleri, 512 byte
sonrası, split/encoded koordinatlar bu sürümün kapsamı dışındadır. Boş sonuç yokluk kanıtı değildir.
Kayıtlar v1.4'ten daha büyük olabilir. Canlı performans ve Home adayı henüz doğrulanmadı.

Kullanım: eski EXE'yi kapatın, v1.5'i yönetici olarak açın, tek mob seçin, manuel offset
kutusunu boş bırakın. Aynı kayıt içinde iki farklı yönde takip–dönüş tamamlayıp kaydı bitirin.
Arada mob değiştirmeyin. Oyuna yazma, hook, paket gönderimi veya teleport yoktur.

`--linked-test`: sentetik pointer değişimi, region sınırı/koruması, bütçe, okunamayan hedef,
nesne dışı offset reddi ve kayıp/rebound linklerin aday analizinden elenmesini sınar.

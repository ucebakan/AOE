# Captcha EXE v1.1 — tam kaynak kodu ve çalışma raporu

Rapor tarihi: **7 Ekim 2026, Europe/Istanbul**. İncelenen uygulama: **4UnityPuzzleTest 1.1.0**, bağımsız Windows x64 programı. Bu belge önceki teslimin kaynak ve kanıt arşividir; yeni oyun testi veya yeni EXE sürümü değildir.

## İçindekiler

1. Amaç ve teslim kapsamı
2. Bulmaca kuralları ve örnekler
3. Sürüm farkları
4. Mimari ve kaynak dosyaları
5. Başlatma ve istemci bağlantısı
6. Otomatik pencere bulma
7. OCR ve eşleştirme
8. Dört adımlı durum makinesi
9. Tıklama ve koordinatlar
10. Süre, odak ve durdurma
11. Bellek profili ve nesne arama
12. Bellek ile görüntünün ilişkisi
13. Kayıtlar ve farm entegrasyonu
14. Kullanım ve hata teşhisi
15. Derleme ve UAC
16. Test kanıtları
17. Bilinen sınırlar ve doğrulanmamış davranışlar
18. Kaynak ve kanıt bütünlüğü
19. Ekler: tam kaynak kodları, araştırma betikleri, mevcut açıklamalar ve JSON kanıtları

## 1. Amaç ve teslim kapsamı

Kullanıcının kendi offline test oyunu için geliştirilen araç, oyunda beliren `Macro protection puzzle` penceresini fark edip `Target` satırındaki kelime ile aynı kelimeyi taşıyan butonu seçer. Doğrulama art arda dört adımdır. Kullanıcı yalnız istemciyi seçip izlemeyi başlatır; her bulmaca geldiğinde ayrıca haber vermesi gerekmez.

Program bağımsız bir EXE'dir. Mevcut farm motorunu durdurma veya yeniden başlatma bağlantısı eklenmemiştir. Bu rapor içinde tüm C# kaynakları, proje ve UAC manifesti, üç statik araştırma betiği, mevcut kullanım belgeleri ve doğrulama kayıtları bulunur. Ayrı dosyalar da aynı arşivde tutulur; böylece proje yeniden derlenebilir.

Çalışan yöntemin temeli ekrandaki metni okuyup eşleştirmektir. Bellek izleyicisi, doğrulama penceresinin açık olup olmadığını anlamak için ek sinyal sağlar. Sunucu doğrulamasını kaldıran bir değişiklik uygulanmamıştır.

## 2. Bulmaca kuralları ve örnekler

Gönderilen dört referans karede şu sıra görülür:

| Adım | Hedef | Sol üst | Sağ üst | Sol alt | Sağ alt | Doğru seçenek |
|---|---|---|---|---|---|---|
| 1/4 | SUN | CROWN | STAR | SUN | LEAF | Sol alt |
| 2/4 | STAR | GEM | STAR | CROWN | LEAF | Sağ üst |
| 3/4 | MOON | GEM | MOON | SHIELD | CROWN | Sağ üst |
| 4/4 | SUN | SWORD | SUN | STAR | LEAF | Sağ üst |

Bu tablo uygulamanın gerçek seçimini sabitlemez. `DemoForm` aynı sırayı test üretmek için kullanır; gerçek seçim `OcrReader` tarafından o anda okunan hedef ve seçeneklerden çıkarılır. Örneğin LEAF hangi konumdaysa, okunan hedef LEAF olduğunda o seçenek seçilir. Desteklenen sözlük sekiz kelimedir: `SUN`, `STAR`, `MOON`, `LEAF`, `CROWN`, `GEM`, `SHIELD`, `SWORD`.

## 3. Sürüm farkları

İlk sürüm v1'de kullanıcının kutunun dış çerçevesini elle seçmesi gerekiyordu; başlangıç modu yalnız okumaydı. Kullanıcı alanı seçip Başlat'a bastığı halde uygulamanın bir şey yapmadığını bildirdi. Eski kayıtlar bu ilk sorunun nedenini kesin olarak göstermediği için rapor belirli bir kök neden iddia etmez.

v1.1'de otomatik kutu bulma ve eşleştirme varsayılan olarak açık hale getirildi. Başlatma hataları görünür mesaj kutusuna ve kayıt dosyasına eklendi; PID, okuma/tıklama sayıları ve bellek bağlantısı durumu gösterildi. F9 kullanımda olduğunda Ctrl+Shift+F9 alternatifi eklendi. Canlı yerel testte OCR'nin `Target•. STAR` üretmesi görüldü; sabit etiket ayırıcıları buna göre ele alındı. Sembol kelimelerine tahmine dayalı düzeltme eklenmedi.

## 4. Mimari ve kaynak dosyaları

| Dosya | Sorumluluk |
|---|---|
| `Program.cs` | GUI başlangıcı, tek örnek kilidi ve tanılama/test komutları |
| `MainForm.cs` | Arayüz, bağlantı, zamanlayıcı, OCR/akış koordinasyonu, kayıtlar |
| `TargetWindow.cs` | Pencere seçimi, oturum doğrulaması, görüntü alma, Windows fare mesajları |
| `OcrReader.cs` | Windows OCR, görüntü hazırlama, hedef/adım/seçenek ayrıştırma |
| `PuzzleLocator.cs` | OCR satırlarının geometrisiyle otomatik kutu tahmini |
| `PuzzleFlow.cs` | Dört adımlı seçim ve kapanış durum makinesi |
| `MemoryDetector.cs` | Sürüm kontrollü, salt okunur UI nesnesi ve görünürlük izleyicisi |
| `CropForm.cs` | İsteğe bağlı elle alan seçimi |
| `DemoForm.cs` | Programa ait dört adımlı yerel test penceresi |
| `SelfTests.cs` | Durum/parsing/OCR ve yerel pencere girdi kontrolleri |
| `AutoTests.cs` | Otomatik yer bulma, referans kareler ve bellek alanı kontrolleri |
| `4UnityPuzzleTest.csproj` | .NET/Windows/x64 hedefi ve sürüm bilgileri |
| `app.manifest` | Yönetici UAC seviyesi ve Windows uyumluluk bildirimi |

Veri tipleri: `WindowChoice` seçili pencerenin HWND/PID/yol/oluşturulma zamanını; `Reading` okuma durumunu, hedefi, adımı, deneme sayısını ve dört `Option` nesnesini taşır. `Option` kelime ve merkez koordinatıdır. `LocateResult` başlığın görülüp görülmediğini, kutu sınırını ve açıklamayı taşır. `MemoryState` nesnenin geçerli/aktif durumunu, görünürlüğü, türü, adresini ve açıklamayı taşır.

## 5. Başlatma ve istemci bağlantısı

`WindowChoice.List` görünür, başlıklı üst seviye pencereleri listeler. Süreç yolu okunamayan pencereler listelenmez. Liste yenilenince önceki yol tercih edilir; o bulunamazsa `TClient.exe` seçilir. Kullanıcı doğru PID'yi listeden kontrol edebilir.

`StartRun` seçimi, elle modda alanı, OCR dilini ve eşleştirme modunda durdurma kısayolunu kontrol eder. Durum makinesi sıfırlanır; çalışma nesli ve sayaçlar yenilenir. Seçili pencere başlangıçta öne getirilmeye çalışılır. Bellek bağlantısı arka plan görevinde denenir; bu sırada kullanıcı durdurabilir. Bağlantı sonucu geldikten sonra çalışma nesli halen aynıysa zamanlayıcı başlar.

Bir çevrimin hedef aralığı 600 ms'dir. `busy` bayrağı önceki OCR bitmeden ikinci çevrimin başlamasını engeller; bu nedenle gerçek çevrim süresi OCR çalışma süresine bağlıdır, sabit bir tepki süresi garanti edilmez.

## 6. Otomatik pencere bulma

Otomatik modda önce seçili istemcinin tüm client alanı görünür ekrandan alınır. Windows OCR satır metinleri ve sınırlayıcı dikdörtgenleri çıkarılır. `PuzzleLocator` tek bir `Macro protection puzzle` başlığı, altında yatay olarak hizalı bir Target satırı ve onun altında Step satırı ister.

Başlığın merkezi `cx` kabul edilir. Hedef ve adım satırlarının merkezleri başlık genişliğinin %45'i kadar tolerans içinde hizalı olmalıdır. Hedef/adım merkezleri arasındaki `gap` mesafesi en az 20 piksel ve client yüksekliğinin en fazla %25'i olmalıdır. Tahmini kutu boyutları şöyledir:

```text
height = round(gap × 7.35)
width  = round(height × 0.92)
top    = floor(header.top − header.height × 0.85)
left   = round(cx − width / 2)
```

Tahmin client alanıyla kesiştirilir. En az 200×220 piksel ve tahminin en az %95'i korunmuş olmalıdır. Başlık görünür ama yerleşim belirsizse okumayı bekletir. Birden fazla başlık varsa seçim yapmaz. Kutu bulunduğunda kırpılan alan yeniden OCR'den geçirilir.

Bu katsayılar verilen pencere düzenine dayanır; bütün olası tema ve yerleşimler için genel bir pencere tanıyıcı değildir. Konum sabit kodlanmış değildir, fakat görünüm oranları varsayılır.

## 7. OCR ve eşleştirme

Dil seçimi sırası `en-US`, `tr-TR`, ardından Windows kullanıcı profili OCR dilleridir. Hiçbiri bulunamazsa başlatma hata verir. Test kayıtlarındaki kullanılan dil `tr` olmuştur.

Görüntü ters gri tonlamaya dönüştürülür. Ölçek en fazla 2.5'tir ve `OcrEngine.MaxImageDimension` sınırına göre azaltılır. PNG geçici bellek akışına yazılır, BGRA8 `SoftwareBitmap` olarak çözülür, `Windows.Media.Ocr.OcrEngine.RecognizeAsync` çalıştırılır. OCR koordinatları görüntü ölçeğine bölünerek orijinal client/kırpım koordinatlarına çevrilir.

Başlık, `Step n of 4`, tek basamaklı kalan attempts ve hedef etiketi birlikte aranır. Target için kullanılan ifade:

```regex
TAR[GQ]ET[^\p{L}\p{N}\r\n]*([A-Z]+)
```

Bu ifade sabit etikette G/Q değişimine ve noktalama ayırıcılarına izin verir; hedef kelimesi sekiz kelimelik sözlükte birebir bulunmalıdır. Buton metninde büyük harfe dönüştürme ve A–Z dışı karakterleri kaldırma uygulanır; kelimeyi başka kelimeye benzetme yapılmaz.

Butonlar kutu yüksekliğinin %64'ünün altında aranır. Sol/sağ ayrımı kutu genişliğinin yarısından, üst/alt sıra ayrımı yüksekliğin %82'sinden yapılır. Tam dört kelime dört ayrı 2×2 yuvaya düşmeli, kelimeler farklı olmalı, hedefe tam bir seçenek karşılık gelmeli ve kalan deneme sayısı sıfırdan büyük olmalıdır.

Okuma türleri `Absent`, `Uncertain`, `Ready` olarak ayrılır. Başlık/hedef/adım/seçenek işaretlerinin hiçbiri yoksa `Absent`; bazı işaretler var ama bütün koşullar sağlanmıyorsa `Uncertain`; tamamı sağlanıyorsa `Ready` üretilir.

## 8. Dört adımlı durum makinesi

`PuzzleFlow` girdi göndermez; seçilebilecek `Option` nesnesini üst katmana döndürür. `Blocked`, `Faulted`, `Complete` ve `LastClicked` akışın ana alanlarıdır.

1. İlk geçerli 1/4 okumasında akış bloke edilir ve ikinci görüntü beklenir.
2. Aynı `Reading.Key` ikinci kez görülürse hedefle eşleşen tek seçenek seçilir.
3. Üst katman yeni bir görüntü alır. Aynı anahtar yeniden doğrulanırsa tıklama gönderir.
4. Aynı adım tekrar okunursa yeni tıklama üretilmez; sonraki adım beklenir.
5. 2/4, 3/4 ve 4/4 için aynı süreç uygulanır.
6. Son tıklama tek başına tamamlanma değildir. En az bir saniyeye yayılan üç `Absent` okuması gerekir.
7. Son seçilmiş adım 4 ise kapanış tamamlanma sayılır. Daha erken kapanış hatadır.
8. Sonraki bulmaca geldiğinde yeni akış sıfırlanır; 1/4'ten başlaması gerekir.

Anahtar adım, hedef, attempts ve dört seçeneğin kelime/merkez koordinatlarını içerir. Dolayısıyla konum değişikliği veya OCR kutusu oynaması da kararlılık kontrolünü etkiler. Adım atlama veya geriye gitme hataya yol açar. Başlangıçta yalnız 2/4 görülen bir oturuma otomatik katılma desteklenmez. Hata sonrası araç kendi kendine yeniden başlatılmaz.

## 9. Tıklama ve koordinatlar

Seçeneğin merkezi kırpım alanına göredir. Client koordinatına dönüşüm:

```text
clientX = crop.X + option.Center.X
clientY = crop.Y + option.Center.Y
```

Tıklamadan önce noktanın bölge içinde olması, HWND/PID/yol/oluşturulma zamanının değişmemesi, client boyutunun aynı olması ve seçili pencerenin önde olması kontrol edilir. Alanın sanal ekran içinde bulunması ve 3×3 örnek noktada üstte başka pencere olmaması da kontrol edilir. Örtülme denetimi örneklemeye dayanır; tüm pikseller için ispat değildir.

Seçili HWND'ye sırayla `WM_MOUSEMOVE` (0x200), `WM_LBUTTONDOWN` (0x201) ve `WM_LBUTTONUP` (0x202) `PostMessage` ile gönderilir. Koordinatlar LPARAM içine paketlenir. Sistem fare imleci taşınmaz. Mesajın kuyruğa alınması, oyunun seçimi kabul ettiğini tek başına ispatlamaz; sonraki Step okuması ilerlemenin gözlemidir.

## 10. Süre, odak ve durdurma

Her seçimden sonra sonraki adım/kapanış için 12 saniyelik ilerleme süresi başlar. Odak başka pencereye geçtiğinde veya pencere küçültüldüğünde okuma ve tıklama bekler; bu bekleme süresi ilerleme saatinden çıkarılır. Oyun ekranındaki 90 saniye gibi sunucu sayacı okunup otomasyon zamanlayıcısı yapılmaz; bu sayaç odak kaybında durmayabilir.

F9 global durdurmadır. F9 alınamazsa Ctrl+Shift+F9 denenir. Her iki kısayol da alınamazsa eşleştirme modunun başlatılması engellenir. Durdurma `generation` değerini değiştirir; önceki asenkron OCR sonucu sonradan dönse bile eski nesil için seçim gönderilmez. Bağlanma görevi iptal edilir ve bellek handle'ı kapatılır.

Başlangıçta öne getirme denemesi vardır; izleme sırasında odak kaybolduğunda sürekli odağı geri alma uygulanmaz. Elle modda boyut değişimi yeniden alan seçimini gerektirir. Otomatik modda aktif akış sırasında boyut değişirse çalışma durur.

## 11. Bellek profili ve nesne arama

Statik analiz edilen dosya `C:/Games/4Unity/TClient.exe`, 16.549.888 bayttır. SHA-256:

```text
4dc9c526a895a10113c4cf23f2d199bff283d2c7ce75f949dc49cef15d27d622
```

RTTI sınıfı `CTSecuritySystemDlg` olarak bulunmuştur. Şu konumlar statik koddan çıkarılmıştır:

| Alan / yöntem | Konum | Anlamı |
|---|---|---|
| Vtable | TClient RVA 0xDC94B8 | UI sınıf kimliği |
| RTTI COL | RVA 0xE540C0 | Sınıf tanımlayıcısı |
| Show/hide override | Slot 0x30 → RVA 0x384080 | Açma/kapama yolu |
| Render override | Slot 0x160 → RVA 0x3839D0 | Güvenlik penceresi çizimi |
| Puzzle metin güncelleme | RVA 0x3840A0 | Başlık/hedef/adım/attempts etiketleri |
| Görünürlük | Nesne +0x19C, DWORD | Beklenen değer 0 veya 1 |
| UI türü | Nesne +0x328, byte | Eşleştirme bulmacası 0xFA |
| Başlık kontrolü | Nesne +0x288, pointer | Aday nesneye ek kimlik kontrolü |

RVA'lar yüklü modül tabanına eklenir; ASLR nedeniyle sabit mutlak adres kullanılmaz. Nesne adresi statik olarak varsayılmaz, bağlantı sırasında aranır.

Bağlantı yalnız tam dosya adı ve SHA profili eşleşirse denenir. `OpenProcess(0x410)` query-information + VM-read ister. Yol ve süreç oluşturulma zamanı doğrulanır. Yüklü vtable'ın iki yöntemi beklenen modül-relative adreslerde olmalıdır.

`VirtualQueryEx` ile committed, readable, private bölgeler gezilir; görüntü ve mapped bölgeler atlanır. En fazla 12.000 bölge, 256 MiB okuma bütçesi ve altı saniyelik döngü sınırı vardır. Bu süre API çağrıları/nesne doğrulamaları nedeniyle kesin bir toplam zaman garantisi değildir. Parçalar en fazla 256 KiB'dir ve sınırdaki pointer için sekiz bayt örtüşme bırakılır.

Modül tabanı + vtable RVA pointer'ı aranır; aday adres sekiz bayta hizalı olmalıdır. Adaydan 0x330 bayt okunur. Vtable, görünürlük 0/1 ve tür 0–11 veya 0xFA denetlenir. +0x288 başlık kontrolünün vtable'ı modül görüntüsü içinde olmalıdır. Tarama bütçesinde birden fazla aday bulunursa izleyici devreye alınmaz. Bütçe dışındaki nesnelerin yokluğu kanıtlanmış değildir.

Tek aday önbelleğe alınır. Her Snapshot nesne kimliğini ve oturumu yeniden kontrol eder. Aktif koşulu `Visible == 1 && Mode == 0xFA`dır. Nesne değişirse izleyici kapatılır; otomatik yeniden tarama bağlantıyı yeniden başlatmayı gerektirir.

Araştırma betikleri oyun dosyalarını diskten inceler. `puzzle_static_probe.py` etiket/RTTI arar; `puzzle_string_refs.py` RIP-relative etiket referanslarını disassemble eder; `puzzle_vtable_probe.py` render referansından vtable ve RTTI çıkarır. Python bağımlılıkları pefile ve capstone'dur. Bu betiklerin dosya yolları mevcut test ortamına göre sabittir; farklı ortamda yollar ayarlanmalıdır.

## 12. Bellek ile görüntünün ilişkisi

Bellek cevap kelimesini okumaz ve buton seçmez. Otomatik görüntü araması, bellek bağlantısı olmasa da çalışır. Desteklenmeyen sürüm, erişim reddi, aday bulunamaması veya nesne geçersizliği durumunda kullanıcıya neden gösterilir.

Otomatik modda belleğe göre pencere aktifken görsel başlık bulunamazsa `Absent` yerine `Uncertain` üretilir; böylece okunamayan açık pencere başarılı kapanış sanılmaz. Bu ek kural otomatik modun kutu bulunamaması yolundadır; elle kırpım moduna aynı bellek/Absent override'ı eklenmiş değildir.

Geliştirme sırasında yönetici olarak çalışan gerçek istemciye normal tanılama işlemi salt okunur erişim açamadı: **Win32 5**. `memory-probe.json` içinde state null, writes_attempted false ve input_sent false vardır. Canlı UI nesnesi ve açık/kapalı geçişi doğrulanmamıştır. EXE'nin UAC istemesi bu testin geçtiği anlamına gelmez.

## 13. Kayıtlar ve farm entegrasyonu

Kayıt kökü EXE/DLL'nin çalıştığı klasör altındaki `logs` dizinidir. `integration-state.json` önce `.tmp` dosyasına yazılır ve ardından eski dosyanın yerine taşınır. `events.jsonl` yalnız phase/detail değiştiğinde eklenir. Kayıt hatası durum satırında belirtilir.

| Alan grubu | İçerik |
|---|---|
| Sürüm ve zaman | utc, version, standalone, farm_connected |
| Akış | running, mode, blocked, complete, faulted, last_clicked_step |
| Pencere | selected_pid, selected_path, client, roi |
| Okuma | step, target, options, ocr_language, raw_puzzle_text, finder_detail |
| Tanılama | phase, frame_count, click_count, stop_key, backend |
| Bellek | memory_detail, memory_state, memory_access |

`phase` örnekleri: selected, manual_area_ready, connecting, connecting_memory, connected, waiting_for_window, waiting_for_puzzle, reading_uncertain, reading_ready, click_posted, complete, faulted, stopped, start_failed, calibration_failed. `locating` geçici çalışma aşamasıdır; her ara aşama ayrı dosya yazımı garantisi değildir.

`last-error.txt` son istisnayı; `finder-reading.json` otomatik yer bulmada seçilen başlık/hedef/adım satırlarını; `step-N-...png` tıklamadan önce karar verilen kırpılmış kareyi içerir. Tüm istemci OCR metni LocateAsync sonrasında temizlenir; panel metni kayıt için saklanır. Önizlemede tam client görüntüsü görülebilir.

Sonraki farm entegrasyonunda tüketici güncel zaman damgasını, çalışmayı, fault/blocked durumunu ve oturum kimliğini kontrol etmelidir. Kayıt eski/eksikse veya yardımcı durduysa farm beklemelidir. Kullanıcının elle durdurduğu farm tamamlanma bilgisiyle yeniden başlatılmamalıdır. Bu kurallar tasarım önerisidir; mevcut EXE diğer farm uygulamasına komut göndermez.

## 14. Kullanım ve hata teşhisi

1. EXE'yi aç, Windows yönetici onayını tamamla.
2. Doğru TClient.exe penceresini/PID'yi seç.
3. Kutuyu otomatik bul açık, Eşleştir modu seçili kalsın.
4. Bağlan ve başlat kullan; puzzle açık değilken de izleme başlar.
5. Oyunu önde ve görünür tut. Durdurmak için F9 veya gösterilen alternatif kısayolu kullan.

Yerel deneme için Yerel deneme aç → Bağlan ve başlat kullanılır. Yalnız oku modu hedef/adımı gösterir ve girdi göndermez. Alanı elle seç otomatik yer bulmayı kapatır; dış çerçeve, başlık ve dört buton birlikte seçilmelidir.

| Belirti | Kontrol |
|---|---|
| Listede client yok | Yenile; süreç yoluna erişim ve UAC seviyesi |
| Başlatılamadı | Mesaj kutusu ve last-error.txt; OCR dili ve kısayol |
| Öne getirin / küçültülmüş | Seçili oyunu görünür ve önde tut |
| Başlık bulundu, seçenek yok | Finder kaydı, tema/ölçek/yerleşim; gerekirse elle alan |
| Bellek Win32 5 | Süreç erişimi reddedildi; görsel yöntem devrede |
| İstemci sürümü farklı | Bellek SHA profili bu dosyaya uymuyor |
| Sonraki adım gelmedi | Mesajın oyun tarafından kabulü veya ekranın ilerlemesi doğrulanmadı |
| Ekran seçimden önce değişti | Üçüncü okuma aynı değildi; tıklama yapılmadı |

## 15. Derleme ve UAC

Proje WinForms, Windows x64 ve `net9.0-windows10.0.19041.0` hedeflidir. Windows 10 19041 veya üzeri / Windows 11 ve derleme için .NET 9 SDK kullanılır. Çalıştırılacak EXE self-contained paketlenir; Windows OCR dil bileşeni işletim sisteminden gelir.

Depo kökünde derleme/paketleme:

```powershell
dotnet build captcha/src/4UnityPuzzleTest.csproj -c Release
dotnet publish captcha/src/4UnityPuzzleTest.csproj -c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o captcha/releases/1.1.0
```

Manifest `requestedExecutionLevel="requireAdministrator"` ve `uiAccess="false"` içerir. Yönetici onayını Windows UAC gösterir ve kullanıcı verir. Bu yayıncı imzası değildir. Uygulamanın kendi DLL'si üzerinden test çalıştırmak EXE manifestinin doğrulanması yerine geçmez; önceki teslimde paketlenmiş EXE'den manifest ayrıca çıkarılmıştır.

Program tek normal GUI örneğini `Local\4UnityPuzzleTest` mutex'iyle sınırlar. Tanılama komutları bu normal başlangıç yolundan ayrı çalışır; görünür testleri aynı anda çalıştırmak odak/kısayol çakışmasına yol açabilir.

| Komut | İşlev |
|---|---|
| --self-test | Durum, parser ve OCR kontrolleri |
| --integration-test | Uygulamanın kendi native penceresine dört mesaj seçimi |
| --automatic-session-test | Boş ekran → sonradan puzzle gelişini tüm uygulamayla test |
| --auto-test <resimler> | Otomatik yer bulma ve bellek alanı parser kontrolleri |
| --analyze-files <resimler> | Dosyadan OCR, girdi göndermez |
| --inspect-targets | TClient pencereleri ve F9 durumu; ekran almaz |
| --memory-probe <pid> <path> | Salt okunur bağlantı tanılaması |
| --render-ui | Yerel örnekle arayüz PNG'si üretir |

DLL yolu: `captcha/src/bin/Release/net9.0-windows10.0.19041.0/win-x64/4UnityPuzzleTest.dll`. Komut hata verirse `verification/failure.txt` yazılır ve çıkış kodu 1 olur. Bilinmeyen seçenek normal uygulamayı açmak yerine hata verir.

## 16. Test kanıtları

Bu bölüm **5 Ekim 2026 tesliminde arşivlenen** sonuçları anlatır. Rapor hazırlanırken oyuna tekrar tıklama gönderilmedi. Test JSON'ları ve görüntüler `evidence` altında korunmuştur.

| Kanıt | Sonuç | Sınır |
|---|---|---|
| self-tests.json | 41 kontrol, pass true | Durum/parsing ve yerel çizim OCR |
| auto-tests.json | 38 kontrol, pass true | Dört referans kare × 1.0 / 1.25 / 0.85 ölçek; sentetik bellek |
| window-tests.json | 23 kontrol, dört girdi ve dört kabul | Yalnız yardımcıya ait native pencere |
| automatic-session.json | Alan seçimi/bildirim yok; 2 boş çevrim, toplam 13 çevrim, dört doğru seçim ve kapanış | Yalnız yardımcıya ait demo |
| image-readings.json | Dört özgün karenin hedef/seçenekleri okundu | Görüntü dosyası testi |
| memory-probe.json | Win32 5, state null | Canlı bellek başarısı yok |
| build.json + uac-manifest.xml | v1.1.0, x64 paket, admin manifest | Paket kimliği/manifest kanıtı |

41 + 38 + 23 = 102 kontrol vardır; tam uygulama geliş oturumu ayrıca kayıtlıdır. Bunları 102 canlı oyun testi olarak yorumlamak yanlıştır. Referans karelere konum ve ölçek uygulanmış olsa da her oyun teması/çözünürlüğü taranmamıştır.

Arşivlenen v1.1 EXE: **56.699.173 bayt**. SHA-256:

```text
B3CDF76E3F7E8E318CAE1B5DE7082FA9F11C457A1913D780054FFC938413BDA7
```

Asıl EXE çalışma klasöründeki `dist/4UnityPuzzleTest-v1.1/4UnityPuzzleTest.exe` dosyasıdır. Git arşivi kaynak/rapor/kanıt içindir; EXE ikilisi bu belge teslimine tekrar eklenmez. Aynı kaynaktan yeniden derlemek SDK/ortam ve paketleme farkları nedeniyle aynı EXE hash'ini garanti etmez.

## 17. Bilinen sınırlar ve doğrulanmamış davranışlar

- Gerçek istemcide dört adımın kabul edildiği ve kapanışın gerçekleştiği doğrulanmadı.
- Canlı CTSecuritySystemDlg nesnesi ve açılış/kapanış geçişleri doğrulanmadı; alanlar statik koddan çıkarıldı.
- Bellek profili yalnız belirtilen SHA sürümüne aittir. Sürümler arasında offset taşımak güvenilir değildir.
- Ekran okuma CopyFromScreen kullanır. Arkada/küçük veya farklı render davranışı gösteren oyun için görünmez ekran yakalama eklenmedi.
- Sekiz kelime ve 2×2 düzen desteklenir. Yeni kelime veya farklı düzen bekler/hata verir.
- OCR geometrisi ve merkez koordinatları kesin anahtara girer; küçük oynama kararlılık şartını bozabilir.
- Kutu bulma oranları verilen düzen içindir; başlık okunamıyorsa ve bellek yoksa otomatik arama yanlışlıkla yokluk okuyabilir.
- Örtülme kontrolü yalnız dokuz noktayı örnekler. Tam piksel analizi değildir.
- Bellek taraması sınırlıdır; aday bulunamaması bütün süreçte nesne yok anlamına gelmez.
- Bellek SHA dosyası okunamaması gibi erken hatalar tüm başlatmayı başarısız yapabilir; her istisna sessiz görsel fallback değildir.
- Ayrı farm EXE'siyle eşzamanlı girdi ve duraklama koordinasyonu kurulmamıştır.
- Oturum kayıtları EXE klasörüne yazılır; eski olaylar birikir, otomatik saklama/rotasyon eklenmemiştir.

## 18. Kaynak ve kanıt bütünlüğü

Tam kaynak ekleri aşağıda dosyalardan otomatik alınır. Arşivdeki `source-manifest.json` her dosyanın byte boyutunu ve SHA-256'sını içerir. Rapor metni UTF-8'dır; kaynak ekleri okunabilirlik için LF satır sonuna çevrilir. Ayrı kaynak dosyalarının özgün baytları korunur ve manifest bu ayrı dosyalara aittir.

`report-tools/export_captcha_report.py` ve bu ana metin raporun nasıl oluşturulduğunu saklar. Kanıt JSON'larında geçen PID'ler ve Temp dosya yolları geçmiş test oturumunun bilgisi olup güncel hedef değildir. Kaynak yorumları ve eski kullanım açıklamaları eklerde aynen korunmuştur; üstteki inceleme mevcut v1.1 davranışını ve sınırlarını açıklar.

## 19. Tam kaynak ve kanıt ekleri

Aşağıdaki bölümler uygulamanın tüm kaynak dosyalarını, araştırma araçlarını, mevcut açıklamaları ve metin kanıtlarını eksiksiz içerir. PNG/JPEG görselleri arşivde ayrı dosyalardır.

### Ek: src/4UnityPuzzleTest.csproj

SHA-256: `37fa1276bd1d9a5b26e18be4d581f5948512aaef0d9ff2771c22849f87bf468d`. Boyut: 625 bayt.

````xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
    <PlatformTarget>x64</PlatformTarget><RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>4UnityPuzzleTest</AssemblyName>
    <Version>1.1.0</Version><FileVersion>1.1.0.0</FileVersion>
  </PropertyGroup>
</Project>
````

### Ek: src/app.manifest

SHA-256: `434c83886041666b1e9715281478cfdb052bf624d6281eae342de185737b7f7f`. Boyut: 562 bayt.

````xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="4UnityPuzzleTest"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3"><security><requestedPrivileges><requestedExecutionLevel level="requireAdministrator" uiAccess="false"/></requestedPrivileges></security></trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1"><application><supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/></application></compatibility>
</assembly>
````

### Ek: src/AutoTests.cs

SHA-256: `ede745f0f5c26940734ec19a9879a7ea747dfe69fb48084fd9327ae43efefdbf`. Boyut: 5053 bayt.

````csharp
using System.Text.Json;

namespace UnityPuzzleTest;

static class AutoTests
{
    public static async Task Run(string output, string[] files)
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new IOException(message); }
        LineBox[] lines = [new("Macro protection puzzle", new(400, 110, 215, 19)), new("Target: SUN", new(458, 210, 96, 15)), new("Step 1 of 4", new(465, 270, 80, 15))];
        Check(!PuzzleLocator.Locate([], new(1600, 900)).HeaderSeen, "background falsely identified as a puzzle");
        Check(PuzzleLocator.Locate(lines, new(1600, 900)).Bounds.Contains(new Point(507, 490)), "known layout not located");
        Check(PuzzleLocator.Locate([lines[0]], new(1600, 900)).Bounds.IsEmpty, "header-only layout must wait");
        Check(PuzzleLocator.Locate([lines[0], lines[0], lines[1], lines[2]], new(1600, 900)).Bounds.IsEmpty, "ambiguous header must wait");
        Check(PuzzleLocator.Locate([lines[0], lines[1], lines[2] with { Bounds = new(465, 150, 80, 15) }], new(1600, 900)).Bounds.IsEmpty, "reversed target/step positions accepted");
        Check(!PuzzleLocator.Locate([lines[0], lines[1] with { Text = "Target•. STAR" }, lines[2]], new(1600, 900)).Bounds.IsEmpty, "native OCR label punctuation rejected");
        var memory = new byte[0x330];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(memory, 0x140DC94B8);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(0x19C), 1); memory[0x328] = 0xFA;
        Check(MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Active, "visible macro mode must be active");
        memory[0x328] = 3;
        Check(!MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Active, "other security mode must not be a puzzle");
        memory[0x328] = 0xFA; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(0x19C), 0);
        Check(!MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Active, "hidden macro mode must not be active");
        Check(!MemoryDetector.Parse(memory, 0x140DC94B0, 0x20000000).Valid, "wrong vtable accepted");
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(0x19C), 2);
        Check(!MemoryDetector.Parse(memory, 0x140DC94B8, 0x20000000).Valid, "invalid visibility accepted");
        Check(!MemoryDetector.Parse(new byte[8], 0x140DC94B8, 0x20000000).Valid, "truncated memory fields accepted");
        var reader = new OcrReader(); var results = new List<object>();
        using var fixture = new DemoForm();
        using (var whole = new Bitmap(fixture.ClientSize.Width, fixture.ClientSize.Height))
        {
            using (var g = Graphics.FromImage(whole)) fixture.PaintScene(g);
            var found = await reader.LocateAsync(whole).ConfigureAwait(false);
            Check(!found.Bounds.IsEmpty, "full fixture not auto-located: " + found.Detail);
            using var panel = whole.Clone(found.Bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var r = await reader.ReadAsync(panel).ConfigureAwait(false);
            Check(r.Valid && r.Step == 1 && r.Target == "SUN", "automatically cropped fixture was not readable");
            results.Add(new { fixture = true, found, reading = r });
        }
        foreach (string path in files)
        {
            using var reference = new Bitmap(path);
            foreach (double factor in new[] { 1.0, 1.25, .85 })
            {
                using var whole = new Bitmap(1600, 900);
                var destination = new Rectangle(510, 180, (int)(reference.Width * factor), (int)(reference.Height * factor));
                using (var g = Graphics.FromImage(whole)) { g.Clear(Color.FromArgb(30, 42, 32)); g.DrawImage(reference, destination); }
                var found = await reader.LocateAsync(whole).ConfigureAwait(false);
                Check(!found.Bounds.IsEmpty, $"reference {Path.GetFileName(path)} factor {factor} not located: {found.Detail}");
                using var panel = whole.Clone(found.Bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var reading = await reader.ReadAsync(panel).ConfigureAwait(false);
                if (!reading.Valid)
                {
                    panel.Save(Path.Combine(output, "auto-failure.png"));
                    File.WriteAllText(Path.Combine(output, "auto-failure.json"), JsonSerializer.Serialize(new { path, factor, found, reading, text = reader.LastText }, new JsonSerializerOptions { WriteIndented = true }));
                }
                Check(reading.Valid, $"auto-cropped reference {Path.GetFileName(path)} factor {factor} not readable");
                results.Add(new { file = Path.GetFileName(path), factor, found, reading });
            }
        }
        File.WriteAllText(Path.Combine(output, "auto-tests.json"), JsonSerializer.Serialize(new { pass = true, checks, game_accessed = false, input_sent = false, results }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
````

### Ek: src/CropForm.cs

SHA-256: `311f3f06b745ebf4ffb7300a82dc9b473500a1736197eba7942e17ae03c38dc3`. Boyut: 3216 bayt.

````csharp
namespace UnityPuzzleTest;

sealed class CropForm : Form
{
    readonly Bitmap image;
    Point start, end;
    bool dragging;
    public Rectangle Selected { get; private set; }
    Rectangle DisplayBounds
    {
        get
        {
            var available = new Size(ClientSize.Width - 30, ClientSize.Height - 90);
            double scale = Math.Min(available.Width / (double)image.Width, available.Height / (double)image.Height);
            var size = new Size((int)(image.Width * scale), (int)(image.Height * scale));
            return new((ClientSize.Width - size.Width) / 2, 55, size.Width, size.Height);
        }
    }
    public CropForm(Bitmap image)
    {
        this.image = image;
        Text = "Test ekranının dış çerçevesini seç"; ClientSize = new(1000, 740);
        MinimumSize = new(700, 520); StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(20, 25, 32); ForeColor = Color.White;
        DoubleBuffered = true; KeyPreview = true;
        var accept = new Button { Text = "Alanı kullan", Dock = DockStyle.Bottom, Height = 38 };
        accept.Click += (_, _) =>
        {
            if (Selected.Width < 200 || Selected.Height < 220) { MessageBox.Show(this, "Dört seçenek ve başlık dahil bütün kutuyu seçin."); return; }
            DialogResult = DialogResult.OK;
        };
        Controls.Add(accept);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left && DisplayBounds.Contains(e.Location)) { dragging = true; Capture = true; start = end = e.Location; Selected = Rectangle.Empty; Invalidate(); } };
        MouseMove += (_, e) =>
        {
            if (!dragging) return;
            Rectangle d = DisplayBounds;
            end = new(Math.Clamp(e.X, d.Left, d.Right), Math.Clamp(e.Y, d.Top, d.Bottom));
            float sx = image.Width / (float)d.Width, sy = image.Height / (float)d.Height;
            int left = (int)((Math.Min(start.X, end.X) - d.Left) * sx), top = (int)((Math.Min(start.Y, end.Y) - d.Top) * sy);
            int right = (int)((Math.Max(start.X, end.X) - d.Left) * sx), bottom = (int)((Math.Max(start.Y, end.Y) - d.Top) * sy);
            Selected = Rectangle.Intersect(new Rectangle(Point.Empty, image.Size), Rectangle.FromLTRB(left, top, right, bottom)); Invalidate();
        };
        MouseUp += (_, _) => { dragging = false; Capture = false; };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        TextRenderer.DrawText(e.Graphics, "Kutunun dış çerçevesini fareyle çiz: başlık, hedef, adım ve dört buton dahil.", Font, new Point(16, 18), ForeColor);
        e.Graphics.DrawImage(image, DisplayBounds);
        if (!Selected.IsEmpty)
        {
            Rectangle d = DisplayBounds;
            var selected = new Rectangle(d.Left + Selected.Left * d.Width / image.Width, d.Top + Selected.Top * d.Height / image.Height,
                Selected.Width * d.Width / image.Width, Selected.Height * d.Height / image.Height);
            using var pen = new Pen(Color.Cyan, 3); e.Graphics.DrawRectangle(pen, selected);
        }
    }
}
````

### Ek: src/DemoForm.cs

SHA-256: `7e1b4cf3358d49c910fea9ec9207c0a6072ecd7211f20cf50f2edfa17349e394`. Boyut: 4044 bayt.

````csharp
namespace UnityPuzzleTest;

sealed class DemoForm : Form
{
    public Rectangle PuzzleBounds => new(24, 36, 408, 444);
    public int Step { get; private set; } = 1;
    public int Attempts { get; private set; } = 3;
    public int Accepted { get; private set; }
    public int Inputs { get; private set; }
    public bool Finished => Step == 5;
    internal bool PuzzleVisible = true;
    static readonly string[] Targets = ["SUN", "STAR", "MOON", "SUN"];
    static readonly string[][] Choices = [["CROWN", "STAR", "SUN", "LEAF"], ["GEM", "STAR", "CROWN", "LEAF"], ["GEM", "MOON", "SHIELD", "CROWN"], ["SWORD", "SUN", "STAR", "LEAF"]];
    public DemoForm()
    {
        Text = "4Unity · yerel test penceresi"; ClientSize = new(456, 516);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        BackColor = Color.FromArgb(29, 43, 33); DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;
    }
    Rectangle ButtonBounds(int slot)
    {
        Rectangle r = PuzzleBounds;
        return new(r.X + (slot % 2 == 0 ? 42 : 220), r.Y + (slot / 2 == 0 ? 327 : 385), 146, 36);
    }
    public void Press(int slot)
    {
        Inputs++;
        if (Finished || Attempts <= 0 || slot is < 0 or > 3) return;
        if (Choices[Step - 1][slot] == Targets[Step - 1]) { Accepted++; Step++; } else Attempts--;
        Invalidate(); Update();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || Finished) return;
        for (int i = 0; i < 4; i++) if (ButtonBounds(i).Contains(e.Location)) { Press(i); break; }
    }
    internal void PaintScene(Graphics g)
    {
        g.Clear(BackColor);
        using var small = new Font("Segoe UI", 10);
        TextRenderer.DrawText(g, "YEREL SİMÜLASYON · oyun bağlantısı yok", small, new Rectangle(0, 2, ClientSize.Width, 24), Color.WhiteSmoke, TextFormatFlags.HorizontalCenter);
        if (Finished || !PuzzleVisible)
        {
            using var big = new Font("Segoe UI", 22, FontStyle.Bold);
            TextRenderer.DrawText(g, Finished ? "Test tamamlandı" : "Test alanı · normal oyun", big, new Rectangle(0, 215, ClientSize.Width, 70), Color.LightGreen, TextFormatFlags.HorizontalCenter);
            return;
        }
        Rectangle r = PuzzleBounds;
        using var fill = new SolidBrush(Color.FromArgb(11, 16, 9)); g.FillRectangle(fill, r);
        using var edge = new Pen(Color.Olive, 3); g.DrawRectangle(edge, r);
        using var heading = new Font("Segoe UI", 15, FontStyle.Bold);
        using var normal = new Font("Segoe UI", 11, FontStyle.Bold);
        void Center(string text, int top, Color color, Font font) => TextRenderer.DrawText(g, text, font,
            new Rectangle(r.X + 5, r.Y + top, r.Width - 10, 30), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        Center("Macro protection puzzle", 8, Color.Khaki, heading);
        Center("Click the matching symbol", 63, Color.OrangeRed, normal);
        Center("Target:  " + Targets[Step - 1], 102, Color.WhiteSmoke, normal);
        Center($"Step {Step} of 4", 162, Color.LightCyan, normal);
        Center($"{Attempts} attempts - 90 seconds", 225, Color.Khaki, normal);
        for (int i = 0; i < 4; i++)
        {
            Rectangle b = ButtonBounds(i);
            using var buttonFill = new SolidBrush(Color.FromArgb(53, 46, 20)); g.FillRectangle(buttonFill, b); g.DrawRectangle(edge, b);
            TextRenderer.DrawText(g, Choices[Step - 1][i], normal, b, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintScene(e.Graphics); }
    internal Bitmap Snapshot()
    {
        using var full = new Bitmap(ClientSize.Width, ClientSize.Height);
        using (var g = Graphics.FromImage(full)) PaintScene(g);
        return full.Clone(PuzzleBounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }
}
````

### Ek: src/MainForm.cs

SHA-256: `aa955b9da3943642e544379ea1943b99b8390f20d9ea68354c4d2297335cdef5`. Boyut: 20868 bayt.

````csharp
using System.Diagnostics;
using System.Text.Json;

namespace UnityPuzzleTest;

sealed class MainForm : Form
{
    readonly ComboBox windows = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox automatic = new() { Text = "Kutuyu otomatik bul", Checked = true, AutoSize = true };
    readonly Label status = new(), area = new();
    readonly PictureBox preview = new() { SizeMode = PictureBoxSizeMode.Zoom };
    readonly Button refresh, select, start, stop, demo;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 600 };
    readonly PuzzleFlow flow = new();
    WindowChoice? selected;
    DemoForm? fixture;
    OcrReader? reader;
    MemoryDetector? memory;
    MemoryState? memoryState;
    CancellationTokenSource? connecting;
    string memoryDetail = "Bellek izleme henüz başlamadı";
    Rectangle crop;
    Size clientSize;
    bool running, busy, hotkey;
    string stopKey = "F9", phase = "idle", finderDetail = "";
    int frameCount, clickCount;
    double suspendedAt, suspendedSeconds;
    int generation;
    string lastLog = "";
    readonly string logRoot = Path.Combine(AppContext.BaseDirectory, "logs");
    public MainForm(bool render = false)
    {
        Text = "4Unity Puzzle Test · v1.1"; ClientSize = new(920, 690); MinimumSize = MaximumSize = Size;
        StartPosition = FormStartPosition.CenterScreen; Font = new("Segoe UI", 10);
        BackColor = Color.FromArgb(20, 25, 32); ForeColor = Color.FromArgb(235, 240, 245);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        LabelText("Offline test · dört adımlı eşleştirme", 22, 17, 860, 37, 18, true);
        LabelText("Client seç → Bağlan ve başlat. Kutu geldiğinde otomatik aranır.", 24, 65, 870, 28);
        windows.SetBounds(24, 106, 716, 32); Controls.Add(windows);
        refresh = Button("Yenile", 755, 104, 140, 36, (_, _) => RefreshWindows());
        area.SetBounds(24, 151, 870, 30); area.ForeColor = Color.LightSteelBlue; Controls.Add(area);
        select = Button("Alanı elle seç (isteğe bağlı)", 24, 194, 270, 40, async (_, _) => await SelectAreaAsync());
        demo = Button("Yerel deneme aç", 24, 245, 270, 40, (_, _) => OpenDemo());
        automatic.SetBounds(24, 292, 270, 28); Controls.Add(automatic);
        LabelText("Çalışma şekli", 24, 326, 270, 26);
        mode.Items.AddRange(["Yalnız oku · tıklama yok", "Eşleştir · offline test"]); mode.SelectedIndex = 1;
        mode.SetBounds(24, 359, 270, 32); Controls.Add(mode);
        start = Button("Bağlan ve başlat", 24, 405, 154, 44, async (_, _) => await StartRun());
        stop = Button("Durdur · F9", 186, 405, 108, 44, (_, _) => StopRun("Kullanıcı durdurdu")); stop.Enabled = false;
        LabelText("Oyun önde ve görünür kalmalı. Bekleme veya hata nedeni altta gösterilir.", 24, 472, 272, 69);
        LabelText("Ayrı sürüm mevcut farm EXE’sini durdurmaz. Farm bağlantısı birleştirme aşamasında eklenecek.", 24, 544, 272, 80);
        preview.SetBounds(331, 194, 564, 413); preview.BackColor = Color.FromArgb(11, 15, 20); preview.BorderStyle = BorderStyle.FixedSingle; Controls.Add(preview);
        status.SetBounds(24, 631, 872, 44); status.ForeColor = Color.LightCyan; Controls.Add(status);
        status.Text = "Client seçildiğinde Bağlan ve başlat kullan"; area.Text = "Henüz client seçilmedi";
        windows.SelectedIndexChanged += (_, _) =>
        {
            if (running) return;
            selected = windows.SelectedItem as WindowChoice; crop = Rectangle.Empty;
            area.Text = selected == null ? "Henüz client seçilmedi" : $"Seçildi · PID {selected.Pid} · {selected.Path}";
            phase = "selected"; status.Text = "Client seçildi · bağlantıyı başlatmak için Bağlan ve başlat";
        };
        timer.Tick += async (_, _) => await TickAsync();
        if (!render) RefreshWindows();
        else
        {
            windows.Items.Add("Örnek · offline test penceresi"); windows.SelectedIndex = 0;
            area.Text = "Seçildi · PID 1234 · C:\\Games\\4Unity\\TClient.exe";
            using var example = new DemoForm(); using var snapshot = example.Snapshot(); ShowPreview(snapshot);
        }
    }
    void LabelText(string text, int x, int y, int width, int height, int size = 10, bool bold = false)
    {
        Controls.Add(new Label { Text = text, Bounds = new(x, y, width, height), Font = new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = ForeColor });
    }
    Button Button(string text, int x, int y, int width, int height, EventHandler action)
    {
        var b = new Button { Text = text, Bounds = new(x, y, width, height), BackColor = Color.FromArgb(42, 52, 65), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderColor = Color.FromArgb(75, 94, 110); b.Click += action; Controls.Add(b); return b;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); hotkey = Native.RegisterHotKey(Handle, 9, 0x4000, 0x78);
        if (!hotkey) { hotkey = Native.RegisterHotKey(Handle, 9, 0x4006, 0x78); stopKey = "Ctrl+Shift+F9"; }
        stop.Text = "Durdur" + (stopKey == "F9" ? " · F9" : "");
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x312 && m.WParam.ToInt32() == 9) StopRun(stopKey + " ile durduruldu");
        base.WndProc(ref m);
    }
    void RefreshWindows()
    {
        string? previous = selected?.Path;
        windows.Items.Clear(); windows.Items.AddRange(WindowChoice.List());
        if (windows.Items.Count > 0)
        {
            int index = windows.Items.Cast<WindowChoice>().ToList().FindIndex(w => string.Equals(w.Path, previous, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = windows.Items.Cast<WindowChoice>().ToList().FindIndex(w => string.Equals(Path.GetFileName(w.Path), "TClient.exe", StringComparison.OrdinalIgnoreCase));
            windows.SelectedIndex = Math.Max(0, index);
        }
        else status.Text = "Açık test penceresi bulunamadı; yerel denemeyi açabilirsin";
    }
    void OpenDemo()
    {
        fixture?.Close(); fixture?.Dispose(); fixture = new DemoForm(); fixture.Show();
        using var process = Process.GetCurrentProcess();
        var choice = new WindowChoice(fixture.Handle, Environment.ProcessId, fixture.Text, process.MainModule!.FileName!, process.StartTime.ToUniversalTime().Ticks);
        windows.Items.Add(choice); windows.SelectedItem = choice;
        selected = choice; crop = fixture.PuzzleBounds; clientSize = fixture.ClientSize;
        automatic.Checked = true;
        area.Text = "Yerel simülasyon alanı hazır · oyuna bağlanmaz";
        using var sample = fixture.Snapshot(); ShowPreview(sample);
    }
    async Task SelectAreaAsync()
    {
        if (selected == null) { status.Text = "Önce bir test penceresi seç"; return; }
        SetEnabled(false); var window = selected;
        try
        {
            status.Text = "Test penceresinin görüntüsü alınıyor"; Hide(); Native.SetForegroundWindow(window.Handle);
            await Task.Delay(700);
            using var image = window.Capture(null); Show(); Activate();
            using var picker = new CropForm(image);
            if (picker.ShowDialog(this) != DialogResult.OK) { status.Text = "Alan seçimi iptal edildi"; return; }
            crop = picker.Selected; clientSize = image.Size;
            automatic.Checked = false;
            using var panel = image.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb); ShowPreview(panel);
            area.Text = $"Alan hazır · {crop.Width} × {crop.Height} · pencere {clientSize.Width} × {clientSize.Height}";
            status.Text = "Elle seçilen alan hazır · Bağlan ve başlat kullan"; phase = "manual_area_ready"; WriteState(status.Text, null);
        }
        catch (Exception e) { status.Text = e.Message; phase = "calibration_failed"; WriteState(status.Text, null); SaveError(e); }
        finally { Show(); SetEnabled(true); }
    }
    void SetEnabled(bool enabled)
    {
        windows.Enabled = mode.Enabled = automatic.Enabled = refresh.Enabled = select.Enabled = demo.Enabled = start.Enabled = enabled;
        stop.Enabled = !enabled && running;
    }
    async Task StartRun()
    {
        try
        {
            if (busy) throw new IOException("Önceki okumanın bitmesini bekleyin");
            if (selected == null) throw new IOException("Önce bir client seçin");
            if (!automatic.Checked && crop.IsEmpty) throw new IOException("Otomatik kutu bulmayı açın veya alanı elle seçin");
            if (!hotkey && mode.SelectedIndex == 1) throw new IOException("F9 ve Ctrl+Shift+F9 kullanımda; Yalnız oku modunu kullanın veya çakışan uygulamayı kapatın");
            reader ??= new OcrReader(); flow.Reset(); generation++; running = true;
            frameCount = clickCount = 0; suspendedAt = suspendedSeconds = 0;
            SetEnabled(false); Native.SetForegroundWindow(selected.Handle);
            phase = "connecting"; status.Text = $"Başladı · PID {selected.Pid} · durdurma: {stopKey}"; WriteState(status.Text, null, true);
            connecting?.Dispose(); connecting = new CancellationTokenSource();
            var token = connecting.Token; var window = selected; int ticket = generation;
            phase = "connecting_memory"; status.Text = $"PID {window.Pid} · bellek bağlantısı kontrol ediliyor"; WriteState(status.Text, null, true);
            var linked = await Task.Run(() => MemoryDetector.Connect(window, token));
            if (!running || ticket != generation) { linked.Detector?.Dispose(); return; }
            memory = linked.Detector; memoryDetail = linked.Detail; memoryState = memory?.Snapshot();
            phase = "connected"; status.Text = memoryDetail + $" · durdurma: {stopKey}"; WriteState(status.Text, null, true);
            timer.Start();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (IsDisposed || Disposing) return;
            running = false; timer.Stop(); SetEnabled(true); status.Text = "Başlatılamadı: " + e.Message;
            phase = "start_failed"; WriteState(status.Text, null, true); SaveError(e);
            MessageBox.Show(this, status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    void StopRun(string reason)
    {
        running = false; generation++; timer.Stop(); flow.Fail(reason);
        connecting?.Cancel(); memory?.Dispose(); memory = null;
        status.Text = reason; phase = "stopped"; SetEnabled(true); WriteState(reason, null);
    }
    async Task TickAsync()
    {
        if (!running || busy || selected == null || reader == null) return;
        busy = true; int ticket = generation; var window = selected;
        try
        {
            if (memory != null)
            {
                memoryState = memory.Snapshot(); memoryDetail = memoryState.Detail;
                if (!memoryState.Valid) { memory.Dispose(); memory = null; }
            }
            if (!window.IsReady(out string reason, automatic.Checked ? null : clientSize, automatic.Checked ? null : crop))
            {
                status.Text = memoryState?.Active == true ? "Bellek: doğrulama açık · " + reason : reason; phase = "waiting_for_window";
                if (suspendedAt == 0) suspendedAt = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                // Focus changes are pauses. Geometry/session failures require explicit recalibration.
                if (!reason.StartsWith("Bekliyor:") && !reason.StartsWith("Test alanının üstünde")) StopRun(reason);
                else WriteState(status.Text, null, true);
                return;
            }
            if (suspendedAt > 0) { suspendedSeconds += Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - suspendedAt; suspendedAt = 0; }
            Bitmap? captured = null;
            Reading reading;
            if (automatic.Checked)
            {
                using var full = window.Capture(null);
                if (clientSize != Size.Empty && full.Size != clientSize && flow.Blocked) { StopRun("Oyun boyutu aktif adım sırasında değişti; tekrar başlatın"); return; }
                clientSize = full.Size;
                phase = "locating"; LocateResult located = await reader.LocateAsync(full);
                if (!running || generation != ticket) return;
                finderDetail = located.Detail;
                if (located.HeaderSeen && located.Bounds.IsEmpty)
                {
                    Directory.CreateDirectory(logRoot);
                    File.WriteAllText(Path.Combine(logRoot, "finder-reading.json"), JsonSerializer.Serialize(new { located,
                        lines = reader.LastLines.Where(l => l.Text.Contains("puzzle", StringComparison.OrdinalIgnoreCase) || l.Text.Contains("Tar", StringComparison.OrdinalIgnoreCase) || l.Text.Contains("Step", StringComparison.OrdinalIgnoreCase)) }, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (located.Bounds.IsEmpty)
                {
                    reading = located.HeaderSeen ? Reading.Unknown(located.Detail) : memoryState?.Active == true ?
                        Reading.Unknown("Bellek doğrulamanın açık olduğunu gösteriyor; görüntü henüz okunamadı") : Reading.Absent;
                    crop = Rectangle.Empty; ShowPreview(full);
                }
                else
                {
                    crop = located.Bounds;
                    captured = full.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try { reading = await reader.ReadAsync(captured); } catch { captured.Dispose(); throw; }
                }
            }
            else
            {
                captured = window.Capture(crop, clientSize);
                try { reading = await reader.ReadAsync(captured); } catch { captured.Dispose(); throw; }
            }
            using var frame = captured;
            if (!running || generation != ticket) return;
            frameCount++; phase = reading.Valid ? "reading_ready" : reading.Kind == ReadingKind.Absent ? "waiting_for_puzzle" : "reading_uncertain";
            area.Text = $"Bağlı · PID {window.Pid} · okuma {frameCount} · seçim {clickCount} · {memoryDetail}";
            if (frame != null) ShowPreview(frame);
            if (mode.SelectedIndex == 0)
            {
                string detail = reading.Valid ? $"Okundu · {reading.Step}/4 · {reading.Target} · tıklama yok" : reading.Detail;
                status.Text = detail; WriteState(detail, reading, reading.Kind != ReadingKind.Absent); return;
            }
            Option? choice = flow.Observe(reading, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - suspendedSeconds);
            if (choice != null)
            {
                // Recheck the same screen after asynchronous OCR and before posting any input.
                using var fresh = window.Capture(crop, clientSize);
                var confirm = await reader.ReadAsync(fresh);
                if (!running || generation != ticket) return;
                if (!confirm.Valid || confirm.Key != reading.Key) flow.Fail("Ekran seçimden önce değişti; tıklama yapılmadı");
                else
                {
                    phase = "click_posted"; WriteState($"{reading.Step}/4 · {reading.Target} için tıklama gönderiliyor", reading);
                    window.Click(new(crop.X + choice.Center.X, crop.Y + choice.Center.Y), crop, clientSize); clickCount++;
                    Directory.CreateDirectory(logRoot);
                    frame?.Save(Path.Combine(logRoot, $"step-{reading.Step}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png"));
                }
            }
            status.Text = flow.Status + $" · PID {window.Pid} · durdurma: {stopKey}";
            if (flow.Complete) phase = "complete";
            if (flow.Faulted) phase = "faulted";
            WriteState(flow.Status, reading);
            if (flow.Faulted) { running = false; generation++; timer.Stop(); SetEnabled(true); }
        }
        catch (Exception e) { if (running && generation == ticket) { StopRun("Durdu: " + e.Message); SaveError(e); } }
        finally { busy = false; }
    }
    void ShowPreview(Bitmap bitmap)
    {
        Image? old = preview.Image; preview.Image = new Bitmap(bitmap); old?.Dispose();
    }
    void WriteState(string detail, Reading? reading, bool? blocked = null)
    {
        try
        {
            Directory.CreateDirectory(logRoot);
            var state = new { utc = DateTimeOffset.UtcNow, version = "1.1", standalone = true, farm_connected = false,
                running, mode = mode.SelectedIndex == 0 ? "read_only" : "match", blocked = blocked ?? flow.Blocked, complete = flow.Complete, faulted = flow.Faulted,
                last_clicked_step = flow.LastClicked, detail, selected_pid = selected?.Pid,
                selected_path = selected?.Path, step = reading?.Step, target = reading?.Target, options = reading?.Options.Select(o => o.Word),
                phase, automatic = automatic.Checked, frame_count = frameCount, click_count = clickCount, stop_key = stopKey,
                roi = new { crop.X, crop.Y, crop.Width, crop.Height }, client = new { clientSize.Width, clientSize.Height },
                ocr_language = reader?.Language, raw_puzzle_text = reader?.LastText, finder_detail = finderDetail, backend = "window_messages",
                memory_detail = memoryDetail, memory_state = memoryState, memory_access = "0x410 (query + read only)" };
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            string pending = Path.Combine(logRoot, "integration-state.tmp"); File.WriteAllText(pending, json);
            File.Move(pending, Path.Combine(logRoot, "integration-state.json"), true);
            string change = phase + ":" + detail;
            if (change != lastLog) { File.AppendAllText(Path.Combine(logRoot, "events.jsonl"), JsonSerializer.Serialize(state) + Environment.NewLine); lastLog = change; }
        }
        catch { status.Text = detail + " · günlük yazılamadı"; }
    }
    void SaveError(Exception error)
    {
        try { Directory.CreateDirectory(logRoot); File.WriteAllText(Path.Combine(logRoot, "last-error.txt"), error.ToString()); } catch { }
    }
    internal async Task VerifyAutomaticSession(string output)
    {
        OpenDemo(); fixture!.PuzzleVisible = false; fixture.Invalidate(); fixture.Update();
        if (!automatic.Checked || mode.SelectedIndex != 1) throw new IOException("Automatic defaults are not ready");
        crop = Rectangle.Empty;
        await StartRun();
        if (!running) throw new IOException("UI could not connect: " + status.Text);
        var watch = Stopwatch.StartNew();
        while (frameCount < 2 && watch.Elapsed.TotalSeconds < 12) await Task.Delay(100);
        if (frameCount < 2 || flow.Blocked || clickCount != 0) throw new IOException("Idle scan did not wait without clicking: " + status.Text);
        fixture.PuzzleVisible = true; fixture.Invalidate(); fixture.Update();
        while (!flow.Complete && !flow.Faulted && watch.Elapsed.TotalSeconds < 35) await Task.Delay(100);
        if (!flow.Complete || clickCount != 4 || fixture.Accepted != 4 || fixture.Inputs != 4 || fixture.Attempts != 3)
            throw new IOException("Automatic arrival did not complete: " + status.Text + " / clicks " + clickCount);
        File.WriteAllText(Path.Combine(output, "automatic-session.json"), JsonSerializer.Serialize(new { pass = true,
            manual_area_selected = false, manually_notified_arrival = false, idle_frames = 2, frames = frameCount,
            clicks = clickCount, accepted = fixture.Accepted, attempts = fixture.Attempts, complete = flow.Complete, target_pid = Environment.ProcessId, game_accessed = false }, new JsonSerializerOptions { WriteIndented = true }));
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopRun("Uygulama kapandı"); fixture?.Close(); base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            running = false; generation++; timer.Dispose(); if (hotkey && IsHandleCreated) Native.UnregisterHotKey(Handle, 9);
            connecting?.Cancel(); connecting?.Dispose(); connecting = null; memory?.Dispose(); memory = null;
            fixture?.Dispose(); preview.Image?.Dispose(); preview.Image = null;
        }
        base.Dispose(disposing);
    }
}
````

### Ek: src/MemoryDetector.cs

SHA-256: `f14f167ac004a3870e328f965a62c23a4932aa2d24af3c504afb2c45510e838a`. Boyut: 9552 bayt.

````csharp
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace UnityPuzzleTest;

record MemoryState(bool Valid, bool Active, int Visible, int Mode, string Object, string Detail);

sealed class MemoryDetector : IDisposable
{
    internal const string BuildHash = "4DC9C526A895A10113C4CF23F2D199BFF283D2C7CE75F949DC49CEF15D27D622";
    internal const long TableRva = 0xDC94B8;
    readonly IntPtr handle;
    readonly WindowChoice window;
    readonly long moduleBase, moduleSize;
    long component;
    bool disposed;
    MemoryDetector(IntPtr handle, WindowChoice window, long moduleBase, long moduleSize)
    { this.handle = handle; this.window = window; this.moduleBase = moduleBase; this.moduleSize = moduleSize; }
    public static (MemoryDetector? Detector, string Detail) Connect(WindowChoice window, CancellationToken cancel)
    {
        if (!string.Equals(Path.GetFileName(window.Path), "TClient.exe", StringComparison.OrdinalIgnoreCase))
            return (null, "Bellek: bu pencere için istemci profili yok; görüntü izleniyor");
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(window.Path))) != BuildHash)
            return (null, "Bellek: istemci sürümü farklı; görüntü izleniyor");
        // Query-information + VM-read only. No VM-write, VM-operation, thread or debug access.
        IntPtr h = MemoryNative.OpenProcess(0x410, false, window.Pid);
        if (h == IntPtr.Zero) return (null, "Bellek: okuma erişimi açılamadı (Win32 " + Marshal.GetLastWin32Error() + "); görüntü izleniyor");
        MemoryDetector? detector = null;
        try
        {
            var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
            if (!MemoryNative.QueryFullProcessImageName(h, 0, path, ref length) || !string.Equals(path.ToString(), window.Path, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Bellek bağlantısında process yolu değişti");
            if (!MemoryNative.GetProcessTimes(h, out long created, out _, out _, out _) || DateTime.FromFileTimeUtc(created).Ticks != window.Created)
                throw new IOException("Bellek bağlantısında process oturumu değişti");
            using var process = Process.GetProcessById(window.Pid);
            var module = process.MainModule ?? throw new IOException("Ana modül okunamadı");
            if (!string.Equals(module.FileName, window.Path, StringComparison.OrdinalIgnoreCase)) throw new IOException("Ana modül yolu değişti");
            detector = new(h, window, module.BaseAddress.ToInt64(), module.ModuleMemorySize);
            if (detector.ReadPointer(detector.moduleBase + TableRva + 0x30) != detector.moduleBase + 0x384080 ||
                detector.ReadPointer(detector.moduleBase + TableRva + 0x160) != detector.moduleBase + 0x3839D0)
                throw new IOException("Bellekteki UI profil imzası farklı");
            detector.Find(cancel);
            if (detector.component == 0) { detector.Dispose(); return (null, "Bellek: UI nesnesi sınırlı taramada bulunamadı; görüntü izleniyor"); }
            return (detector, "Bellek: pencerenin açık/kapalı durumu izleniyor");
        }
        catch (OperationCanceledException) { if (detector != null) detector.Dispose(); else MemoryNative.CloseHandle(h); throw; }
        catch (Exception e) { if (detector != null) detector.Dispose(); else MemoryNative.CloseHandle(h); return (null, "Bellek: " + e.Message + "; görüntü izleniyor"); }
    }
    byte[] Read(long address, int length)
    {
        var result = new byte[length];
        if (!MemoryNative.ReadProcessMemory(handle, (IntPtr)address, result, (nuint)length, out var count) || count != (nuint)length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UI durumu okunamadı");
        return result;
    }
    long ReadPointer(long address) => BinaryPrimitives.ReadInt64LittleEndian(Read(address, 8));
    internal static MemoryState Parse(byte[] bytes, long expectedTable, long address)
    {
        if (bytes.Length < 0x330 || BinaryPrimitives.ReadInt64LittleEndian(bytes) != expectedTable)
            return new(false, false, -1, -1, "", "UI nesne kimliği doğrulanamadı");
        int visible = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x19C));
        int mode = bytes[0x328];
        if (visible is not (0 or 1) || (mode > 11 && mode != 0xFA))
            return new(false, false, visible, mode, "", "UI alanları beklenen aralıkta değil");
        bool active = visible == 1 && mode == 0xFA;
        return new(true, active, visible, mode, "0x" + address.ToString("X"), active ? "Bellek: doğrulama açık" : "Bellek: doğrulama kapalı");
    }
    bool Candidate(long address)
    {
        try
        {
            byte[] state = Read(address, 0x330);
            if (!Parse(state, moduleBase + TableRva, address).Valid) return false;
            long title = BinaryPrimitives.ReadInt64LittleEndian(state.AsSpan(0x288));
            if (title < 0x10000) return false;
            long childTable = ReadPointer(title);
            return childTable >= moduleBase && childTable < moduleBase + moduleSize;
        }
        catch { return false; }
    }
    void Find(CancellationToken cancel)
    {
        const long budget = 256L * 1024 * 1024;
        long address = 0, scanned = 0;
        var watch = Stopwatch.StartNew(); var found = new HashSet<long>();
        byte[] signature = BitConverter.GetBytes(moduleBase + TableRva);
        for (int regions = 0; regions < 12000 && scanned < budget && watch.Elapsed.TotalSeconds < 6; regions++)
        {
            cancel.ThrowIfCancellationRequested();
            if (MemoryNative.VirtualQueryEx(handle, (IntPtr)address, out var page, (nuint)Marshal.SizeOf<MemoryNative.Info>()) == 0) break;
            long end = page.BaseAddress + checked((long)page.RegionSize);
            if (end <= address) break;
            // UI instances are private committed allocations; skip images, mappings and inaccessible pages.
            if (page.State == 0x1000 && page.Type == 0x20000 && (page.Protect & 0x101) == 0 && (page.Protect & 0xEE) != 0)
            {
                for (long at = page.BaseAddress; at < end && scanned < budget && watch.Elapsed.TotalSeconds < 6;)
                {
                    cancel.ThrowIfCancellationRequested();
                    int length = (int)Math.Min(256 * 1024, end - at);
                    byte[] bytes;
                    try { bytes = Read(at, length); } catch { at += length; continue; }
                    scanned += length;
                    int from = 0;
                    while (from <= bytes.Length - 8)
                    {
                        int offset = bytes.AsSpan(from).IndexOf(signature);
                        if (offset < 0) break;
                        offset += from; long possible = at + offset;
                        if (possible % 8 == 0 && Candidate(possible)) found.Add(possible);
                        if (found.Count > 1) throw new IOException("Birden fazla UI nesnesi bulundu; bellek durumu belirsiz");
                        from = offset + 8;
                    }
                    at += length == 256 * 1024 ? length - 8 : length;
                }
            }
            address = end;
        }
        if (found.Count == 1) component = found.Single();
    }
    public MemoryState Snapshot()
    {
        if (disposed) return new(false, false, -1, -1, "", "Bellek izleme kapalı");
        try
        {
            if (!MemoryNative.GetProcessTimes(handle, out long created, out _, out _, out _) || DateTime.FromFileTimeUtc(created).Ticks != window.Created || !Native.IsWindow(window.Handle))
                return new(false, false, -1, -1, "", "Process oturumu değişti");
            if (!Candidate(component)) return new(false, false, -1, -1, "", "UI nesnesi değişti; görüntü izleniyor");
            return Parse(Read(component, 0x330), moduleBase + TableRva, component);
        }
        catch (Exception e) { return new(false, false, -1, -1, "", e.Message); }
    }
    public void Dispose() { if (disposed) return; disposed = true; MemoryNative.CloseHandle(handle); }
}

static class MemoryNative
{
    [StructLayout(LayoutKind.Sequential)] public struct Info
    {
        public long BaseAddress, AllocationBase;
        public uint AllocationProtect, Padding;
        public ulong RegionSize;
        public uint State, Protect, Type, Padding2;
    }
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] bytes, nuint length, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nuint VirtualQueryEx(IntPtr process, IntPtr address, out Info info, nuint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetProcessTimes(IntPtr process, out long created, out long exit, out long kernel, out long user);
}
````

### Ek: src/OcrReader.cs

SHA-256: `02f59444e61ba8c970bda74a774fa4f74ee374ef202bb6fa0fca064997800f0f`. Boyut: 6397 bayt.

````csharp
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace UnityPuzzleTest;

record WordBox(string Text, RectangleF Bounds);
sealed class OcrReader
{
    readonly OcrEngine engine;
    public string Language => engine.RecognizerLanguage.LanguageTag;
    public string LastText { get; private set; } = "";
    public LineBox[] LastLines { get; private set; } = [];
    static readonly HashSet<string> Vocabulary = ["SUN", "STAR", "MOON", "LEAF", "CROWN", "GEM", "SHIELD", "SWORD"];
    internal const string TargetPattern = @"TAR[GQ]ET[^\p{L}\p{N}\r\n]*([A-Z]+)";
    public OcrReader()
    {
        engine = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ??
            OcrEngine.TryCreateFromLanguage(new Language("tr-TR")) ?? OcrEngine.TryCreateFromUserProfileLanguages() ??
            throw new InvalidOperationException("Windows metin tanıma dili bulunamadı. Windows dil ayarlarından İngilizce veya Türkçe temel yazmayı ekleyin.");
    }
    public async Task<Reading> ReadAsync(Bitmap source)
    {
        using var image = Prepare(source);
        using var bytes = new MemoryStream(); image.Save(bytes, ImageFormat.Png);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes.ToArray()); await writer.StoreAsync().AsTask().ConfigureAwait(false);
            await writer.FlushAsync().AsTask().ConfigureAwait(false); writer.DetachStream();
        }
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
        using var software = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore).AsTask().ConfigureAwait(false);
        var result = await engine.RecognizeAsync(software).AsTask().ConfigureAwait(false);
        LastText = result.Text;
        float scale = image.Width / (float)source.Width;
        LastLines = result.Lines.Where(l => l.Words.Count > 0).Select(l =>
        {
            float left = (float)l.Words.Min(w => w.BoundingRect.X) / scale;
            float top = (float)l.Words.Min(w => w.BoundingRect.Y) / scale;
            float right = (float)l.Words.Max(w => w.BoundingRect.X + w.BoundingRect.Width) / scale;
            float bottom = (float)l.Words.Max(w => w.BoundingRect.Y + w.BoundingRect.Height) / scale;
            return new LineBox(l.Text, RectangleF.FromLTRB(left, top, right, bottom));
        }).ToArray();
        var words = result.Lines.SelectMany(l => l.Words).Select(w => new WordBox(w.Text,
            new((float)w.BoundingRect.X / scale, (float)w.BoundingRect.Y / scale,
                (float)w.BoundingRect.Width / scale, (float)w.BoundingRect.Height / scale))).ToArray();
        return Parse(result.Lines.Select(l => l.Text).ToArray(), words, source.Size);
    }
    public async Task<LocateResult> LocateAsync(Bitmap client)
    {
        await ReadAsync(client).ConfigureAwait(false);
        LocateResult result = PuzzleLocator.Locate(LastLines, client.Size);
        LastText = "";
        return result;
    }
    internal static Reading Parse(string[] lines, WordBox[] words, Size size)
    {
        string text = string.Join("\n", lines).ToUpperInvariant();
        // The label's stylized lowercase g is consistently read as q in the supplied frames.
        // Only this fixed label permits that variant; symbol names never use fuzzy matching.
        var targetMatch = Regex.Match(text, TargetPattern);
        var stepMatch = Regex.Match(text, @"STEP\s+([1-4])\s+OF\s+4\b");
        var attemptsMatch = Regex.Match(text, @"([0-9])\s+ATTEMPTS?\b");
        bool title = Regex.IsMatch(text, @"MACRO\s+PROTECTION\s+PUZZLE");
        string Normalize(string s) => Regex.Replace(s.ToUpperInvariant(), "[^A-Z]", "");
        var buttons = words.Where(w => w.Bounds.Top > size.Height * .64 && Vocabulary.Contains(Normalize(w.Text))).ToArray();
        if (!title && !targetMatch.Success && !stepMatch.Success && buttons.Length == 0) return Reading.Absent;
        if (!title || !targetMatch.Success || !stepMatch.Success || !attemptsMatch.Success || buttons.Length != 4)
            return Reading.Unknown("Başlık, hedef, adım veya dört seçenek birlikte okunamadı; tıklama yok");
        string target = targetMatch.Groups[1].Value;
        if (!Vocabulary.Contains(target)) return Reading.Unknown("Hedef tanınmadı; tıklama yok");
        int Slot(WordBox w) => (w.Bounds.Top + w.Bounds.Height / 2 > size.Height * .82 ? 2 : 0) + (w.Bounds.Left + w.Bounds.Width / 2 > size.Width / 2f ? 1 : 0);
        if (buttons.Select(Slot).Distinct().Count() != 4) return Reading.Unknown("2 × 2 seçenek düzeni doğrulanamadı; tıklama yok");
        var options = buttons.OrderBy(Slot).Select(w => new Option(Normalize(w.Text), new(
            (int)Math.Round(w.Bounds.Left + w.Bounds.Width / 2), (int)Math.Round(w.Bounds.Top + w.Bounds.Height / 2)))).ToArray();
        var r = new Reading(ReadingKind.Ready, target, int.Parse(stepMatch.Groups[1].Value), int.Parse(attemptsMatch.Groups[1].Value), options, "");
        return r.Valid ? r : Reading.Unknown("Tek bir kesin eşleşme veya kalan deneme doğrulanamadı; tıklama yok");
    }
    static Bitmap Prepare(Bitmap source)
    {
        double scale = Math.Min(2.5, Math.Min(OcrEngine.MaxImageDimension / (double)source.Width, OcrEngine.MaxImageDimension / (double)source.Height));
        var result = new Bitmap(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new[] {
            new float[] { -.299f, -.299f, -.299f, 0, 0 }, new float[] { -.587f, -.587f, -.587f, 0, 0 },
            new float[] { -.114f, -.114f, -.114f, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 1, 1, 1, 0, 1 } }));
        graphics.DrawImage(source, new Rectangle(Point.Empty, result.Size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return result;
    }
}
````

### Ek: src/Program.cs

SHA-256: `2f7c12f3310deae1e4e53fe09863a88cc3598faad613744efa4772c8ceedb377`. Boyut: 6165 bayt.

````csharp
using System.Text.Json;

namespace UnityPuzzleTest;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string output = Path.Combine(AppContext.BaseDirectory, "verification");
        try
        {
            if (args.Contains("--self-test")) { Directory.CreateDirectory(output); SelfTests.Run(output).GetAwaiter().GetResult(); return; }
            if (args.Contains("--integration-test")) { Directory.CreateDirectory(output); SelfTests.RunWindow(output); return; }
            if (args.Contains("--inspect-targets"))
            {
                Directory.CreateDirectory(output);
                bool f9 = Native.RegisterHotKey(IntPtr.Zero, 91, 0x4000, 0x78);
                int f9Error = f9 ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (f9) Native.UnregisterHotKey(IntPtr.Zero, 91);
                var targets = WindowChoice.List().Where(w => string.Equals(Path.GetFileName(w.Path), "TClient.exe", StringComparison.OrdinalIgnoreCase))
                    .Select(w => new { w.Pid, w.Title, w.Path, window = w.Handle.ToString("X"), minimized = Native.IsIconic(w.Handle) }).ToArray();
                File.WriteAllText(Path.Combine(output, "target-inspection.json"), JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, f9_available = f9, f9_error = f9Error, targets, screen_captured = false, input_sent = false }, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
            if (args.Contains("--memory-probe"))
            {
                Directory.CreateDirectory(output);
                using var process = System.Diagnostics.Process.GetProcessById(int.Parse(args[1]));
                var target = new WindowChoice(process.MainWindowHandle, process.Id, "Read-only diagnostic", Path.GetFullPath(args[2]), process.StartTime.ToUniversalTime().Ticks);
                var linked = MemoryDetector.Connect(target, CancellationToken.None);
                using var detector = linked.Detector;
                File.WriteAllText(Path.Combine(output, "memory-probe.json"), JsonSerializer.Serialize(new { pid = target.Pid, target.Path, access = "0x410", detail = linked.Detail,
                    state = detector?.Snapshot(), input_sent = false, writes_attempted = false }, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
            if (args.Contains("--auto-test")) { Directory.CreateDirectory(output); AutoTests.Run(output, args.Skip(1).ToArray()).GetAwaiter().GetResult(); return; }
            if (args.Contains("--automatic-session-test"))
            {
                Directory.CreateDirectory(output); Exception? failure = null;
                using var form = new MainForm();
                form.Shown += async (_, _) =>
                {
                    try { await form.VerifyAutomaticSession(output); }
                    catch (Exception e) { failure = e; }
                    finally { form.Close(); }
                };
                Application.Run(form); if (failure != null) throw failure; return;
            }
            if (args.Contains("--analyze-files"))
            {
                Directory.CreateDirectory(output); var reader = new OcrReader();
                var result = args.Skip(1).Select(path =>
                {
                    using var image = new Bitmap(path);
                    var reading = reader.ReadAsync(image).GetAwaiter().GetResult();
                    return new { file = path, raw_text = reader.LastText, reading, exact_match = reading.Valid ? reading.Options.Single(o => o.Word == reading.Target) : null };
                }).ToArray();
                File.WriteAllText(Path.Combine(output, "image-readings.json"), JsonSerializer.Serialize(new { language = reader.Language, input_sent = false, result }, new JsonSerializerOptions { WriteIndented = true }));
                if (result.Any(r => !r.reading.Valid)) Environment.ExitCode = 1;
                return;
            }
            if (args.Contains("--render-ui"))
            {
                Directory.CreateDirectory(output); using var form = new MainForm(true);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                using var graphics = Graphics.FromImage(bitmap); graphics.Clear(form.BackColor);
                foreach (Control control in form.Controls)
                {
                    using var part = new Bitmap(control.Width, control.Height);
                    if (control is ComboBox combo)
                    {
                        using var g = Graphics.FromImage(part); g.Clear(Color.White);
                        using var brush = new SolidBrush(Color.Black);
                        using var format = new StringFormat { LineAlignment = StringAlignment.Center };
                        g.DrawString(combo.Text, combo.Font, brush, new RectangleF(5, 1, part.Width - 24, part.Height - 2), format);
                        g.DrawString("▾", combo.Font, brush, new PointF(part.Width - 18, 2));
                    }
                    else control.DrawToBitmap(part, new(0, 0, part.Width, part.Height));
                    graphics.DrawImageUnscaled(part, control.Left, control.Top);
                }
                bitmap.Save(Path.Combine(output, "puzzle-test-ui.png")); return;
            }
            if (args.Length > 0) throw new ArgumentException("Bilinmeyen tanılama seçeneği: " + args[0]);
            using var mutex = new Mutex(true, @"Local\4UnityPuzzleTest", out bool owner);
            if (!owner) { MessageBox.Show("Puzzle Test zaten açık.", "4Unity Puzzle Test"); return; }
            try { Application.Run(new MainForm()); } finally { mutex.ReleaseMutex(); }
        }
        catch (Exception e)
        {
            if (args.Length > 0)
            {
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString()); Environment.ExitCode = 1;
            }
            else MessageBox.Show(e.Message, "4Unity Puzzle Test");
        }
    }
}
````

### Ek: src/PuzzleFlow.cs

SHA-256: `77b7377fcd6dd61c3c4286f8cfce596e8ef7f9984cf0813aca3888a9241c1302`. Boyut: 3692 bayt.

````csharp
namespace UnityPuzzleTest;

enum ReadingKind { Absent, Uncertain, Ready }
record Option(string Word, Point Center);
record Reading(ReadingKind Kind, string Target, int Step, int Attempts, Option[] Options, string Detail)
{
    public static Reading Absent => new(ReadingKind.Absent, "", 0, -1, [], "Test ekranı görünmüyor");
    public static Reading Unknown(string detail) => new(ReadingKind.Uncertain, "", 0, -1, [], detail);
    public string Key => $"{Step}:{Target}:{Attempts}:" + string.Join("|", Options.Select(o => $"{o.Word}@{o.Center.X},{o.Center.Y}"));
    public bool Valid => Kind == ReadingKind.Ready && Step is >= 1 and <= 4 && Attempts > 0 &&
        Options.Length == 4 && Options.Select(o => o.Word).Distinct().Count() == 4 && Options.Count(o => o.Word == Target) == 1;
}

// This model is also the future farm integration boundary. It never sends input itself.
sealed class PuzzleFlow
{
    public bool Blocked { get; private set; }
    public bool Faulted { get; private set; }
    public bool Complete { get; private set; }
    public int LastClicked { get; private set; }
    public string Status { get; private set; } = "Hazır";
    string stableKey = "";
    int stableCount, absentCount;
    double deadline, firstAbsent;
    public void Reset()
    {
        Blocked = Faulted = Complete = false; LastClicked = stableCount = absentCount = 0;
        deadline = firstAbsent = 0; stableKey = ""; Status = "Test ekranı bekleniyor";
    }
    public void Fail(string reason) { Faulted = Blocked = true; Complete = false; Status = reason; }
    public Option? Observe(Reading r, double now)
    {
        if (Faulted) return null;
        if (r.Kind == ReadingKind.Absent)
        {
            stableKey = ""; stableCount = 0;
            if (!Blocked) { if (!Complete) Status = "Test ekranı bekleniyor"; return null; }
            if (absentCount++ == 0) firstAbsent = now;
            if (absentCount >= 3 && now - firstAbsent >= 1)
            {
                if (LastClicked == 4) { Blocked = false; Complete = true; Status = "4/4 tamamlandı; ekran kapandı"; deadline = 0; }
                else Fail("Ekran erken kapandı; tamamlanma doğrulanamadı. Durdur / başlat.");
            }
            if (deadline > 0 && now >= deadline && Blocked) Fail("Ekranın kapanması doğrulanamadı. Durdur / başlat.");
            return null;
        }
        absentCount = 0;
        if (Complete)
        {
            Reset();
            Blocked = true;
            if (!r.Valid || r.Step != 1) { Status = "Yeni test için 1/4 bekleniyor"; return null; }
        }
        Blocked = true;
        if (deadline > 0 && now >= deadline) { Fail("Sonraki adım gelmedi; tekrar tıklanmadı. Durdur / başlat."); return null; }
        if (!r.Valid) { stableKey = ""; stableCount = 0; Status = r.Detail.Length > 0 ? r.Detail : "Okuma belirsiz; tıklama bekletiliyor"; return null; }
        if (r.Step < LastClicked || r.Step > LastClicked + 1) { Fail("Beklenmeyen adım sırası; tıklama durdu."); return null; }
        if (r.Step == LastClicked) { stableKey = ""; stableCount = 0; Status = LastClicked == 4 ? "4/4 seçildi; ekranın kapanması bekleniyor" : $"{LastClicked}/4 seçildi; sonraki adım bekleniyor"; return null; }
        if (r.Key != stableKey) { stableKey = r.Key; stableCount = 1; Status = $"{r.Step}/4 okunuyor; ikinci görüntü bekleniyor"; return null; }
        if (++stableCount < 2) return null;
        Option choice = r.Options.Single(o => o.Word == r.Target);
        LastClicked = r.Step; deadline = now + 12; stableKey = ""; stableCount = 0;
        Status = $"{r.Step}/4 · {r.Target} seçiliyor";
        return choice;
    }
}
````

### Ek: src/PuzzleLocator.cs

SHA-256: `54bb81a48c1c00936e8f6631e4d0ce06839c6774fd6fde8afe6e1f1493b01463`. Boyut: 2527 bayt.

````csharp
using System.Text.RegularExpressions;

namespace UnityPuzzleTest;

record LineBox(string Text, RectangleF Bounds);
record LocateResult(bool HeaderSeen, Rectangle Bounds, string Detail);

static class PuzzleLocator
{
    public static LocateResult Locate(LineBox[] lines, Size client)
    {
        static string Upper(string text) => text.ToUpperInvariant();
        var headers = lines.Where(l => Regex.IsMatch(Upper(l.Text), @"MACRO\s+PROTECTION\s+PUZZLE")).ToArray();
        if (headers.Length == 0) return new(false, Rectangle.Empty, "Bağlı · doğrulama kutusu bekleniyor");
        if (headers.Length != 1) return new(true, Rectangle.Empty, "Birden fazla kutu başlığı okundu; seçim bekletiliyor");
        var header = headers[0];
        float cx = header.Bounds.Left + header.Bounds.Width / 2;
        bool Aligned(LineBox l) => Math.Abs(l.Bounds.Left + l.Bounds.Width / 2 - cx) < header.Bounds.Width * .45;
        var targets = lines.Where(l => l.Bounds.Top > header.Bounds.Bottom && Aligned(l) &&
            Regex.IsMatch(Upper(l.Text), OcrReader.TargetPattern)).ToArray();
        var steps = lines.Where(l => l.Bounds.Top > header.Bounds.Bottom && Aligned(l) &&
            Regex.IsMatch(Upper(l.Text), @"STEP\s+[1-4]\s+OF\s+4\b")).ToArray();
        if (targets.Length != 1 || steps.Length != 1 || steps[0].Bounds.Top <= targets[0].Bounds.Bottom)
            return new(true, Rectangle.Empty, "Kutu başlığı bulundu; hedef ve adımın konumu henüz okunamadı");
        float targetCy = targets[0].Bounds.Top + targets[0].Bounds.Height / 2;
        float stepCy = steps[0].Bounds.Top + steps[0].Bounds.Height / 2;
        float gap = stepCy - targetCy;
        if (gap < 20 || gap > client.Height * .25) return new(true, Rectangle.Empty, "Kutu düzeni doğrulanamadı; seçim bekletiliyor");
        int height = (int)Math.Round(gap * 7.35), width = (int)Math.Round(height * .92);
        int top = (int)Math.Floor(header.Bounds.Top - header.Bounds.Height * .85);
        var candidate = new Rectangle((int)Math.Round(cx - width / 2f), top, width, height);
        var bounds = Rectangle.Intersect(candidate, new Rectangle(Point.Empty, client));
        if (bounds.Width < 200 || bounds.Height < 220 || bounds.Width < candidate.Width * .95 || bounds.Height < candidate.Height * .95)
            return new(true, Rectangle.Empty, "Kutunun tamamı görünür değil; oyun penceresini ekrana sığdırın");
        return new(true, bounds, "Doğrulama kutusu bulundu · seçenekler okunuyor");
    }
}
````

### Ek: src/SelfTests.cs

SHA-256: `af6f88a516518aa3644c2481405d640df2b5463c3a4da16d3ed0e99e742f5554`. Boyut: 10049 bayt.

````csharp
using System.Diagnostics;
using System.Text.Json;

namespace UnityPuzzleTest;

static class SelfTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++; if (!condition) throw new InvalidOperationException(message);
    }
    static Reading Sample(int step = 1, string target = "SUN") => new(ReadingKind.Ready, target, step, 3,
        [new("CROWN", new(110, 340)), new("STAR", new(280, 340)), new("SUN", new(110, 400)), new("LEAF", new(280, 400))], "");
    static Option? Stable(PuzzleFlow flow, Reading reading, double at) { flow.Observe(reading, at); return flow.Observe(reading, at + .6); }
    public static async Task Run(string output)
    {
        var flow = new PuzzleFlow(); flow.Reset();
        Check(flow.Observe(Sample(), 0) == null && flow.Blocked, "first reading must pause without clicking");
        Check(flow.Observe(Sample(), .6)?.Word == "SUN", "two stable frames must select the unique target");
        for (int i = 0; i < 8; i++) Check(flow.Observe(Sample(), 1 + i * .6) == null, "same step was clicked again");
        Check(flow.LastClicked == 1, "repeated step changed progress");
        Check(Stable(flow, Sample(2, "STAR"), 6)?.Word == "STAR", "next step not selected");
        Check(Stable(flow, Sample(3), 7.5)?.Word == "SUN", "step 3 not selected");
        Check(Stable(flow, Sample(4), 9)?.Word == "SUN", "step 4 not selected");
        Check(!flow.Complete && flow.Blocked, "last click alone must not complete the session");
        flow.Observe(Reading.Absent, 10); flow.Observe(Reading.Absent, 10.6);
        Check(!flow.Complete, "two absence frames must not complete");
        flow.Observe(Reading.Absent, 11.2);
        Check(flow.Complete && !flow.Blocked, "three absence frames must confirm completion");
        flow.Observe(Reading.Unknown("new uncertain panel"), 11.8);
        Check(flow.Blocked && !flow.Complete, "a new uncertain panel must block a completed session again");
        Check(Stable(flow, Sample(), 12)?.Word == "SUN" && flow.LastClicked == 1, "new session did not reset correctly");
        flow.Reset(); Stable(flow, Sample(), 0); flow.Observe(Sample(), 14);
        Check(flow.Faulted && flow.Blocked, "stalled step must fault without retry");
        Check(flow.Observe(Sample(2), 15) == null, "faulted session emitted input");
        flow.Reset(); Stable(flow, Sample(), 0); flow.Observe(Sample(3), 1);
        Check(flow.Faulted, "skipped step must fault");
        flow.Reset(); Stable(flow, Sample(), 0); flow.Observe(Reading.Absent, 1); flow.Observe(Reading.Absent, 1.6); flow.Observe(Reading.Absent, 2.2);
        Check(flow.Faulted && !flow.Complete, "early disappearance must not count as success");
        flow.Reset();
        Check(Stable(flow, Sample() with { Attempts = 0 }, 0) == null, "exhausted attempts must never click");
        Check(Stable(flow, Sample() with { Options = [new("SUN", new(1, 1)), new("SUN", new(2, 2)), new("STAR", new(3, 3)), new("LEAF", new(4, 4))] }, 2) == null, "ambiguous target must never click");
        Check(Stable(flow, Sample() with { Target = "MOON" }, 4) == null, "missing match must never click");
        flow.Reset(); flow.Observe(Sample(), 0); flow.Observe(Sample(1, "STAR"), .6);
        Check(flow.LastClicked == 0, "unstable target must not click");
        flow.Observe(Reading.Unknown("uncertain"), 1); Check(!flow.Complete && flow.Blocked, "uncertain reading must pause");
        string[] lines = ["Macro protection puzzle", "Target: SUN", "Step 1 of 4", "3 attempts - 90 seconds"];
        WordBox[] words = [new("CROWN", new(70, 330, 70, 18)), new("STAR", new(260, 330, 50, 18)), new("SUN", new(90, 390, 40, 18)), new("LEAF", new(260, 390, 50, 18))];
        Check(OcrReader.Parse(lines, words, new(408, 444)).Valid, "parser rejected valid grid");
        Check(OcrReader.Parse(lines.Select(s => s.Replace("Target", "Tarqet")).ToArray(), words, new(408, 444)).Valid, "known label font variant rejected");
        Check(!OcrReader.Parse(lines.Select(s => s.Replace("SUN", "SVM")).ToArray(), words, new(408, 444)).Valid, "symbol typo must not use fuzzy matching");
        Check(OcrReader.Parse(lines.Skip(1).ToArray(), words, new(408, 444)).Kind == ReadingKind.Uncertain, "missing header must not be absence");
        Check(OcrReader.Parse(["background"], [], new(408, 444)).Kind == ReadingKind.Absent, "background should be absent");
        Check(!OcrReader.Parse(lines, words.Take(3).ToArray(), new(408, 444)).Valid, "incomplete grid accepted");
        Check(!OcrReader.Parse(lines, words.Select(w => w with { Bounds = new(70, 330, 70, 18) }).ToArray(), new(408, 444)).Valid, "overlapping grid accepted");
        var reader = new OcrReader(); using var demo = new DemoForm();
        var readings = new List<Reading>(); var sequence = new[] { 2, 1, 1, 1 };
        for (int i = 0; i < 4; i++)
        {
            using var frame = demo.Snapshot(); frame.Save(Path.Combine(output, $"fixture-step-{i + 1}.png"));
            var reading = await reader.ReadAsync(frame).ConfigureAwait(false); readings.Add(reading);
            Check(reading.Valid && reading.Step == i + 1, $"OCR fixture step {i + 1} failed: {reading.Detail}");
            demo.Press(sequence[i]);
        }
        using (var closed = demo.Snapshot()) Check((await reader.ReadAsync(closed).ConfigureAwait(false)).Kind == ReadingKind.Absent, "completed fixture was not absent");
        Check(demo.Accepted == 4 && demo.Attempts == 3, "fixture sequence failed");
        File.WriteAllText(Path.Combine(output, "self-tests.json"), JsonSerializer.Serialize(new { pass = true, checks, language = reader.Language, live_game_accessed = false, readings }, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void RunWindow(string output)
    {
        Exception? failure = null;
        using var fixture = new DemoForm();
        fixture.Shown += async (_, _) =>
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                var target = new WindowChoice(fixture.Handle, Environment.ProcessId, fixture.Text, process.MainModule!.FileName!, process.StartTime.ToUniversalTime().Ticks);
                var reader = new OcrReader(); var flow = new PuzzleFlow(); flow.Reset();
                Native.SetForegroundWindow(fixture.Handle); await Task.Delay(200);
                Check(!(target with { Created = target.Created + 1 }).IsReady(out _), "changed process identity accepted");
                Check(!target.IsReady(out _, new Size(fixture.ClientSize.Width + 1, fixture.ClientSize.Height)), "changed client geometry accepted");
                bool rejected = false;
                try { target.Click(new(-1, -1), fixture.PuzzleBounds, fixture.ClientSize); } catch (IOException) { rejected = true; }
                Check(rejected && fixture.Inputs == 0, "out-of-region click was sent");
                using (var cover = new Form { Text = "Yerel odak testi", ClientSize = new(150, 100), StartPosition = FormStartPosition.CenterScreen })
                {
                    cover.Show(); Native.SetForegroundWindow(cover.Handle); await Task.Delay(100);
                    Check(!target.IsReady(out _), "background target accepted");
                    cover.Close();
                }
                Native.SetForegroundWindow(fixture.Handle); await Task.Delay(100);
                fixture.WindowState = FormWindowState.Minimized; await Task.Delay(100);
                Check(!target.IsReady(out _), "minimized target accepted");
                fixture.WindowState = FormWindowState.Normal; fixture.Activate(); Native.SetForegroundWindow(fixture.Handle); await Task.Delay(500);
                for (int i = 1; i <= 4; i++)
                {
                    using var first = target.Capture(fixture.PuzzleBounds, fixture.ClientSize);
                    var a = await reader.ReadAsync(first);
                    Check(a.Valid && a.Step == i, "native capture OCR failed");
                    Check(flow.Observe(a, i * 2) == null, "first native frame emitted input");
                    using var second = target.Capture(fixture.PuzzleBounds, fixture.ClientSize);
                    var b = await reader.ReadAsync(second);
                    var choice = flow.Observe(b, i * 2 + .6);
                    if (choice == null)
                    {
                        File.WriteAllText(Path.Combine(output, "native-reading-debug.json"), JsonSerializer.Serialize(new { a, b, state = flow.Status }, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    Check(choice != null, "stable native frames did not select");
                    target.Click(new(fixture.PuzzleBounds.X + choice!.Center.X, fixture.PuzzleBounds.Y + choice.Center.Y), fixture.PuzzleBounds, fixture.ClientSize);
                    await Task.Delay(160);
                    Check(fixture.Accepted == i, "posted click did not advance fixture");
                }
                for (int i = 0; i < 3; i++)
                {
                    using var frame = target.Capture(fixture.PuzzleBounds, fixture.ClientSize);
                    flow.Observe(await reader.ReadAsync(frame), 10 + i * .6);
                }
                Check(flow.Complete && !flow.Blocked, "native full sequence did not complete");
                Check(fixture.Inputs == 4 && fixture.Attempts == 3, "native sequence produced wrong or extra input");
                File.WriteAllText(Path.Combine(output, "window-tests.json"), JsonSerializer.Serialize(new { pass = true, checks, inputs = fixture.Inputs,
                    accepted = fixture.Accepted, target_pid = Environment.ProcessId, live_game_accessed = false, complete = flow.Complete }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) { failure = e; }
            finally { fixture.Close(); }
        };
        Application.Run(fixture);
        if (failure != null) throw failure;
    }
}
````

### Ek: src/TargetWindow.cs

SHA-256: `82947640cf79cf6a2a54dd3958aad28f8ecf2c2c10063742453247b1c84002d5`. Boyut: 7137 bayt.

````csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace UnityPuzzleTest;

sealed record WindowChoice(IntPtr Handle, int Pid, string Title, string Path, long Created)
{
    public override string ToString() => $"{Title}  ·  PID {Pid}  ·  {System.IO.Path.GetFileName(Path)}";
    public static WindowChoice[] List()
    {
        var choices = new List<WindowChoice>();
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h) || Native.GetWindowTextLength(h) == 0) return true;
            Native.GetWindowThreadProcessId(h, out int pid);
            if (pid == Environment.ProcessId) return true;
            try
            {
                using var p = Process.GetProcessById(pid);
                string path = p.MainModule?.FileName ?? "";
                var text = new StringBuilder(512); Native.GetWindowText(h, text, text.Capacity);
                if (path.Length > 0) choices.Add(new(h, pid, text.ToString(), path, p.StartTime.ToUniversalTime().Ticks));
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return choices.OrderBy(c => c.Title).ToArray();
    }
    public bool IsReady(out string reason, Size? expected = null, Rectangle? region = null)
    {
        reason = "";
        if (!Native.IsWindow(Handle)) { reason = "Seçilen oyun penceresi kapandı; tekrar client seçin"; return false; }
        if (Native.IsIconic(Handle)) { reason = "Bekliyor: seçilen oyun penceresi küçültülmüş"; return false; }
        Native.GetWindowThreadProcessId(Handle, out int pid);
        try
        {
            using var p = Process.GetProcessById(pid);
            if (pid != Pid || p.StartTime.ToUniversalTime().Ticks != Created || !string.Equals(p.MainModule?.FileName, Path, StringComparison.OrdinalIgnoreCase))
                { reason = "Test uygulaması oturumu değişti; tekrar pencere seçin"; return false; }
        }
        catch { reason = "Test uygulamasına erişilemiyor"; return false; }
        if (Native.GetAncestor(Native.GetForegroundWindow(), 2) != Handle) { reason = "Bekliyor: seçilen test penceresini öne getirin"; return false; }
        if (!Native.GetClientRect(Handle, out var rect)) { reason = "Pencere boyutu okunamadı"; return false; }
        var size = new Size(rect.Right, rect.Bottom);
        if (expected != null && expected.Value != size) { reason = "Pencere boyutu değişti; alanı tekrar seçin"; return false; }
        if (region != null)
        {
            if (!new Rectangle(Point.Empty, size).Contains(region.Value)) { reason = "Seçilen alan pencere dışında"; return false; }
            var origin = new Native.POINT();
            if (!Native.ClientToScreen(Handle, ref origin)) { reason = "Ekran konumu okunamadı"; return false; }
            Rectangle screen = region.Value; screen.Offset(origin.X, origin.Y);
            if (!SystemInformation.VirtualScreen.Contains(screen)) { reason = "Test alanının tamamı görünür olmalı"; return false; }
            foreach (double x in new[] { .08, .5, .92 }) foreach (double y in new[] { .08, .5, .92 })
            {
                var at = new Native.POINT { X = screen.Left + (int)(screen.Width * x), Y = screen.Top + (int)(screen.Height * y) };
                IntPtr top = Native.WindowFromPoint(at);
                if (top != Handle && !Native.IsChild(Handle, top)) { reason = "Test alanının üstünde başka bir pencere var"; return false; }
            }
        }
        return true;
    }
    public Bitmap Capture(Rectangle? crop, Size? expected = null)
    {
        if (!IsReady(out string reason, expected, crop)) throw new IOException(reason);
        Native.GetClientRect(Handle, out var rect);
        var bounds = crop ?? new Rectangle(0, 0, rect.Right, rect.Bottom);
        var origin = new Native.POINT { X = bounds.Left, Y = bounds.Top };
        if (!Native.ClientToScreen(Handle, ref origin)) throw new Win32Exception();
        var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try { using var g = Graphics.FromImage(bitmap); g.CopyFromScreen(origin.X, origin.Y, 0, 0, bitmap.Size); return bitmap; }
        catch { bitmap.Dispose(); throw; }
    }
    public void Click(Point point, Rectangle region, Size expected)
    {
        if (!region.Contains(point)) throw new IOException("Tıklama test alanı dışında");
        if (!IsReady(out string reason, expected, region)) throw new IOException("Tıklama koşulları değişti: " + reason);
        if (point.X is < 0 or > 32767 || point.Y is < 0 or > 32767) throw new IOException("Tıklama konumu desteklenmiyor");
        IntPtr position = (IntPtr)((point.Y << 16) | point.X);
        if (!Native.PostMessage(Handle, 0x200, IntPtr.Zero, position)) throw new Win32Exception();
        bool down = Native.PostMessage(Handle, 0x201, (IntPtr)1, position);
        bool up = Native.PostMessage(Handle, 0x202, IntPtr.Zero, position);
        if (!down || !up) throw new IOException("Test penceresine tıklama iletilemedi");
    }
}

static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder text, int size);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT rectangle);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
}
````

### Ek: research/puzzle_static_probe.py

SHA-256: `b653f0e49152cf05ddccef075561b0ef67508f33419fdefddc23116daff87626`. Boyut: 2353 bayt.

````python
"""Read-only offline inspection of UI-related strings and RTTI. Never opens a process."""
import hashlib
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / 'speed_analysis_deps'))
import pefile

root = Path(r'C:\Games\4Unity')
output = Path('logs/puzzle_static')
output.mkdir(parents=True, exist_ok=True)
patterns = [b'captcha', b'puzzle', b'macro protection', b'matching symbol', b'attempts', b'Target:', b'protect']
report = []
for path in sorted(root.iterdir()):
    if path.suffix.lower() not in ['.exe', '.dll']:
        continue
    data = path.read_bytes()
    hits = []
    lower = data.lower()
    for term in patterns:
        for encoding in ['ascii', 'utf-16le']:
            needle = term.decode().encode(encoding)
            at = 0
            while len(hits) < 100:
                at = lower.find(needle, at)
                if at < 0:
                    break
                start = max(0, at - (40 if encoding == 'ascii' else 80))
                end = min(len(data), at + (120 if encoding == 'ascii' else 240))
                text = data[start:end].decode(encoding, errors='replace')
                hits.append({'term': term.decode(), 'offset': hex(at), 'encoding': encoding, 'context': text})
                at += len(needle)
    rtti = [m.group().decode('ascii') for m in re.finditer(rb'\.\?AV[A-Za-z0-9_?$]+@@', data)
            if any(word in m.group().lower() for word in [b'captcha', b'puzzle', b'macro', b'protect'])]
    if not hits and not rtti:
        continue
    pe = pefile.PE(data=data, fast_load=True)
    for hit in hits:
        try:
            rva = pe.get_rva_from_offset(int(hit['offset'], 16))
            if rva is not None:
                hit['rva'] = hex(rva)
        except pefile.PEFormatError:
            pass
    report.append({'file': path.name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'image_base': hex(pe.OPTIONAL_HEADER.ImageBase), 'rtti': sorted(set(rtti)), 'hits': hits})
(output / 'strings.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
for item in report:
    print(item['file'], item['sha256'], 'rtti=', item['rtti'])
    for hit in item['hits']:
        text = re.sub(r'[^\x20-\x7e]', '.', hit['context'])
        print(hit.get('rva', hit['offset']), hit['term'], hit['encoding'], text)
````

### Ek: research/puzzle_string_refs.py

SHA-256: `089daf4c93fc440a4664c2db591488c0742d0a5c3bbab6127119157c39e31e55`. Boyut: 2075 bayt.

````python
"""Disassemble only static references to the puzzle's display labels; no process access."""
import json
import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / 'speed_analysis_deps'))
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

path = Path(r'C:\Games\4Unity\TClient.exe')
blob = path.read_bytes()
pe = pefile.PE(data=blob)
base = pe.OPTIONAL_HEADER.ImageBase
labels = {}
for literal in [b'Macro protection puzzle\0', b'Click the matching symbol\0', b'Target:  %s\0', b'Step %u of %u\0', b'%u attempts - %u seconds\0']:
    off = blob.find(literal)
    if off >= 0:
        labels[pe.get_rva_from_offset(off)] = literal[:-1].decode()
decoder = Cs(CS_ARCH_X86, CS_MODE_64)
entries = [(e.struct.BeginAddress, e.struct.EndAddress) for e in pe.DIRECTORY_ENTRY_EXCEPTION]
refs = []
for sec in pe.sections:
    if not sec.Characteristics & 0x20000000:
        continue
    data = sec.get_data()
    for match in re.finditer(rb'[\x48-\x4f]\x8d[\x05\x0d\x15\x1d\x25\x2d\x35\x3d].{4}', data, re.S):
        rva = sec.VirtualAddress + match.start()
        target = rva + 7 + struct.unpack_from('<i', match.group(), 3)[0]
        if target not in labels:
            continue
        bounds = next(((b, e) for b, e in entries if b <= rva < e), None)
        if bounds is None:
            continue
        b, e = bounds
        instructions = list(decoder.disasm(blob[pe.get_offset_from_rva(b):pe.get_offset_from_rva(b) + e - b], base + b))
        detail = [{'rva': hex(i.address-base), 'instruction': i.mnemonic + ' ' + i.op_str} for i in instructions]
        refs.append({'label': labels[target], 'label_rva': hex(target), 'ref_rva': hex(rva), 'function': [hex(b),hex(e)], 'instructions': detail})
out = Path('logs/puzzle_static/refs.json')
out.write_text(json.dumps({'labels': {hex(k):v for k,v in labels.items()}, 'refs': refs}, indent=2), encoding='utf-8')
for ref in refs:
    print(ref['label'], ref['ref_rva'], ref['function'])
    for i in ref['instructions']:
        print(i['rva'], i['instruction'])
````

### Ek: research/puzzle_vtable_probe.py

SHA-256: `795d01ed9122c349a2dac305964363fe87aa745974290c96d8a9846c41ebd5f5`. Boyut: 1897 bayt.

````python
"""Recover UI class identity and vtable from the static display method; no live process access."""
import json
import struct
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / 'speed_analysis_deps'))
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

blob = Path(r'C:\Games\4Unity\TClient.exe').read_bytes()
pe = pefile.PE(data=blob)
base = pe.OPTIONAL_HEADER.ImageBase
def raw(rva, size):
    try:
        off = pe.get_offset_from_rva(rva)
        return blob[off:off+size]
    except (pefile.PEFormatError, TypeError):
        return b''
results = []
for sec in pe.sections:
    if sec.Characteristics & 0x20000000:
        continue
    data = sec.get_data()
    needle = struct.pack('<Q', base + 0x3839D0)
    pos = data.find(needle)
    while pos >= 0:
        hit = sec.VirtualAddress + pos
        for back in range(8, 256*8, 8):
            pointer = raw(hit-back, 8)
            if len(pointer) != 8:
                break
            col = struct.unpack('<Q', pointer)[0] - base
            fields = raw(col, 24) if 0 <= col < pe.OPTIONAL_HEADER.SizeOfImage else b''
            if len(fields) != 24:
                continue
            sig, offset, cd, td, chd, own = struct.unpack('<6I', fields)
            if sig != 1 or own != col or offset != 0:
                continue
            name = raw(td+16, 180).split(b'\0')[0].decode('ascii', errors='replace')
            vt = hit-back+8
            record = {'method': hex(hit), 'vtable': hex(vt), 'slot': hex(hit-vt), 'col': hex(col), 'class': name,
                      'slots': {hex(i*8):hex(struct.unpack('<Q', raw(vt+i*8,8))[0]-base) for i in range(45)}}
            results.append(record)
            break
        pos = data.find(needle, pos+1)
Path('logs/puzzle_static/vtable.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(results, indent=2))
````

### Ek: research/requirements.txt

SHA-256: `94b6292179914274eb5d637b9681a4843da3e5bc2b007d2a5554f353a266a80c`. Boyut: 18 bayt.

````text
pefile
capstone
````

### Ek: src/docs/puzzle_detection_profile.md

SHA-256: `e63a2c4250b63b8c00d5130caebe6b9b4c346b57d5a9d51e161c95b31587c62c`. Boyut: 2375 bayt.

````markdown
# Read-only puzzle visibility profile

Analyzed client: C:/Games/4Unity/TClient.exe, 16,549,888 bytes.

SHA-256: 4dc9c526a895a10113c4cf23f2d199bff283d2c7ce75f949dc49cef15d27d622.

Static inspection found the puzzle strings and references in the dialog update routine. RTTI identifies CTSecuritySystemDlg. Addresses below are RVAs or object-relative offsets, not fixed runtime addresses.

| Item | Location | Evidence/interpretation |
|---|---|---|
| Class vtable | RVA 0xDC94B8 | RTTI COL RVA 0xE540C0 |
| Show/hide override | Vtable slot 0x30 → RVA 0x384080 | Tests mode and visibility/challenge state |
| Render override | Vtable slot 0x160 → RVA 0x3839D0 | Security dialog rendering |
| Matching display update | RVA 0x3840A0 | References title, target, stage and attempts |
| Puzzle mode | Object byte +0x328 | Display update sets 0xFA for matching puzzle |
| Visibility | Object DWORD +0x19C | Base UI methods write/read 0 or 1 |
| Title control pointer | Object pointer +0x288 | Additional candidate validation |

The observer requires visibility 1 and mode 0xFA for an active matching puzzle. Other modes are not treated as the puzzle. It opens the selected process with 0x410 (query information and VM read); it requests no write, VM operation, debug or thread creation access. Runtime addresses use the loaded module base.

Only committed readable private allocations are scanned, with a 256 MiB / six-second bound and a 12,000-region cap. Multiple candidates found within that budget are rejected; uniqueness outside that range is not established. Loaded vtable method addresses and a title child object's image-resident vtable are validated. Snapshot reads revalidate identity. Unsupported or invalid states fall back to visual detection. Answer selection remains verified through OCR.

Static evidence: workspace logs/puzzle_static/strings.json, refs.json and vtable.json, produced by tools/puzzle_static_probe.py, puzzle_string_refs.py and puzzle_vtable_probe.py. Packaged verification includes these reports.

Live limitation: the read-only probe of the elevated client (PID 900 at probe time) returned Win32 5 access denied. No live UI object or open/closed transition was verified. The packaged EXE requests administrator approval. Synthetic byte parser tests and automatic four-stage interaction with the helper's fixture are not live memory validation.
````

### Ek: src/KULLANIM.txt

SHA-256: `8128b33b939df0e7f5c0572a6bc3deb64182ff3bc081c71829db6768e1d2dd31`. Boyut: 4255 bayt.

````text
4Unity Puzzle Test v1.1 — bağımsız offline test yardımcısı

4UnityPuzzleTest.exe dosyasını aç ve Windows yönetici onayını tamamla.
Windows 10 (19041 ve üzeri) veya Windows 11 gerekir. .NET paket içindedir.

OTOMATİK KULLANIM
1. Kendi offline oyununu pencere modunda aç.
2. Listeden TClient.exe olan doğru oyun penceresini seç. Gerekirse Yenile kullan.
3. Kutuyu otomatik bul işaretli kalsın. Eşleştir · offline test varsayılan moddur.
4. Bağlan ve başlat'a bas. Puzzle henüz açık olmasa da izleme başlar.
5. Oyunu önde ve görünür tut. Puzzle geldiğinde program kendisi kutuyu bulur,
   hedefi ve seçenekleri okur, eşleşen seçeneğe tıklar. Alan çizmek veya
   puzzle geldiğini ayrıca bildirmek gerekmez.
6. Dört adımdan sonra kutunun kapandığı doğrulanır. Program çalışmaya devam
   eder ve sonraki puzzle için tekrar 1/4 bekler.
7. F9 veya Durdur ile durdur. F9 başka uygulamada kullanılıyorsa Ctrl+Shift+F9
   denenir; etkin kısayol durum satırında gösterilir.

YEREL DENEME
Yerel deneme aç → Bağlan ve başlat. Bu uygulamanın kendi penceresinde dört
adım tamamlanır. Gerçek oyuna girdi göndermez.

BELLEKTEN OTOMATİK ALGILAMA
Bu TClient.exe sürümünün CTSecuritySystemDlg nesnesinde görünürlük ve
eşleştirme türü alanları bulundu. Başlangıçta yalnız okuma erişimiyle
nesne aranır; bulunursa pencerenin açık/kapalı durumu izlenir.
İstemci sürümü farklıysa, erişim alınamazsa veya nesne bulunamazsa görüntü
ile otomatik arama devam eder. Durum satırı ve logs/integration-state.json
hangi yöntemin çalıştığını ve nedenini gösterir.
Bellek sinyali cevap seçmez; her seçim ayrıca ekrandan doğrulanır.
Oyun belleğine yazılmaz veya koruma kodu değiştirilmez.
Canlı bellek geçişi geliştirme oturumunda doğrulanamadı: yönetici olarak
çalışan istemciye normal tanılama işlemi Win32 5 erişim reddi aldı.
Yeni EXE yönetici onayı ister; gerçek istemcideki sonuç ayrıca denenmelidir.

BEKLEME / HATA
- Oyun arkadaysa, küçültülmüşse veya başka pencere alanı kapatıyorsa ekran
  okuma/tıklama bekler. Bellek izleme bağlıysa açık durumunu yine gösterebilir.
- Başlatma hataları mesaj kutusunda ve logs/last-error.txt dosyasında görünür.
- logs/integration-state.json: PID, bağlantı aşaması, okuma/tıklama sayısı,
  bellek durumu, okunan hedef/adım ve son neden.
- logs/events.jsonl: durum değişiklikleri. Tıklama kareleri logs altında tutulur.
- Elle alan seçimi isteğe bağlıdır. Bu seçim otomatik kutu bulmayı kapatır.
- Yalnız oku · tıklama yok modu, okunan bilgiyi kontrol etmek içindir.

SEÇİM KURALLARI
- Desteklenen kelimeler: SUN, STAR, MOON, LEAF, CROWN, GEM, SHIELD, SWORD.
- Başlık, hedef, adım, kalan deneme ve dört farklı seçenek birlikte okunmalıdır.
- Sembol adlarına yaklaşık eşleştirme yapılmaz; tek bir kesin eşleşme gerekir.
- İki aynı okuma ve tıklama öncesinde yeni bir kontrol gerekir.
- Her adıma en fazla bir tıklama gönderilir. Sonraki adım 12 saniyede gelmezse
  durur. Odak kaybında bu süre bekletilir. Yeniden başlatma kullanıcıya aittir.
- Akış 1/4'ten başlar. Belirsizlikte tıklama yapılmaz. Aktif adımda pencere
  boyutu veya oyun oturumu değişirse durur.
- Tıklama seçili pencereye Windows fare mesajıyla gönderilir. Gerçek oyun
  istemcisinin bu mesajı kabul ettiği henüz doğrulanmadı.
- Windows metin tanıma dili yoksa Windows dil ayarlarından İngilizce veya
  Türkçe temel yazma özelliğini ekle. Program dil indirmez.

FARM BAĞLANTISI
Bu bağımsız sürüm mevcut farm EXE'sini durdurmaz veya yeniden başlatmaz.
logs/integration-state.json sonraki birleştirme için durum arayüzüdür.

DOĞRULAMA
41 davranış/OCR kontrolü, 38 otomatik bulma/bellek alanı kontrolü ve
23 yerel pencere kontrolü geçti. Dört örnek görüntü üç ölçekte okundu.
Tam uygulama testi: önce boş ekran izlendi; sonra beliren puzzle kullanıcı
bildirimi veya alan seçimi olmadan algılandı; dört doğru seçimle kapandı.
Gerçek oyuna test sırasında girdi gönderilmedi. Canlı bellek geçişi doğrulanmadı.
UAC: requireAdministrator / uiAccess=false. Dijital yayıncı imzası değildir.
````

### Ek: src/README.md

SHA-256: `014416459ce4c855cb0008be2dd80f496e0f737859b4cdcd699babb735c7abdd`. Boyut: 4903 bayt.

````markdown
# 4Unity Puzzle Test v1.1

Standalone Windows x64 helper for the user's owned offline test client. Select the client and start monitoring before a puzzle appears. Automatic dialog location and matching are enabled by default; manual cropping and read-only operation remain available. The existing farm executable is separate and cannot be paused by this release.

Read KULLANIM.txt for the Turkish workflow. Normal EXE startup requires UAC approval (requireAdministrator, uiAccess=false). The self-contained release includes .NET. Windows OCR uses an installed English or Turkish recognizer, falling back to the user profile; languages are not downloaded automatically.

## Automatic detection

Whole-client OCR locates the unique puzzle title, aligned target label and stage label, then estimates the dialog bounds from their geometry. The cropped dialog is read independently. Supported symbols are SUN, STAR, MOON, LEAF, CROWN, GEM, SHIELD and SWORD. The fixed Target label tolerates its observed OCR spelling and punctuation; symbol names require exact recognition.

A read-only memory observer is provided for the exact TClient.exe build documented in [the profile](docs/puzzle_detection_profile.md). It validates the disk hash, loaded vtable methods, process creation time and UI object shape before observing visibility and puzzle mode. A bounded private-memory scan runs once per connection. Unsupported builds, access denial or missing/changed objects fall back to visual scanning. Memory never selects an answer or replaces the OCR checks.

The elevated game denied the development process's read-only connection with Win32 error 5. Static fields and parser checks are established; live open/closed transitions remain unverified. The packaged application requests administrator elevation on ordinary startup.

## Selection and input

The title, target, stage 1–4, remaining attempts and four distinct symbols in a 2×2 layout must be present. Two consecutive identical readings and a fresh third reading precede input. PID, creation time, path, HWND ownership, foreground, client geometry, ROI and sampled occlusion are checked before input.

Each stage receives at most one mouse down/up pair. A 12-second progression timeout stops the session; focus waiting pauses that clock. Stage jumps and premature disappearance fault. Completion requires three absent observations spanning at least one second after stage 4. An active memory signal prevents unreadable images from being treated as successful closure. Monitoring continues after completion and starts the next session at step 1.

Input uses PostMessage to the selected HWND; the system cursor is untouched. The actual game client's acceptance remains unverified. Screen reading and clicking require a visible foreground window; a connected memory observer can report activity while waiting for focus. F9 cancels pending decisions; Ctrl+Shift+F9 is a fallback if F9 is occupied. Startup failures are displayed and logged.

## Farm integration boundary

logs/integration-state.json is atomically replaced with timestamp, connection phase, selected process, frame/click counts, reading, memory observation and running/blocked/faulted/completed state. It is an observation interface, not connected farm control. A future consumer should stop on a stale/missing record, stopped/faulted helper or blocked state, and preserve its own user-stop state. Focus waiting publishes a blocked heartbeat. Events and cropped click frames are local.

## Build and verification

Build the csproj with dotnet build -c Release. The resulting DLL supports --self-test, --integration-test, --automatic-session-test, --auto-test followed by reference image paths, and --render-ui. Do not run visible window tests concurrently.

Publish with dotnet publish tools/4UnityPuzzleTest/4UnityPuzzleTest.csproj -c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist/4UnityPuzzleTest-v1.1.

Passed: 41 state/parser/OCR checks; 38 automatic location and memory parser checks using four supplied images at three scales; 23 native fixture checks. The full application test begins with a hidden fixture puzzle, observes idle frames without input, then detects arrival and completes exactly four accepted selections with no manual area selection or user arrival notification. All input tests target this application's own fixture. These are not live game or live memory transition validation.

--analyze-files reads image files without input. --inspect-targets lists matching windows without screen capture. --memory-probe <pid> <path> attempts a read-only connection and writes its result. Reports are in verification.

OCR reference: [Microsoft OcrEngine.RecognizeAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.recognizeasync?view=winrt-26100).
````

### Ek: evidence/auto-tests.json

SHA-256: `da760c7dcd94229ea8161d7d8789990324f371ea3442a9d5886826e7238fa693`. Boyut: 22522 bayt.

````json
{
  "pass": true,
  "checks": 38,
  "game_accessed": false,
  "input_sent": false,
  "results": [
    {
      "fixture": true,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 24,
            "Y": 34
          },
          "Size": {
            "IsEmpty": false,
            "Width": 406,
            "Height": 441
          },
          "X": 24,
          "Y": 34,
          "Width": 406,
          "Height": 441,
          "Left": 24,
          "Top": 34,
          "Right": 430,
          "Bottom": 475,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 1,
        "Attempts": 3,
        "Options": [
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 114,
              "Y": 347
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 292,
              "Y": 347
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 114,
              "Y": 405
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 293,
              "Y": 405
            }
          }
        ],
        "Detail": "",
        "Key": "1:SUN:3:CROWN@114,347|STAR@292,347|SUN@114,405|LEAF@293,405",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-e7f7b9fb-73ef-4f85-a2ad-1a68ad3bbc54.jpg",
      "factor": 1,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 518,
            "Y": 181
          },
          "Size": {
            "IsEmpty": false,
            "Width": 402,
            "Height": 437
          },
          "X": 518,
          "Y": 181,
          "Width": 402,
          "Height": 437,
          "Left": 518,
          "Top": 181,
          "Right": 920,
          "Bottom": 618,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 1,
        "Attempts": 3,
        "Options": [
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 119,
              "Y": 343
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 282,
              "Y": 343
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 120,
              "Y": 399
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 283,
              "Y": 399
            }
          }
        ],
        "Detail": "",
        "Key": "1:SUN:3:CROWN@119,343|STAR@282,343|SUN@120,399|LEAF@283,399",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-e7f7b9fb-73ef-4f85-a2ad-1a68ad3bbc54.jpg",
      "factor": 1.25,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 520,
            "Y": 182
          },
          "Size": {
            "IsEmpty": false,
            "Width": 503,
            "Height": 547
          },
          "X": 520,
          "Y": 182,
          "Width": 503,
          "Height": 547,
          "Left": 520,
          "Top": 182,
          "Right": 1023,
          "Bottom": 729,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 1,
        "Attempts": 3,
        "Options": [
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 149,
              "Y": 428
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 352,
              "Y": 428
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 150,
              "Y": 498
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 353,
              "Y": 498
            }
          }
        ],
        "Detail": "",
        "Key": "1:SUN:3:CROWN@149,428|STAR@352,428|SUN@150,498|LEAF@353,498",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-e7f7b9fb-73ef-4f85-a2ad-1a68ad3bbc54.jpg",
      "factor": 0.85,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 516,
            "Y": 181
          },
          "Size": {
            "IsEmpty": false,
            "Width": 342,
            "Height": 372
          },
          "X": 516,
          "Y": 181,
          "Width": 342,
          "Height": 372,
          "Left": 516,
          "Top": 181,
          "Right": 858,
          "Bottom": 553,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 1,
        "Attempts": 3,
        "Options": [
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 102,
              "Y": 291
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 240,
              "Y": 291
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 103,
              "Y": 339
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 241,
              "Y": 339
            }
          }
        ],
        "Detail": "",
        "Key": "1:SUN:3:CROWN@102,291|STAR@240,291|SUN@103,339|LEAF@241,339",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-d47d49d7-b571-44a1-9ba5-8054f18efe5f.jpg",
      "factor": 1,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 519,
            "Y": 181
          },
          "Size": {
            "IsEmpty": false,
            "Width": 402,
            "Height": 437
          },
          "X": 519,
          "Y": 181,
          "Width": 402,
          "Height": 437,
          "Left": 519,
          "Top": 181,
          "Right": 921,
          "Bottom": 618,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "STAR",
        "Step": 2,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 120,
              "Y": 343
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 282,
              "Y": 343
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 119,
              "Y": 399
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 283,
              "Y": 399
            }
          }
        ],
        "Detail": "",
        "Key": "2:STAR:3:GEM@120,343|STAR@282,343|CROWN@119,399|LEAF@283,399",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-d47d49d7-b571-44a1-9ba5-8054f18efe5f.jpg",
      "factor": 1.25,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 521,
            "Y": 182
          },
          "Size": {
            "IsEmpty": false,
            "Width": 503,
            "Height": 547
          },
          "X": 521,
          "Y": 182,
          "Width": 503,
          "Height": 547,
          "Left": 521,
          "Top": 182,
          "Right": 1024,
          "Bottom": 729,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "STAR",
        "Step": 2,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 150,
              "Y": 428
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 352,
              "Y": 428
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 149,
              "Y": 498
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 353,
              "Y": 498
            }
          }
        ],
        "Detail": "",
        "Key": "2:STAR:3:GEM@150,428|STAR@352,428|CROWN@149,498|LEAF@353,498",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-d47d49d7-b571-44a1-9ba5-8054f18efe5f.jpg",
      "factor": 0.85,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 518,
            "Y": 181
          },
          "Size": {
            "IsEmpty": false,
            "Width": 340,
            "Height": 370
          },
          "X": 518,
          "Y": 181,
          "Width": 340,
          "Height": 370,
          "Left": 518,
          "Top": 181,
          "Right": 858,
          "Bottom": 551,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "STAR",
        "Step": 2,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 101,
              "Y": 291
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 239,
              "Y": 291
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 101,
              "Y": 339
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 240,
              "Y": 339
            }
          }
        ],
        "Detail": "",
        "Key": "2:STAR:3:GEM@101,291|STAR@239,291|CROWN@101,339|LEAF@240,339",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-a483ba0e-1185-49aa-8e84-e00df742a535.jpg",
      "factor": 1,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 518,
            "Y": 193
          },
          "Size": {
            "IsEmpty": false,
            "Width": 402,
            "Height": 437
          },
          "X": 518,
          "Y": 193,
          "Width": 402,
          "Height": 437,
          "Left": 518,
          "Top": 193,
          "Right": 920,
          "Bottom": 630,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "MOON",
        "Step": 3,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 120,
              "Y": 343
            }
          },
          {
            "Word": "MOON",
            "Center": {
              "IsEmpty": false,
              "X": 282,
              "Y": 343
            }
          },
          {
            "Word": "SHIELD",
            "Center": {
              "IsEmpty": false,
              "X": 121,
              "Y": 399
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 281,
              "Y": 399
            }
          }
        ],
        "Detail": "",
        "Key": "3:MOON:3:GEM@120,343|MOON@282,343|SHIELD@121,399|CROWN@281,399",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-a483ba0e-1185-49aa-8e84-e00df742a535.jpg",
      "factor": 1.25,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 521,
            "Y": 197
          },
          "Size": {
            "IsEmpty": false,
            "Width": 501,
            "Height": 545
          },
          "X": 521,
          "Y": 197,
          "Width": 501,
          "Height": 545,
          "Left": 521,
          "Top": 197,
          "Right": 1022,
          "Bottom": 742,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "MOON",
        "Step": 3,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 149,
              "Y": 428
            }
          },
          {
            "Word": "MOON",
            "Center": {
              "IsEmpty": false,
              "X": 352,
              "Y": 428
            }
          },
          {
            "Word": "SHIELD",
            "Center": {
              "IsEmpty": false,
              "X": 150,
              "Y": 498
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 351,
              "Y": 498
            }
          }
        ],
        "Detail": "",
        "Key": "3:MOON:3:GEM@149,428|MOON@352,428|SHIELD@150,498|CROWN@351,498",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-a483ba0e-1185-49aa-8e84-e00df742a535.jpg",
      "factor": 0.85,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 516,
            "Y": 191
          },
          "Size": {
            "IsEmpty": false,
            "Width": 342,
            "Height": 372
          },
          "X": 516,
          "Y": 191,
          "Width": 342,
          "Height": 372,
          "Left": 516,
          "Top": 191,
          "Right": 858,
          "Bottom": 563,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "MOON",
        "Step": 3,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 103,
              "Y": 291
            }
          },
          {
            "Word": "MOON",
            "Center": {
              "IsEmpty": false,
              "X": 240,
              "Y": 291
            }
          },
          {
            "Word": "SHIELD",
            "Center": {
              "IsEmpty": false,
              "X": 103,
              "Y": 339
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 239,
              "Y": 339
            }
          }
        ],
        "Detail": "",
        "Key": "3:MOON:3:GEM@103,291|MOON@240,291|SHIELD@103,339|CROWN@239,339",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-9e8d9c50-d452-425d-a43d-b8482c7d11fd.jpg",
      "factor": 1,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 513,
            "Y": 187
          },
          "Size": {
            "IsEmpty": false,
            "Width": 402,
            "Height": 437
          },
          "X": 513,
          "Y": 187,
          "Width": 402,
          "Height": 437,
          "Left": 513,
          "Top": 187,
          "Right": 915,
          "Bottom": 624,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 4,
        "Attempts": 3,
        "Options": [
          {
            "Word": "SWORD",
            "Center": {
              "IsEmpty": false,
              "X": 120,
              "Y": 343
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 282,
              "Y": 343
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 120,
              "Y": 399
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 283,
              "Y": 399
            }
          }
        ],
        "Detail": "",
        "Key": "4:SUN:3:SWORD@120,343|SUN@282,343|STAR@120,399|LEAF@283,399",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-9e8d9c50-d452-425d-a43d-b8482c7d11fd.jpg",
      "factor": 1.25,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 515,
            "Y": 189
          },
          "Size": {
            "IsEmpty": false,
            "Width": 500,
            "Height": 544
          },
          "X": 515,
          "Y": 189,
          "Width": 500,
          "Height": 544,
          "Left": 515,
          "Top": 189,
          "Right": 1015,
          "Bottom": 733,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 4,
        "Attempts": 3,
        "Options": [
          {
            "Word": "SWORD",
            "Center": {
              "IsEmpty": false,
              "X": 149,
              "Y": 428
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 351,
              "Y": 428
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 148,
              "Y": 499
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 352,
              "Y": 499
            }
          }
        ],
        "Detail": "",
        "Key": "4:SUN:3:SWORD@149,428|SUN@351,428|STAR@148,499|LEAF@352,499",
        "Valid": true
      }
    },
    {
      "file": "codex-clipboard-9e8d9c50-d452-425d-a43d-b8482c7d11fd.jpg",
      "factor": 0.85,
      "found": {
        "HeaderSeen": true,
        "Bounds": {
          "Location": {
            "IsEmpty": false,
            "X": 512,
            "Y": 186
          },
          "Size": {
            "IsEmpty": false,
            "Width": 342,
            "Height": 372
          },
          "X": 512,
          "Y": 186,
          "Width": 342,
          "Height": 372,
          "Left": 512,
          "Top": 186,
          "Right": 854,
          "Bottom": 558,
          "IsEmpty": false
        },
        "Detail": "Do\u011Frulama kutusu bulundu \u00B7 se\u00E7enekler okunuyor"
      },
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 4,
        "Attempts": 3,
        "Options": [
          {
            "Word": "SWORD",
            "Center": {
              "IsEmpty": false,
              "X": 102,
              "Y": 291
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 240,
              "Y": 291
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 102,
              "Y": 339
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 240,
              "Y": 339
            }
          }
        ],
        "Detail": "",
        "Key": "4:SUN:3:SWORD@102,291|SUN@240,291|STAR@102,339|LEAF@240,339",
        "Valid": true
      }
    }
  ]
}
````

### Ek: evidence/automatic-session.json

SHA-256: `f1da3651bac056476e6c2f1b4aa0901bb86122099f18479a6086b64083a44eb3`. Boyut: 255 bayt.

````json
{
  "pass": true,
  "manual_area_selected": false,
  "manually_notified_arrival": false,
  "idle_frames": 2,
  "frames": 13,
  "clicks": 4,
  "accepted": 4,
  "attempts": 3,
  "complete": true,
  "target_pid": 14872,
  "game_accessed": false
}
````

### Ek: evidence/build.json

SHA-256: `11da975e2ca3793041b10bf0afc0e019c4c388c31030177cc0d766d7948eeeb6`. Boyut: 467 bayt.

````json
{
  "version": "1.1.0",
  "utc": "2026-10-05T19:03:44.8715565+00:00",
  "bytes": 56699173,
  "sha256": "B3CDF76E3F7E8E318CAE1B5DE7082FA9F11C457A1913D780054FFC938413BDA7",
  "file_version": "1.1.0.0",
  "execution_level": "requireAdministrator",
  "ui_access": "false",
  "self_checks": 41,
  "automatic_checks": 38,
  "window_checks": 23,
  "automatic_arrival_session": true,
  "game_input_tested": false,
  "live_memory_transitions_verified": false
}
````

### Ek: evidence/image-readings.json

SHA-256: `01abadddc756cd03ddfc0010977df6abc88d3b719872377df8d4c9bf7f7ae682`. Boyut: 5734 bayt.

````json
{
  "language": "tr",
  "input_sent": false,
  "result": [
    {
      "file": "C:/Users/xaofx/AppData/Local/Temp/codex-clipboard-e7f7b9fb-73ef-4f85-a2ad-1a68ad3bbc54.jpg",
      "raw_text": "Macro protection puzzle Click the matching symbol Tarqet SUN Step 1 of 4 3 attempts - 90 seconds CROWN SUN STAR LEAF",
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 1,
        "Attempts": 3,
        "Options": [
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 127,
              "Y": 344
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 290,
              "Y": 344
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 128,
              "Y": 400
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 291,
              "Y": 400
            }
          }
        ],
        "Detail": "",
        "Key": "1:SUN:3:CROWN@127,344|STAR@290,344|SUN@128,400|LEAF@291,400",
        "Valid": true
      },
      "exact_match": {
        "Word": "SUN",
        "Center": {
          "IsEmpty": false,
          "X": 128,
          "Y": 400
        }
      }
    },
    {
      "file": "C:/Users/xaofx/AppData/Local/Temp/codex-clipboard-d47d49d7-b571-44a1-9ba5-8054f18efe5f.jpg",
      "raw_text": "Macro protection puzzle Click the matching symbol Tarqet STAR Step 2 of 4 3 attempts - 51 seconds GEM CROWN STAR LEAF",
      "reading": {
        "Kind": 2,
        "Target": "STAR",
        "Step": 2,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 129,
              "Y": 344
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 291,
              "Y": 344
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 128,
              "Y": 400
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 292,
              "Y": 400
            }
          }
        ],
        "Detail": "",
        "Key": "2:STAR:3:GEM@129,344|STAR@291,344|CROWN@128,400|LEAF@292,400",
        "Valid": true
      },
      "exact_match": {
        "Word": "STAR",
        "Center": {
          "IsEmpty": false,
          "X": 291,
          "Y": 344
        }
      }
    },
    {
      "file": "C:/Users/xaofx/AppData/Local/Temp/codex-clipboard-a483ba0e-1185-49aa-8e84-e00df742a535.jpg",
      "raw_text": "Macro protection puzzle Click the matching symbol Tarqet: MOON Step 3 of 4 3 attempts - 42 seconds GEM SHIELD MOON CROWN",
      "reading": {
        "Kind": 2,
        "Target": "MOON",
        "Step": 3,
        "Attempts": 3,
        "Options": [
          {
            "Word": "GEM",
            "Center": {
              "IsEmpty": false,
              "X": 128,
              "Y": 356
            }
          },
          {
            "Word": "MOON",
            "Center": {
              "IsEmpty": false,
              "X": 290,
              "Y": 356
            }
          },
          {
            "Word": "SHIELD",
            "Center": {
              "IsEmpty": false,
              "X": 129,
              "Y": 412
            }
          },
          {
            "Word": "CROWN",
            "Center": {
              "IsEmpty": false,
              "X": 289,
              "Y": 412
            }
          }
        ],
        "Detail": "",
        "Key": "3:MOON:3:GEM@128,356|MOON@290,356|SHIELD@129,412|CROWN@289,412",
        "Valid": true
      },
      "exact_match": {
        "Word": "MOON",
        "Center": {
          "IsEmpty": false,
          "X": 290,
          "Y": 356
        }
      }
    },
    {
      "file": "C:/Users/xaofx/AppData/Local/Temp/codex-clipboard-9e8d9c50-d452-425d-a43d-b8482c7d11fd.jpg",
      "raw_text": "Macro protection puzzle Click the matching symbol Tarqet: SUN Step 4 of 4 3 attempts - 37 seconds 4 SWORD STAR SUN LEAF",
      "reading": {
        "Kind": 2,
        "Target": "SUN",
        "Step": 4,
        "Attempts": 3,
        "Options": [
          {
            "Word": "SWORD",
            "Center": {
              "IsEmpty": false,
              "X": 123,
              "Y": 350
            }
          },
          {
            "Word": "SUN",
            "Center": {
              "IsEmpty": false,
              "X": 285,
              "Y": 350
            }
          },
          {
            "Word": "STAR",
            "Center": {
              "IsEmpty": false,
              "X": 123,
              "Y": 407
            }
          },
          {
            "Word": "LEAF",
            "Center": {
              "IsEmpty": false,
              "X": 286,
              "Y": 406
            }
          }
        ],
        "Detail": "",
        "Key": "4:SUN:3:SWORD@123,350|SUN@285,350|STAR@123,407|LEAF@286,406",
        "Valid": true
      },
      "exact_match": {
        "Word": "SUN",
        "Center": {
          "IsEmpty": false,
          "X": 285,
          "Y": 350
        }
      }
    }
  ]
}
````

### Ek: evidence/memory-probe.json

SHA-256: `aa4063fc0f711f1350aec9df682bd50bf3b339b49122fc8bf04b333a2c9479f7`. Boyut: 269 bayt.

````json
{
  "pid": 900,
  "Path": "C:\\Games\\4Unity\\TClient.exe",
  "access": "0x410",
  "detail": "Bellek: okuma eri\u015Fimi a\u00E7\u0131lamad\u0131 (Win32 5); g\u00F6r\u00FCnt\u00FC izleniyor",
  "state": null,
  "input_sent": false,
  "writes_attempted": false
}
````

### Ek: evidence/self-tests.json

SHA-256: `97575649e554c57e08b8cf29741aecaaba2060aaded72986dd14b26caee67d6a`. Boyut: 3692 bayt.

````json
{
  "pass": true,
  "checks": 41,
  "language": "tr",
  "live_game_accessed": false,
  "readings": [
    {
      "Kind": 2,
      "Target": "SUN",
      "Step": 1,
      "Attempts": 3,
      "Options": [
        {
          "Word": "CROWN",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 345
          }
        },
        {
          "Word": "STAR",
          "Center": {
            "IsEmpty": false,
            "X": 292,
            "Y": 345
          }
        },
        {
          "Word": "SUN",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 403
          }
        },
        {
          "Word": "LEAF",
          "Center": {
            "IsEmpty": false,
            "X": 293,
            "Y": 403
          }
        }
      ],
      "Detail": "",
      "Key": "1:SUN:3:CROWN@114,345|STAR@292,345|SUN@114,403|LEAF@293,403",
      "Valid": true
    },
    {
      "Kind": 2,
      "Target": "STAR",
      "Step": 2,
      "Attempts": 3,
      "Options": [
        {
          "Word": "GEM",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 345
          }
        },
        {
          "Word": "STAR",
          "Center": {
            "IsEmpty": false,
            "X": 292,
            "Y": 345
          }
        },
        {
          "Word": "CROWN",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 403
          }
        },
        {
          "Word": "LEAF",
          "Center": {
            "IsEmpty": false,
            "X": 293,
            "Y": 403
          }
        }
      ],
      "Detail": "",
      "Key": "2:STAR:3:GEM@114,345|STAR@292,345|CROWN@114,403|LEAF@293,403",
      "Valid": true
    },
    {
      "Kind": 2,
      "Target": "MOON",
      "Step": 3,
      "Attempts": 3,
      "Options": [
        {
          "Word": "GEM",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 345
          }
        },
        {
          "Word": "MOON",
          "Center": {
            "IsEmpty": false,
            "X": 292,
            "Y": 345
          }
        },
        {
          "Word": "SHIELD",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 403
          }
        },
        {
          "Word": "CROWN",
          "Center": {
            "IsEmpty": false,
            "X": 292,
            "Y": 403
          }
        }
      ],
      "Detail": "",
      "Key": "3:MOON:3:GEM@114,345|MOON@292,345|SHIELD@114,403|CROWN@292,403",
      "Valid": true
    },
    {
      "Kind": 2,
      "Target": "SUN",
      "Step": 4,
      "Attempts": 3,
      "Options": [
        {
          "Word": "SWORD",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 345
          }
        },
        {
          "Word": "SUN",
          "Center": {
            "IsEmpty": false,
            "X": 292,
            "Y": 345
          }
        },
        {
          "Word": "STAR",
          "Center": {
            "IsEmpty": false,
            "X": 114,
            "Y": 403
          }
        },
        {
          "Word": "LEAF",
          "Center": {
            "IsEmpty": false,
            "X": 293,
            "Y": 403
          }
        }
      ],
      "Detail": "",
      "Key": "4:SUN:3:SWORD@114,345|SUN@292,345|STAR@114,403|LEAF@293,403",
      "Valid": true
    }
  ]
}
````

### Ek: evidence/static-profile/refs.json

SHA-256: `b24b9380c20125a2392bf3da5553e37836d7581b29f69bc69dfa8c67f5e4c771`. Boyut: 42078 bayt.

````json
{
  "labels": {
    "0xdc9f50": "Macro protection puzzle",
    "0xdc9f68": "Click the matching symbol",
    "0xdc9f88": "Target:  %s",
    "0xdc9f98": "Step %u of %u",
    "0xdc9fa8": "%u attempts - %u seconds"
  },
  "refs": [
    {
      "label": "Macro protection puzzle",
      "label_rva": "0xdc9f50",
      "ref_rva": "0x384108",
      "function": [
        "0x3840a0",
        "0x384214"
      ],
      "instructions": [
        {
          "rva": "0x3840a0",
          "instruction": "mov qword ptr [rsp + 8], rbx"
        },
        {
          "rva": "0x3840a5",
          "instruction": "mov qword ptr [rsp + 0x10], rbp"
        },
        {
          "rva": "0x3840aa",
          "instruction": "mov qword ptr [rsp + 0x18], rsi"
        },
        {
          "rva": "0x3840af",
          "instruction": "push rdi"
        },
        {
          "rva": "0x3840b0",
          "instruction": "sub rsp, 0x20"
        },
        {
          "rva": "0x3840b4",
          "instruction": "mov esi, r9d"
        },
        {
          "rva": "0x3840b7",
          "instruction": "movzx ebp, r8b"
        },
        {
          "rva": "0x3840bb",
          "instruction": "mov ebx, edx"
        },
        {
          "rva": "0x3840bd",
          "instruction": "mov rdi, rcx"
        },
        {
          "rva": "0x3840c0",
          "instruction": "call 0x140383850"
        },
        {
          "rva": "0x3840c5",
          "instruction": "mov rax, qword ptr [rsp + 0x68]"
        },
        {
          "rva": "0x3840ca",
          "instruction": "mov r11, rdi"
        },
        {
          "rva": "0x3840cd",
          "instruction": "sub r11, rax"
        },
        {
          "rva": "0x3840d0",
          "instruction": "mov dword ptr [rdi + 0x398], ebx"
        },
        {
          "rva": "0x3840d6",
          "instruction": "mov r10d, 4"
        },
        {
          "rva": "0x3840dc",
          "instruction": "nop dword ptr [rax]"
        },
        {
          "rva": "0x3840e0",
          "instruction": "mov edx, dword ptr [rax]"
        },
        {
          "rva": "0x3840e2",
          "instruction": "mov dword ptr [r11 + rax + 0x388], edx"
        },
        {
          "rva": "0x3840ea",
          "instruction": "lea rax, [rax + 4]"
        },
        {
          "rva": "0x3840ee",
          "instruction": "sub r10, 1"
        },
        {
          "rva": "0x3840f2",
          "instruction": "jne 0x1403840e0"
        },
        {
          "rva": "0x3840f4",
          "instruction": "xor r8d, r8d"
        },
        {
          "rva": "0x3840f7",
          "instruction": "mov dl, 0xfa"
        },
        {
          "rva": "0x3840f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3840fc",
          "instruction": "call 0x140384220"
        },
        {
          "rva": "0x384101",
          "instruction": "mov rcx, qword ptr [rdi + 0x288]"
        },
        {
          "rva": "0x384108",
          "instruction": "lea rdx, [rip + 0xa45e41]"
        },
        {
          "rva": "0x38410f",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384113",
          "instruction": "mov r8d, 0x17"
        },
        {
          "rva": "0x384119",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38411e",
          "instruction": "mov rcx, qword ptr [rdi + 0x290]"
        },
        {
          "rva": "0x384125",
          "instruction": "lea rdx, [rip + 0xa45e3c]"
        },
        {
          "rva": "0x38412c",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384130",
          "instruction": "mov r8d, 0x19"
        },
        {
          "rva": "0x384136",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38413b",
          "instruction": "mov rcx, qword ptr [rdi + 0x298]"
        },
        {
          "rva": "0x384142",
          "instruction": "lea rdx, [rip + 0xa45e3f]"
        },
        {
          "rva": "0x384149",
          "instruction": "mov r8, qword ptr [rsp + 0x60]"
        },
        {
          "rva": "0x38414e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384152",
          "instruction": "mov r8, qword ptr [r8]"
        },
        {
          "rva": "0x384155",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38415a",
          "instruction": "movzx r8d, byte ptr [rsp + 0x50]"
        },
        {
          "rva": "0x384160",
          "instruction": "lea rdx, [rip + 0xa45e31]"
        },
        {
          "rva": "0x384167",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a0]"
        },
        {
          "rva": "0x38416e",
          "instruction": "inc r8d"
        },
        {
          "rva": "0x384171",
          "instruction": "movzx r9d, byte ptr [rsp + 0x58]"
        },
        {
          "rva": "0x384177",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x38417b",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x384180",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a8]"
        },
        {
          "rva": "0x384187",
          "instruction": "lea rdx, [rip + 0xa45e1a]"
        },
        {
          "rva": "0x38418e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384192",
          "instruction": "mov r8d, ebp"
        },
        {
          "rva": "0x384195",
          "instruction": "mov r9d, esi"
        },
        {
          "rva": "0x384198",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38419d",
          "instruction": "mov rcx, qword ptr [rdi + 0x320]"
        },
        {
          "rva": "0x3841a4",
          "instruction": "mov rbx, qword ptr [rsp + 0x70]"
        },
        {
          "rva": "0x3841a9",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ad",
          "instruction": "mov rdx, rbx"
        },
        {
          "rva": "0x3841b0",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841b5",
          "instruction": "mov rcx, qword ptr [rdi + 0x380]"
        },
        {
          "rva": "0x3841bc",
          "instruction": "lea rdx, [rbx + 8]"
        },
        {
          "rva": "0x3841c0",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841c4",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841c9",
          "instruction": "mov rcx, qword ptr [rdi + 0x310]"
        },
        {
          "rva": "0x3841d0",
          "instruction": "lea rdx, [rbx + 0x10]"
        },
        {
          "rva": "0x3841d4",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841d8",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841dd",
          "instruction": "mov rcx, qword ptr [rdi + 0x318]"
        },
        {
          "rva": "0x3841e4",
          "instruction": "lea rdx, [rbx + 0x18]"
        },
        {
          "rva": "0x3841e8",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ec",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841f1",
          "instruction": "mov rax, qword ptr [rdi]"
        },
        {
          "rva": "0x3841f4",
          "instruction": "mov edx, 1"
        },
        {
          "rva": "0x3841f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3841fc",
          "instruction": "mov rbx, qword ptr [rsp + 0x30]"
        },
        {
          "rva": "0x384201",
          "instruction": "mov rbp, qword ptr [rsp + 0x38]"
        },
        {
          "rva": "0x384206",
          "instruction": "mov rsi, qword ptr [rsp + 0x40]"
        },
        {
          "rva": "0x38420b",
          "instruction": "add rsp, 0x20"
        },
        {
          "rva": "0x38420f",
          "instruction": "pop rdi"
        },
        {
          "rva": "0x384210",
          "instruction": "jmp qword ptr [rax + 0x30]"
        }
      ]
    },
    {
      "label": "Click the matching symbol",
      "label_rva": "0xdc9f68",
      "ref_rva": "0x384125",
      "function": [
        "0x3840a0",
        "0x384214"
      ],
      "instructions": [
        {
          "rva": "0x3840a0",
          "instruction": "mov qword ptr [rsp + 8], rbx"
        },
        {
          "rva": "0x3840a5",
          "instruction": "mov qword ptr [rsp + 0x10], rbp"
        },
        {
          "rva": "0x3840aa",
          "instruction": "mov qword ptr [rsp + 0x18], rsi"
        },
        {
          "rva": "0x3840af",
          "instruction": "push rdi"
        },
        {
          "rva": "0x3840b0",
          "instruction": "sub rsp, 0x20"
        },
        {
          "rva": "0x3840b4",
          "instruction": "mov esi, r9d"
        },
        {
          "rva": "0x3840b7",
          "instruction": "movzx ebp, r8b"
        },
        {
          "rva": "0x3840bb",
          "instruction": "mov ebx, edx"
        },
        {
          "rva": "0x3840bd",
          "instruction": "mov rdi, rcx"
        },
        {
          "rva": "0x3840c0",
          "instruction": "call 0x140383850"
        },
        {
          "rva": "0x3840c5",
          "instruction": "mov rax, qword ptr [rsp + 0x68]"
        },
        {
          "rva": "0x3840ca",
          "instruction": "mov r11, rdi"
        },
        {
          "rva": "0x3840cd",
          "instruction": "sub r11, rax"
        },
        {
          "rva": "0x3840d0",
          "instruction": "mov dword ptr [rdi + 0x398], ebx"
        },
        {
          "rva": "0x3840d6",
          "instruction": "mov r10d, 4"
        },
        {
          "rva": "0x3840dc",
          "instruction": "nop dword ptr [rax]"
        },
        {
          "rva": "0x3840e0",
          "instruction": "mov edx, dword ptr [rax]"
        },
        {
          "rva": "0x3840e2",
          "instruction": "mov dword ptr [r11 + rax + 0x388], edx"
        },
        {
          "rva": "0x3840ea",
          "instruction": "lea rax, [rax + 4]"
        },
        {
          "rva": "0x3840ee",
          "instruction": "sub r10, 1"
        },
        {
          "rva": "0x3840f2",
          "instruction": "jne 0x1403840e0"
        },
        {
          "rva": "0x3840f4",
          "instruction": "xor r8d, r8d"
        },
        {
          "rva": "0x3840f7",
          "instruction": "mov dl, 0xfa"
        },
        {
          "rva": "0x3840f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3840fc",
          "instruction": "call 0x140384220"
        },
        {
          "rva": "0x384101",
          "instruction": "mov rcx, qword ptr [rdi + 0x288]"
        },
        {
          "rva": "0x384108",
          "instruction": "lea rdx, [rip + 0xa45e41]"
        },
        {
          "rva": "0x38410f",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384113",
          "instruction": "mov r8d, 0x17"
        },
        {
          "rva": "0x384119",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38411e",
          "instruction": "mov rcx, qword ptr [rdi + 0x290]"
        },
        {
          "rva": "0x384125",
          "instruction": "lea rdx, [rip + 0xa45e3c]"
        },
        {
          "rva": "0x38412c",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384130",
          "instruction": "mov r8d, 0x19"
        },
        {
          "rva": "0x384136",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38413b",
          "instruction": "mov rcx, qword ptr [rdi + 0x298]"
        },
        {
          "rva": "0x384142",
          "instruction": "lea rdx, [rip + 0xa45e3f]"
        },
        {
          "rva": "0x384149",
          "instruction": "mov r8, qword ptr [rsp + 0x60]"
        },
        {
          "rva": "0x38414e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384152",
          "instruction": "mov r8, qword ptr [r8]"
        },
        {
          "rva": "0x384155",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38415a",
          "instruction": "movzx r8d, byte ptr [rsp + 0x50]"
        },
        {
          "rva": "0x384160",
          "instruction": "lea rdx, [rip + 0xa45e31]"
        },
        {
          "rva": "0x384167",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a0]"
        },
        {
          "rva": "0x38416e",
          "instruction": "inc r8d"
        },
        {
          "rva": "0x384171",
          "instruction": "movzx r9d, byte ptr [rsp + 0x58]"
        },
        {
          "rva": "0x384177",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x38417b",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x384180",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a8]"
        },
        {
          "rva": "0x384187",
          "instruction": "lea rdx, [rip + 0xa45e1a]"
        },
        {
          "rva": "0x38418e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384192",
          "instruction": "mov r8d, ebp"
        },
        {
          "rva": "0x384195",
          "instruction": "mov r9d, esi"
        },
        {
          "rva": "0x384198",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38419d",
          "instruction": "mov rcx, qword ptr [rdi + 0x320]"
        },
        {
          "rva": "0x3841a4",
          "instruction": "mov rbx, qword ptr [rsp + 0x70]"
        },
        {
          "rva": "0x3841a9",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ad",
          "instruction": "mov rdx, rbx"
        },
        {
          "rva": "0x3841b0",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841b5",
          "instruction": "mov rcx, qword ptr [rdi + 0x380]"
        },
        {
          "rva": "0x3841bc",
          "instruction": "lea rdx, [rbx + 8]"
        },
        {
          "rva": "0x3841c0",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841c4",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841c9",
          "instruction": "mov rcx, qword ptr [rdi + 0x310]"
        },
        {
          "rva": "0x3841d0",
          "instruction": "lea rdx, [rbx + 0x10]"
        },
        {
          "rva": "0x3841d4",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841d8",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841dd",
          "instruction": "mov rcx, qword ptr [rdi + 0x318]"
        },
        {
          "rva": "0x3841e4",
          "instruction": "lea rdx, [rbx + 0x18]"
        },
        {
          "rva": "0x3841e8",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ec",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841f1",
          "instruction": "mov rax, qword ptr [rdi]"
        },
        {
          "rva": "0x3841f4",
          "instruction": "mov edx, 1"
        },
        {
          "rva": "0x3841f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3841fc",
          "instruction": "mov rbx, qword ptr [rsp + 0x30]"
        },
        {
          "rva": "0x384201",
          "instruction": "mov rbp, qword ptr [rsp + 0x38]"
        },
        {
          "rva": "0x384206",
          "instruction": "mov rsi, qword ptr [rsp + 0x40]"
        },
        {
          "rva": "0x38420b",
          "instruction": "add rsp, 0x20"
        },
        {
          "rva": "0x38420f",
          "instruction": "pop rdi"
        },
        {
          "rva": "0x384210",
          "instruction": "jmp qword ptr [rax + 0x30]"
        }
      ]
    },
    {
      "label": "Target:  %s",
      "label_rva": "0xdc9f88",
      "ref_rva": "0x384142",
      "function": [
        "0x3840a0",
        "0x384214"
      ],
      "instructions": [
        {
          "rva": "0x3840a0",
          "instruction": "mov qword ptr [rsp + 8], rbx"
        },
        {
          "rva": "0x3840a5",
          "instruction": "mov qword ptr [rsp + 0x10], rbp"
        },
        {
          "rva": "0x3840aa",
          "instruction": "mov qword ptr [rsp + 0x18], rsi"
        },
        {
          "rva": "0x3840af",
          "instruction": "push rdi"
        },
        {
          "rva": "0x3840b0",
          "instruction": "sub rsp, 0x20"
        },
        {
          "rva": "0x3840b4",
          "instruction": "mov esi, r9d"
        },
        {
          "rva": "0x3840b7",
          "instruction": "movzx ebp, r8b"
        },
        {
          "rva": "0x3840bb",
          "instruction": "mov ebx, edx"
        },
        {
          "rva": "0x3840bd",
          "instruction": "mov rdi, rcx"
        },
        {
          "rva": "0x3840c0",
          "instruction": "call 0x140383850"
        },
        {
          "rva": "0x3840c5",
          "instruction": "mov rax, qword ptr [rsp + 0x68]"
        },
        {
          "rva": "0x3840ca",
          "instruction": "mov r11, rdi"
        },
        {
          "rva": "0x3840cd",
          "instruction": "sub r11, rax"
        },
        {
          "rva": "0x3840d0",
          "instruction": "mov dword ptr [rdi + 0x398], ebx"
        },
        {
          "rva": "0x3840d6",
          "instruction": "mov r10d, 4"
        },
        {
          "rva": "0x3840dc",
          "instruction": "nop dword ptr [rax]"
        },
        {
          "rva": "0x3840e0",
          "instruction": "mov edx, dword ptr [rax]"
        },
        {
          "rva": "0x3840e2",
          "instruction": "mov dword ptr [r11 + rax + 0x388], edx"
        },
        {
          "rva": "0x3840ea",
          "instruction": "lea rax, [rax + 4]"
        },
        {
          "rva": "0x3840ee",
          "instruction": "sub r10, 1"
        },
        {
          "rva": "0x3840f2",
          "instruction": "jne 0x1403840e0"
        },
        {
          "rva": "0x3840f4",
          "instruction": "xor r8d, r8d"
        },
        {
          "rva": "0x3840f7",
          "instruction": "mov dl, 0xfa"
        },
        {
          "rva": "0x3840f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3840fc",
          "instruction": "call 0x140384220"
        },
        {
          "rva": "0x384101",
          "instruction": "mov rcx, qword ptr [rdi + 0x288]"
        },
        {
          "rva": "0x384108",
          "instruction": "lea rdx, [rip + 0xa45e41]"
        },
        {
          "rva": "0x38410f",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384113",
          "instruction": "mov r8d, 0x17"
        },
        {
          "rva": "0x384119",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38411e",
          "instruction": "mov rcx, qword ptr [rdi + 0x290]"
        },
        {
          "rva": "0x384125",
          "instruction": "lea rdx, [rip + 0xa45e3c]"
        },
        {
          "rva": "0x38412c",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384130",
          "instruction": "mov r8d, 0x19"
        },
        {
          "rva": "0x384136",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38413b",
          "instruction": "mov rcx, qword ptr [rdi + 0x298]"
        },
        {
          "rva": "0x384142",
          "instruction": "lea rdx, [rip + 0xa45e3f]"
        },
        {
          "rva": "0x384149",
          "instruction": "mov r8, qword ptr [rsp + 0x60]"
        },
        {
          "rva": "0x38414e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384152",
          "instruction": "mov r8, qword ptr [r8]"
        },
        {
          "rva": "0x384155",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38415a",
          "instruction": "movzx r8d, byte ptr [rsp + 0x50]"
        },
        {
          "rva": "0x384160",
          "instruction": "lea rdx, [rip + 0xa45e31]"
        },
        {
          "rva": "0x384167",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a0]"
        },
        {
          "rva": "0x38416e",
          "instruction": "inc r8d"
        },
        {
          "rva": "0x384171",
          "instruction": "movzx r9d, byte ptr [rsp + 0x58]"
        },
        {
          "rva": "0x384177",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x38417b",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x384180",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a8]"
        },
        {
          "rva": "0x384187",
          "instruction": "lea rdx, [rip + 0xa45e1a]"
        },
        {
          "rva": "0x38418e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384192",
          "instruction": "mov r8d, ebp"
        },
        {
          "rva": "0x384195",
          "instruction": "mov r9d, esi"
        },
        {
          "rva": "0x384198",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38419d",
          "instruction": "mov rcx, qword ptr [rdi + 0x320]"
        },
        {
          "rva": "0x3841a4",
          "instruction": "mov rbx, qword ptr [rsp + 0x70]"
        },
        {
          "rva": "0x3841a9",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ad",
          "instruction": "mov rdx, rbx"
        },
        {
          "rva": "0x3841b0",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841b5",
          "instruction": "mov rcx, qword ptr [rdi + 0x380]"
        },
        {
          "rva": "0x3841bc",
          "instruction": "lea rdx, [rbx + 8]"
        },
        {
          "rva": "0x3841c0",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841c4",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841c9",
          "instruction": "mov rcx, qword ptr [rdi + 0x310]"
        },
        {
          "rva": "0x3841d0",
          "instruction": "lea rdx, [rbx + 0x10]"
        },
        {
          "rva": "0x3841d4",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841d8",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841dd",
          "instruction": "mov rcx, qword ptr [rdi + 0x318]"
        },
        {
          "rva": "0x3841e4",
          "instruction": "lea rdx, [rbx + 0x18]"
        },
        {
          "rva": "0x3841e8",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ec",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841f1",
          "instruction": "mov rax, qword ptr [rdi]"
        },
        {
          "rva": "0x3841f4",
          "instruction": "mov edx, 1"
        },
        {
          "rva": "0x3841f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3841fc",
          "instruction": "mov rbx, qword ptr [rsp + 0x30]"
        },
        {
          "rva": "0x384201",
          "instruction": "mov rbp, qword ptr [rsp + 0x38]"
        },
        {
          "rva": "0x384206",
          "instruction": "mov rsi, qword ptr [rsp + 0x40]"
        },
        {
          "rva": "0x38420b",
          "instruction": "add rsp, 0x20"
        },
        {
          "rva": "0x38420f",
          "instruction": "pop rdi"
        },
        {
          "rva": "0x384210",
          "instruction": "jmp qword ptr [rax + 0x30]"
        }
      ]
    },
    {
      "label": "Step %u of %u",
      "label_rva": "0xdc9f98",
      "ref_rva": "0x384160",
      "function": [
        "0x3840a0",
        "0x384214"
      ],
      "instructions": [
        {
          "rva": "0x3840a0",
          "instruction": "mov qword ptr [rsp + 8], rbx"
        },
        {
          "rva": "0x3840a5",
          "instruction": "mov qword ptr [rsp + 0x10], rbp"
        },
        {
          "rva": "0x3840aa",
          "instruction": "mov qword ptr [rsp + 0x18], rsi"
        },
        {
          "rva": "0x3840af",
          "instruction": "push rdi"
        },
        {
          "rva": "0x3840b0",
          "instruction": "sub rsp, 0x20"
        },
        {
          "rva": "0x3840b4",
          "instruction": "mov esi, r9d"
        },
        {
          "rva": "0x3840b7",
          "instruction": "movzx ebp, r8b"
        },
        {
          "rva": "0x3840bb",
          "instruction": "mov ebx, edx"
        },
        {
          "rva": "0x3840bd",
          "instruction": "mov rdi, rcx"
        },
        {
          "rva": "0x3840c0",
          "instruction": "call 0x140383850"
        },
        {
          "rva": "0x3840c5",
          "instruction": "mov rax, qword ptr [rsp + 0x68]"
        },
        {
          "rva": "0x3840ca",
          "instruction": "mov r11, rdi"
        },
        {
          "rva": "0x3840cd",
          "instruction": "sub r11, rax"
        },
        {
          "rva": "0x3840d0",
          "instruction": "mov dword ptr [rdi + 0x398], ebx"
        },
        {
          "rva": "0x3840d6",
          "instruction": "mov r10d, 4"
        },
        {
          "rva": "0x3840dc",
          "instruction": "nop dword ptr [rax]"
        },
        {
          "rva": "0x3840e0",
          "instruction": "mov edx, dword ptr [rax]"
        },
        {
          "rva": "0x3840e2",
          "instruction": "mov dword ptr [r11 + rax + 0x388], edx"
        },
        {
          "rva": "0x3840ea",
          "instruction": "lea rax, [rax + 4]"
        },
        {
          "rva": "0x3840ee",
          "instruction": "sub r10, 1"
        },
        {
          "rva": "0x3840f2",
          "instruction": "jne 0x1403840e0"
        },
        {
          "rva": "0x3840f4",
          "instruction": "xor r8d, r8d"
        },
        {
          "rva": "0x3840f7",
          "instruction": "mov dl, 0xfa"
        },
        {
          "rva": "0x3840f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3840fc",
          "instruction": "call 0x140384220"
        },
        {
          "rva": "0x384101",
          "instruction": "mov rcx, qword ptr [rdi + 0x288]"
        },
        {
          "rva": "0x384108",
          "instruction": "lea rdx, [rip + 0xa45e41]"
        },
        {
          "rva": "0x38410f",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384113",
          "instruction": "mov r8d, 0x17"
        },
        {
          "rva": "0x384119",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38411e",
          "instruction": "mov rcx, qword ptr [rdi + 0x290]"
        },
        {
          "rva": "0x384125",
          "instruction": "lea rdx, [rip + 0xa45e3c]"
        },
        {
          "rva": "0x38412c",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384130",
          "instruction": "mov r8d, 0x19"
        },
        {
          "rva": "0x384136",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38413b",
          "instruction": "mov rcx, qword ptr [rdi + 0x298]"
        },
        {
          "rva": "0x384142",
          "instruction": "lea rdx, [rip + 0xa45e3f]"
        },
        {
          "rva": "0x384149",
          "instruction": "mov r8, qword ptr [rsp + 0x60]"
        },
        {
          "rva": "0x38414e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384152",
          "instruction": "mov r8, qword ptr [r8]"
        },
        {
          "rva": "0x384155",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38415a",
          "instruction": "movzx r8d, byte ptr [rsp + 0x50]"
        },
        {
          "rva": "0x384160",
          "instruction": "lea rdx, [rip + 0xa45e31]"
        },
        {
          "rva": "0x384167",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a0]"
        },
        {
          "rva": "0x38416e",
          "instruction": "inc r8d"
        },
        {
          "rva": "0x384171",
          "instruction": "movzx r9d, byte ptr [rsp + 0x58]"
        },
        {
          "rva": "0x384177",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x38417b",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x384180",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a8]"
        },
        {
          "rva": "0x384187",
          "instruction": "lea rdx, [rip + 0xa45e1a]"
        },
        {
          "rva": "0x38418e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384192",
          "instruction": "mov r8d, ebp"
        },
        {
          "rva": "0x384195",
          "instruction": "mov r9d, esi"
        },
        {
          "rva": "0x384198",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38419d",
          "instruction": "mov rcx, qword ptr [rdi + 0x320]"
        },
        {
          "rva": "0x3841a4",
          "instruction": "mov rbx, qword ptr [rsp + 0x70]"
        },
        {
          "rva": "0x3841a9",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ad",
          "instruction": "mov rdx, rbx"
        },
        {
          "rva": "0x3841b0",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841b5",
          "instruction": "mov rcx, qword ptr [rdi + 0x380]"
        },
        {
          "rva": "0x3841bc",
          "instruction": "lea rdx, [rbx + 8]"
        },
        {
          "rva": "0x3841c0",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841c4",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841c9",
          "instruction": "mov rcx, qword ptr [rdi + 0x310]"
        },
        {
          "rva": "0x3841d0",
          "instruction": "lea rdx, [rbx + 0x10]"
        },
        {
          "rva": "0x3841d4",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841d8",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841dd",
          "instruction": "mov rcx, qword ptr [rdi + 0x318]"
        },
        {
          "rva": "0x3841e4",
          "instruction": "lea rdx, [rbx + 0x18]"
        },
        {
          "rva": "0x3841e8",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ec",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841f1",
          "instruction": "mov rax, qword ptr [rdi]"
        },
        {
          "rva": "0x3841f4",
          "instruction": "mov edx, 1"
        },
        {
          "rva": "0x3841f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3841fc",
          "instruction": "mov rbx, qword ptr [rsp + 0x30]"
        },
        {
          "rva": "0x384201",
          "instruction": "mov rbp, qword ptr [rsp + 0x38]"
        },
        {
          "rva": "0x384206",
          "instruction": "mov rsi, qword ptr [rsp + 0x40]"
        },
        {
          "rva": "0x38420b",
          "instruction": "add rsp, 0x20"
        },
        {
          "rva": "0x38420f",
          "instruction": "pop rdi"
        },
        {
          "rva": "0x384210",
          "instruction": "jmp qword ptr [rax + 0x30]"
        }
      ]
    },
    {
      "label": "%u attempts - %u seconds",
      "label_rva": "0xdc9fa8",
      "ref_rva": "0x384187",
      "function": [
        "0x3840a0",
        "0x384214"
      ],
      "instructions": [
        {
          "rva": "0x3840a0",
          "instruction": "mov qword ptr [rsp + 8], rbx"
        },
        {
          "rva": "0x3840a5",
          "instruction": "mov qword ptr [rsp + 0x10], rbp"
        },
        {
          "rva": "0x3840aa",
          "instruction": "mov qword ptr [rsp + 0x18], rsi"
        },
        {
          "rva": "0x3840af",
          "instruction": "push rdi"
        },
        {
          "rva": "0x3840b0",
          "instruction": "sub rsp, 0x20"
        },
        {
          "rva": "0x3840b4",
          "instruction": "mov esi, r9d"
        },
        {
          "rva": "0x3840b7",
          "instruction": "movzx ebp, r8b"
        },
        {
          "rva": "0x3840bb",
          "instruction": "mov ebx, edx"
        },
        {
          "rva": "0x3840bd",
          "instruction": "mov rdi, rcx"
        },
        {
          "rva": "0x3840c0",
          "instruction": "call 0x140383850"
        },
        {
          "rva": "0x3840c5",
          "instruction": "mov rax, qword ptr [rsp + 0x68]"
        },
        {
          "rva": "0x3840ca",
          "instruction": "mov r11, rdi"
        },
        {
          "rva": "0x3840cd",
          "instruction": "sub r11, rax"
        },
        {
          "rva": "0x3840d0",
          "instruction": "mov dword ptr [rdi + 0x398], ebx"
        },
        {
          "rva": "0x3840d6",
          "instruction": "mov r10d, 4"
        },
        {
          "rva": "0x3840dc",
          "instruction": "nop dword ptr [rax]"
        },
        {
          "rva": "0x3840e0",
          "instruction": "mov edx, dword ptr [rax]"
        },
        {
          "rva": "0x3840e2",
          "instruction": "mov dword ptr [r11 + rax + 0x388], edx"
        },
        {
          "rva": "0x3840ea",
          "instruction": "lea rax, [rax + 4]"
        },
        {
          "rva": "0x3840ee",
          "instruction": "sub r10, 1"
        },
        {
          "rva": "0x3840f2",
          "instruction": "jne 0x1403840e0"
        },
        {
          "rva": "0x3840f4",
          "instruction": "xor r8d, r8d"
        },
        {
          "rva": "0x3840f7",
          "instruction": "mov dl, 0xfa"
        },
        {
          "rva": "0x3840f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3840fc",
          "instruction": "call 0x140384220"
        },
        {
          "rva": "0x384101",
          "instruction": "mov rcx, qword ptr [rdi + 0x288]"
        },
        {
          "rva": "0x384108",
          "instruction": "lea rdx, [rip + 0xa45e41]"
        },
        {
          "rva": "0x38410f",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384113",
          "instruction": "mov r8d, 0x17"
        },
        {
          "rva": "0x384119",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38411e",
          "instruction": "mov rcx, qword ptr [rdi + 0x290]"
        },
        {
          "rva": "0x384125",
          "instruction": "lea rdx, [rip + 0xa45e3c]"
        },
        {
          "rva": "0x38412c",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384130",
          "instruction": "mov r8d, 0x19"
        },
        {
          "rva": "0x384136",
          "instruction": "call 0x14001fee0"
        },
        {
          "rva": "0x38413b",
          "instruction": "mov rcx, qword ptr [rdi + 0x298]"
        },
        {
          "rva": "0x384142",
          "instruction": "lea rdx, [rip + 0xa45e3f]"
        },
        {
          "rva": "0x384149",
          "instruction": "mov r8, qword ptr [rsp + 0x60]"
        },
        {
          "rva": "0x38414e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384152",
          "instruction": "mov r8, qword ptr [r8]"
        },
        {
          "rva": "0x384155",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38415a",
          "instruction": "movzx r8d, byte ptr [rsp + 0x50]"
        },
        {
          "rva": "0x384160",
          "instruction": "lea rdx, [rip + 0xa45e31]"
        },
        {
          "rva": "0x384167",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a0]"
        },
        {
          "rva": "0x38416e",
          "instruction": "inc r8d"
        },
        {
          "rva": "0x384171",
          "instruction": "movzx r9d, byte ptr [rsp + 0x58]"
        },
        {
          "rva": "0x384177",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x38417b",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x384180",
          "instruction": "mov rcx, qword ptr [rdi + 0x2a8]"
        },
        {
          "rva": "0x384187",
          "instruction": "lea rdx, [rip + 0xa45e1a]"
        },
        {
          "rva": "0x38418e",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x384192",
          "instruction": "mov r8d, ebp"
        },
        {
          "rva": "0x384195",
          "instruction": "mov r9d, esi"
        },
        {
          "rva": "0x384198",
          "instruction": "call 0x14001ecd0"
        },
        {
          "rva": "0x38419d",
          "instruction": "mov rcx, qword ptr [rdi + 0x320]"
        },
        {
          "rva": "0x3841a4",
          "instruction": "mov rbx, qword ptr [rsp + 0x70]"
        },
        {
          "rva": "0x3841a9",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ad",
          "instruction": "mov rdx, rbx"
        },
        {
          "rva": "0x3841b0",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841b5",
          "instruction": "mov rcx, qword ptr [rdi + 0x380]"
        },
        {
          "rva": "0x3841bc",
          "instruction": "lea rdx, [rbx + 8]"
        },
        {
          "rva": "0x3841c0",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841c4",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841c9",
          "instruction": "mov rcx, qword ptr [rdi + 0x310]"
        },
        {
          "rva": "0x3841d0",
          "instruction": "lea rdx, [rbx + 0x10]"
        },
        {
          "rva": "0x3841d4",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841d8",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841dd",
          "instruction": "mov rcx, qword ptr [rdi + 0x318]"
        },
        {
          "rva": "0x3841e4",
          "instruction": "lea rdx, [rbx + 0x18]"
        },
        {
          "rva": "0x3841e8",
          "instruction": "add rcx, 0x10"
        },
        {
          "rva": "0x3841ec",
          "instruction": "call 0x14001d590"
        },
        {
          "rva": "0x3841f1",
          "instruction": "mov rax, qword ptr [rdi]"
        },
        {
          "rva": "0x3841f4",
          "instruction": "mov edx, 1"
        },
        {
          "rva": "0x3841f9",
          "instruction": "mov rcx, rdi"
        },
        {
          "rva": "0x3841fc",
          "instruction": "mov rbx, qword ptr [rsp + 0x30]"
        },
        {
          "rva": "0x384201",
          "instruction": "mov rbp, qword ptr [rsp + 0x38]"
        },
        {
          "rva": "0x384206",
          "instruction": "mov rsi, qword ptr [rsp + 0x40]"
        },
        {
          "rva": "0x38420b",
          "instruction": "add rsp, 0x20"
        },
        {
          "rva": "0x38420f",
          "instruction": "pop rdi"
        },
        {
          "rva": "0x384210",
          "instruction": "jmp qword ptr [rax + 0x30]"
        }
      ]
    }
  ]
}
````

### Ek: evidence/static-profile/strings.json

SHA-256: `dd624187a7f39df961bb542a4e027b4c2fb8d00fbd90238442442754f13cb040`. Boyut: 23882 bayt.

````json
[
  {
    "file": "4Unity_Launcher.exe",
    "bytes": 8259584,
    "sha256": "0c9d9dab33d6658b969bc98f61f4ce95113039255eb39c9e3edf133e6bf03217",
    "image_base": "0x400000",
    "rtti": [],
    "hits": [
      {
        "term": "protect",
        "offset": "0x1e98cb",
        "encoding": "ascii",
        "context": "\u0000r\u0002GetFileTime\u0000K\u0005SetErrorMode\u0000\u0000\t\u0006VirtualProtect\u0000\u0000\ufffd\u0002GetOEMCP\u0000\u0000\ufffd\u0001GetCPInfo\u0000S\u0003GetWindowsDirectoryA\u0000\u00006\u0003GetTickCount64\u0000\u0000\ufffd\u0005VerSetConditionMask\u0000\u0001\u0006VerifyVersionInfoA\u0000\u0000\ufffd",
        "rva": "0x1ea8cb"
      }
    ]
  },
  {
    "file": "d3dx11_43.dll",
    "bytes": 276832,
    "sha256": "981e42629df751217406e7150477cddc853b79abd6a8568a1566298ed8f7bd59",
    "image_base": "0x180000000",
    "rtti": [],
    "hits": [
      {
        "term": "protect",
        "offset": "0x3a95b",
        "encoding": "ascii",
        "context": "XZ\u0000\ufffd\u0000DisableThreadLibraryCalls\u0000\ufffd\u0004VirtualProtect\u0000\u0000\ufffd\u0004Sleep\u0000\u001a\u0002GetModuleHandleA\u0000\u0000>\u0003LoadLibraryA\u0000\u0000K\u0002GetProcAddress\u0000\u0000i\u0001FreeLibrary\u0000R\u0000CloseHandle\u0000\ufffd\u0003ReadFile\u0000\u0000\ufffd\u0001GetFile",
        "rva": "0x3b55b"
      }
    ]
  },
  {
    "file": "D3DX9_43.dll",
    "bytes": 2401112,
    "sha256": "84b900dbd7fa978d6e0caee26fc54f2f61d92c9c75d10b35f00e3e82cd1d67b4",
    "image_base": "0x180000000",
    "rtti": [],
    "hits": [
      {
        "term": "protect",
        "offset": "0x229e79",
        "encoding": "ascii",
        "context": "ll\u0000\ufffd\u0000DisableThreadLibraryCalls\u0000\ufffd\u0004VirtualProtect\u0000\u0000\ufffd\u0004Sleep\u0000\ufffd\u0002GetVersionExA\u0000>\u0003LoadLibraryA\u0000\u0000K\u0002GetProcAddress\u0000\u0000i\u0001FreeLibrary\u0000\u001a\u0002GetModuleHandleA\u0000\u0000\ufffd\u0003OutputDebugString",
        "rva": "0x22aa79"
      }
    ]
  },
  {
    "file": "TClient.exe",
    "bytes": 16549888,
    "sha256": "4dc9c526a895a10113c4cf23f2d199bff283d2c7ce75f949dc49cef15d27d622",
    "image_base": "0x140000000",
    "rtti": [
      ".?AVCTMacroBuilderDlg@@"
    ],
    "hits": [
      {
        "term": "puzzle",
        "offset": "0xdc8b61",
        "encoding": "ascii",
        "context": " can try again.\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Macro protection puzzle\u0000Click the matching symbol\u0000\u0000\u0000\u0000\u0000\u0000\u0000Target:  %s\u0000\u0000\u0000\u0000\u0000Step %u of %u\u0000\u0000\u0000%u attempts - %u seconds\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000PA\ufffd@\u0001\u0000\u0000\u0000\ufffd\ufffd\u0001@\u0001\u0000\u0000\u0000\u0010",
        "rva": "0xdc9f61"
      },
      {
        "term": "macro protection",
        "offset": "0xdc8b50",
        "encoding": "ascii",
        "context": "nute(s) until you can try again.\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Macro protection puzzle\u0000Click the matching symbol\u0000\u0000\u0000\u0000\u0000\u0000\u0000Target:  %s\u0000\u0000\u0000\u0000\u0000Step %u of %u\u0000\u0000\u0000%u attempts - %u seconds\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000",
        "rva": "0xdc9f50"
      },
      {
        "term": "matching symbol",
        "offset": "0xdc8b72",
        "encoding": "ascii",
        "context": "\u0000\u0000\u0000\u0000\u0000\u0000Macro protection puzzle\u0000Click the matching symbol\u0000\u0000\u0000\u0000\u0000\u0000\u0000Target:  %s\u0000\u0000\u0000\u0000\u0000Step %u of %u\u0000\u0000\u0000%u attempts - %u seconds\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000PA\ufffd@\u0001\u0000\u0000\u0000\ufffd\ufffd\u0001@\u0001\u0000\u0000\u0000\u0010P8@\u0001\u0000\u0000\u0000\ufffd\ufffd\ufffd@\u0001\u0000\u0000\u0000 6",
        "rva": "0xdc9f72"
      },
      {
        "term": "attempts",
        "offset": "0xdc883b",
        "encoding": "ascii",
        "context": " be locked for 24 hours after 10 failed attempts.\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Entered Securecode is invalid. Please check again and re-enter your Securecode.\u0000If you forgot your ",
        "rva": "0xdc9c3b"
      },
      {
        "term": "attempts",
        "offset": "0xdc8bab",
        "encoding": "ascii",
        "context": "\u0000\u0000\u0000\u0000\u0000Target:  %s\u0000\u0000\u0000\u0000\u0000Step %u of %u\u0000\u0000\u0000%u attempts - %u seconds\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000PA\ufffd@\u0001\u0000\u0000\u0000\ufffd\ufffd\u0001@\u0001\u0000\u0000\u0000\u0010P8@\u0001\u0000\u0000\u0000\ufffd\ufffd\ufffd@\u0001\u0000\u0000\u0000 6\ufffd@\u0001\u0000\u0000\u0000\u0010\ufffd\ufffd@\u0001\u0000\u0000\u0000p,\ufffd@\u0001\u0000\u0000\u0000@R8@\u0001\u0000\u0000\u0000\ufffd/\ufffd@\u0001\u0000\u0000\u0000\ufffd/\ufffd@\u0001\u0000\u0000\u0000\ufffdw\ufffd@\u0001\u0000\u0000\u00000t\ufffd",
        "rva": "0xdc9fab"
      },
      {
        "term": "protect",
        "offset": "0xd9cb2a",
        "encoding": "ascii",
        "context": "\u0000\u0000\u0000\u0000\u0000\u0000Failed to create duplicate-window protection mutex.\u0000\u0000\u0000\u0000\u0000InitInstance failure 1\u0000\u00004Unity is already running.\u0000\u0000\u0000\u0000\u0000\u0000InitInstance failure 2\u0000\u0000opening TCD archiv",
        "rva": "0xd9df2a"
      },
      {
        "term": "protect",
        "offset": "0xda1353",
        "encoding": "ascii",
        "context": "BJ_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_DELSELFOBJ_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDLIST_ACK\u0000\u0000\u0000\u0000CS_PROTECTEDADD_REQ\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDADD_ACK\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDERASE_REQ\u0000\u0000\u0000CS_PROTECTEDERASE_ACK\u0000\u0000\u0000CS_",
        "rva": "0xda2753"
      },
      {
        "term": "protect",
        "offset": "0xda136b",
        "encoding": "ascii",
        "context": "BJ_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDLIST_ACK\u0000\u0000\u0000\u0000CS_PROTECTEDADD_REQ\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDADD_ACK\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDERASE_REQ\u0000\u0000\u0000CS_PROTECTEDERASE_ACK\u0000\u0000\u0000CS_REVIVALASK_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_",
        "rva": "0xda276b"
      },
      {
        "term": "protect",
        "offset": "0xda1383",
        "encoding": "ascii",
        "context": "DLIST_ACK\u0000\u0000\u0000\u0000CS_PROTECTEDADD_REQ\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDADD_ACK\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDERASE_REQ\u0000\u0000\u0000CS_PROTECTEDERASE_ACK\u0000\u0000\u0000CS_REVIVALASK_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_REVIVALASK_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_",
        "rva": "0xda2783"
      },
      {
        "term": "protect",
        "offset": "0xda139b",
        "encoding": "ascii",
        "context": "DADD_REQ\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDADD_ACK\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDERASE_REQ\u0000\u0000\u0000CS_PROTECTEDERASE_ACK\u0000\u0000\u0000CS_REVIVALASK_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_REVIVALASK_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_CORPSHP_REQ\u0000\u0000CS_CORPSHP_",
        "rva": "0xda279b"
      },
      {
        "term": "protect",
        "offset": "0xda13b3",
        "encoding": "ascii",
        "context": "DADD_ACK\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDERASE_REQ\u0000\u0000\u0000CS_PROTECTEDERASE_ACK\u0000\u0000\u0000CS_REVIVALASK_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_REVIVALASK_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_CORPSHP_REQ\u0000\u0000CS_CORPSHP_ACK\u0000\u0000CS_PARTYMOVE_REQ\u0000\u0000\u0000",
        "rva": "0xda27b3"
      },
      {
        "term": "protect",
        "offset": "0xda2810",
        "encoding": "ascii",
        "context": "_REQ\u0000\u0000\u0000\u0000CS_RELAYCHARDATA_ACK\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDLIST_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDADD_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDDEL_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_CHECKRELAY_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAY",
        "rva": "0xda3c10"
      },
      {
        "term": "protect",
        "offset": "0xda2830",
        "encoding": "ascii",
        "context": "CS_RELAYPROTECTEDLIST_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDADD_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDDEL_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_CHECKRELAY_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDOPTION_REQ\u0000\u0000\u0000\u0000\u0000CS_COMME",
        "rva": "0xda3c30"
      },
      {
        "term": "protect",
        "offset": "0xda2850",
        "encoding": "ascii",
        "context": "CS_RELAYPROTECTEDADD_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDDEL_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_CHECKRELAY_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDOPTION_REQ\u0000\u0000\u0000\u0000\u0000CS_COMMENT_REQ\u0000\u0000CS_COMMENT_ACK\u0000\u0000CS_PVPPO",
        "rva": "0xda3c50"
      },
      {
        "term": "protect",
        "offset": "0xda2888",
        "encoding": "ascii",
        "context": "\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_CHECKRELAY_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_RELAYPROTECTEDOPTION_REQ\u0000\u0000\u0000\u0000\u0000CS_COMMENT_REQ\u0000\u0000CS_COMMENT_ACK\u0000\u0000CS_PVPPOINT_ACK\u0000CS_GUILDPOINTLOG_REQ\u0000\u0000\u0000\u0000CS_GUILDPOINTLOG_ACK\u0000\u0000\u0000\u0000",
        "rva": "0xda3c88"
      },
      {
        "term": "protect",
        "offset": "0xda2a2b",
        "encoding": "ascii",
        "context": "LEAVECASTLE_ACK\u0000\u0000\u0000\u0000\u0000\u0000CS_WARP_ACK\u0000\u0000\u0000\u0000\u0000CS_PROTECTEDOPTION_REQ\u0000\u0000CS_FRIENDLIST_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_ITEMCHANGE_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_ITEMCHANGE_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_COUNTDOWN_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_",
        "rva": "0xda3e2b"
      },
      {
        "term": "protect",
        "offset": "0xda4a88",
        "encoding": "ascii",
        "context": "ACK\u0000\u0000\u0000\u0000\u0000CS_NOTIFYTZORVAS_ACK\u0000\u0000\u0000\u0000CS_MACROPROTECTION_VERIFY_REQ\u0000\u0000\u0000CS_MACROPROTECTION_ACK\u0000\u0000CS_CANCELCOLLOSEUMQUEUE_ACK\u0000\u0000\u0000\u0000\u0000CS_COLLOSEUMMSG_ACK\u0000\u0000\u0000\u0000\u0000CS_REGISTERCOLLO",
        "rva": "0xda5e88"
      },
      {
        "term": "protect",
        "offset": "0xda4aa8",
        "encoding": "ascii",
        "context": "CS_MACROPROTECTION_VERIFY_REQ\u0000\u0000\u0000CS_MACROPROTECTION_ACK\u0000\u0000CS_CANCELCOLLOSEUMQUEUE_ACK\u0000\u0000\u0000\u0000\u0000CS_COLLOSEUMMSG_ACK\u0000\u0000\u0000\u0000\u0000CS_REGISTERCOLLOSEUM_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_REGISTERCOLLO",
        "rva": "0xda5ea8"
      },
      {
        "term": "protect",
        "offset": "0xda577c",
        "encoding": "ascii",
        "context": "D_REQ\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_HACKSHIELD_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_NPROTECT_REQ\u0000CS_NPROTECT_ACK\u0000CS_WINLDIC_REQ\u0000\u0000CS_WHIP_ACK\u0000\u0000\u0000\u0000\u0000CS_UPLOAD_GUILDMARK\u0000\u0000\u0000\u0000\u0000CS_REQ_GUILDMARK_IMAGE\u0000\u0000CS_REQ_GUILD",
        "rva": "0xda6b7c"
      },
      {
        "term": "protect",
        "offset": "0xda578c",
        "encoding": "ascii",
        "context": "ACKSHIELD_ACK\u0000\u0000\u0000\u0000\u0000\u0000\u0000CS_NPROTECT_REQ\u0000CS_NPROTECT_ACK\u0000CS_WINLDIC_REQ\u0000\u0000CS_WHIP_ACK\u0000\u0000\u0000\u0000\u0000CS_UPLOAD_GUILDMARK\u0000\u0000\u0000\u0000\u0000CS_REQ_GUILDMARK_IMAGE\u0000\u0000CS_REQ_GUILDMARK_IMAGE_ACK\u0000\u0000",
        "rva": "0xda6b8c"
      },
      {
        "term": "protect",
        "offset": "0xdbbd08",
        "encoding": "ascii",
        "context": "\ufffd\ufffd#@\u0001\u0000\u0000\u00000\ufffd#@\u0001\u0000\u0000\u0000\ufffd\ufffd.@\u0001\u0000\u0000\u0000\u0010\ufffd.@\u0001\u0000\u0000\u0000Ocupy\u0000\u0000\u0000Protected\u0000\u0000\u0000Peace\u0000\u0000\u0000\u0000\u0000\u0000\u0000Conquest\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\ufffd\u0011\ufffd@\u0001\u0000\u0000\u0000\ufffd\ufffd\u0001@\u0001\u0000\u0000\u0000\ufffd\ufffd.@\u0001\u0000\u0000\u0000\ufffd\ufffd\ufffd@\u0001\u0000\u0000\u0000 6\ufffd@\u0001\u0000\u0000\u0000\u0010\ufffd\ufffd@\u0001\u0000\u0000\u0000p,\ufffd@\u0001\u0000\u0000\u0000\ufffd'\u0002@\u0001\u0000\u0000\u0000\ufffd/\ufffd@\u0001\u0000\u0000\u0000\ufffd/\ufffd@\u0001\u0000\u0000\u0000",
        "rva": "0xdbd108"
      },
      {
        "term": "protect",
        "offset": "0xdc025d",
        "encoding": "ascii",
        "context": "ng Volition\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Wind Shield\u0000\u0000\u0000\u0000\u0000Mana Protective Barrier\u0000Focus\u0000\u0000\u0000Lightning Calculation\u0000\u0000\u0000Lightning Magic\u0000Power of Pauldron\u0000\u0000\u0000\u0000\u0000\u0000\u0000Inner Calm\u0000\u0000\u0000\u0000\u0000\u0000Mana Enhancem",
        "rva": "0xdc165d"
      },
      {
        "term": "protect",
        "offset": "0xdc02f7",
        "encoding": "ascii",
        "context": "hancement\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Spark of Life\u0000\u0000\u0000Divine Protection\u0000\u0000\u0000\u0000\u0000\u0000\u0000Calmness\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Audaciousness\u0000\u0000\u0000Godly Courage\u0000\u0000\u0000Abundant Mana\u0000\u0000\u0000Soul Band\u0000\u0000\u0000\u0000\u0000\u0000\u0000Protection of Earth\u0000Rag",
        "rva": "0xdc16f7"
      },
      {
        "term": "protect",
        "offset": "0xdc0358",
        "encoding": "ascii",
        "context": "urage\u0000\u0000\u0000Abundant Mana\u0000\u0000\u0000Soul Band\u0000\u0000\u0000\u0000\u0000\u0000\u0000Protection of Earth\u0000Rage\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Good Luck\u0000\u0000\u0000\u0000\u0000\u0000\u0000Boiling Blood\u0000\u0000\u0000Pendatron's Divine Protection\u0000\u0000\u0000Pendatron Divine Protect",
        "rva": "0xdc1758"
      },
      {
        "term": "protect",
        "offset": "0xdc03ab",
        "encoding": "ascii",
        "context": "\u0000\u0000\u0000\u0000\u0000Boiling Blood\u0000\u0000\u0000Pendatron's Divine Protection\u0000\u0000\u0000Pendatron Divine Protection\u0000\u0000\u0000\u0000\u0000Pendatrons Cloak\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Invisibility Potion\u0000\u0000\u0000\u0000\u0000Premium Invisibility Potion",
        "rva": "0xdc17ab"
      },
      {
        "term": "protect",
        "offset": "0xdc03c9",
        "encoding": "ascii",
        "context": "'s Divine Protection\u0000\u0000\u0000Pendatron Divine Protection\u0000\u0000\u0000\u0000\u0000Pendatrons Cloak\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Invisibility Potion\u0000\u0000\u0000\u0000\u0000Premium Invisibility Potion\u0000\u0000\u0000\u0000\u0000Potion of Invisibility (G",
        "rva": "0xdc17c9"
      },
      {
        "term": "protect",
        "offset": "0xdc0526",
        "encoding": "ascii",
        "context": "\u0000\u0000Buff Attack\u0000\u0000\u0000\u0000\u0000Armour Potion\u0000\u0000\u0000Magic Protection Potion\u0000 \"\ufffd@\u0001\u0000\u0000\u0000\ufffd\ufffd\u0001@\u0001\u0000\u0000\u0000\ufffd\ufffd1@\u0001\u0000\u0000\u0000\ufffd\ufffd\ufffd@\u0001\u0000\u0000\u0000 6\ufffd@\u0001\u0000\u0000\u0000\u0010\ufffd\ufffd@\u0001\u0000\u0000\u0000p,\ufffd@\u0001\u0000\u0000\u0000\u0000\ufffd)@\u0001\u0000\u0000\u0000\ufffd/\ufffd@\u0001\u0000\u0000\u0000\ufffd/\ufffd@\u0001\u0000\u0000\u0000\ufffdw\ufffd@\u0001\u0000\u0000\u00000t\ufffd@\u0001\u0000\u0000\u00000t\ufffd@\u0001\u0000",
        "rva": "0xdc1926"
      },
      {
        "term": "protect",
        "offset": "0xdc866b",
        "encoding": "ascii",
        "context": "e. When disabled, you will no longer be protected by the Securecode system.\u0000\u0000Disable\u0000The Securecode system has been disabled.\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000The Securecode syst",
        "rva": "0xdc9a6b"
      },
      {
        "term": "protect",
        "offset": "0xdc8b56",
        "encoding": "ascii",
        "context": ") until you can try again.\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000Macro protection puzzle\u0000Click the matching symbol\u0000\u0000\u0000\u0000\u0000\u0000\u0000Target:  %s\u0000\u0000\u0000\u0000\u0000Step %u of %u\u0000\u0000\u0000%u attempts - %u seconds\u0000\u0000\u0000\u0000\u0000\u0000\u0000\u0000PA\ufffd@\u0001\u0000",
        "rva": "0xdc9f56"
      },
      {
        "term": "protect",
        "offset": "0xefc067",
        "encoding": "ascii",
        "context": "appingW\u0000\u0000\ufffd\u0003InitOnceExecuteOnce\u0000\u001a\u0006VirtualProtect\u0000\u0000-\u0006WakeAllConditionVariable\u0000\u0000\ufffd\u0005SleepConditionVariableSRW\u0000\t\u0005RtlCaptureContext\u0000\u0011\u0005RtlLookupFunctionEntry\u0000\u0000\u0018\u0005RtlVirt",
        "rva": "0xefd467"
      },
      {
        "term": "protect",
        "offset": "0xefe8e7",
        "encoding": "ascii",
        "context": "BinaryA\u0000\u0000|\u0000CryptBinaryToStringA\u0000\u0000\ufffd\u0000CryptProtectData\u0000\u0000\ufffd\u0000CryptUnprotectData\u0000\u0000CRYPT32.dll\u0000[\u0000NCryptOpenStorageProvider\u0000Y\u0000NCryptOpenKey\u0000B\u0000NCryptCreatePersistedKey\u0000\u0000b",
        "rva": "0xeffce7"
      },
      {
        "term": "protect",
        "offset": "0xefe8fd",
        "encoding": "ascii",
        "context": "ToStringA\u0000\u0000\ufffd\u0000CryptProtectData\u0000\u0000\ufffd\u0000CryptUnprotectData\u0000\u0000CRYPT32.dll\u0000[\u0000NCryptOpenStorageProvider\u0000Y\u0000NCryptOpenKey\u0000B\u0000NCryptCreatePersistedKey\u0000\u0000b\u0000NCryptSetProperty\u0000O\u0000N",
        "rva": "0xeffcfd"
      }
    ]
  },
  {
    "file": "unins000.exe",
    "bytes": 6940402,
    "sha256": "d4f76ba3296335cdd5f0387f3bff51496d855b15be1bfdbaa9f8788e83256aeb",
    "image_base": "0x400000",
    "rtti": [],
    "hits": [
      {
        "term": "protect",
        "offset": "0x65cf9",
        "encoding": "ascii",
        "context": "Visibility\u0001\u0000\u0000\u0000\u0000\u0003\u0000\u0000\u0000\ufffdhF\u0000\u0000\u0000\u0000\u0000\tmvPrivate\u000bmvProtected\bmvPublic\u000bmvPublished\u000eSystem.TypInfo\u0002\u00000iF\u0000\u0000\u0000\u0000\u0000\u0003\u000bTMethodKind\u0001\u0000\u0000\u0000\u0000\n\u0000\u0000\u0000(iF\u0000\u0000\u0000\u0000\u0000\u000bmkProcedure\nmkFunction\rmkConstruct",
        "rva": "0x668f9"
      },
      {
        "term": "protect",
        "offset": "0x4ee7db",
        "encoding": "ascii",
        "context": "teger): Boolean;\u0000\ufffd\u0004\u0001\u0000\ufffd\ufffd\ufffd\ufffd@\u0000\u0000\u0000function IsProtectedSystemFile(const Filename: String): Boolean;\u0000\u0000\u0000\u0000\ufffd\u0004\u0001\u0000\ufffd\ufffd\ufffd\ufffd9\u0000\u0000\u0000function MakePendingFileRenameOperationsChecksum: S",
        "rva": "0x4ef3db"
      },
      {
        "term": "protect",
        "offset": "0x517162",
        "encoding": "ascii",
        "context": "\u0000\u0000ShellExecAsOriginalUser\u0000\ufffd\u0004\u0001\u0000\ufffd\ufffd\ufffd\ufffd\u0015\u0000\u0000\u0000ISPROTECTEDSYSTEMFILE\u0000\u0000\u0000\ufffd\u0004\u0001\u0000\ufffd\ufffd\ufffd\ufffd'\u0000\u0000\u0000MAKEPENDINGFILERENAMEOPERATIONSCHECKSUM\u0000\ufffd\u0004\u0001\u0000\ufffd\ufffd\ufffd\ufffd\r\u0000\u0000\u0000MODIFYPIFFILE\u0000\u0000\u0000\ufffd\u0004\u0001\u0000\ufffd\ufffd\ufffd\ufffd\u000e\u0000\u0000\u0000REGIST",
        "rva": "0x517d62"
      },
      {
        "term": "protect",
        "offset": "0x549e71",
        "encoding": "ascii",
        "context": " ]\ufffd\ufffd\u0004\u0002\u0000\ufffd\ufffd\ufffd\ufffd\u0007\u0000\u0000\u0000s\u0000f\u0000c\u0000.\u0000d\u0000l\u0000l\u0000\u0000\u0000SfcIsFileProtected\u0000\u0001\ufffd\ufffd\ufffd\ufffdUH\ufffd\ufffdPH\ufffd\ufffdH\ufffdm8H\ufffdM`\ufffdUhL\ufffdEpL\ufffdMxH\ufffd\ufffd\ufffd\u0000\u0000\u0000H\ufffd\ufffd\ufffd\u0000\u0000\u0000\ufffd\ufffd}h\u0002u\u0018\ufffdUpH\ufffdM`\ufffd2\u0000\u0000\u0000\ufffd\ufffd\ufffd\ufffd\ufffd=\u0002\u0001\u0000\u0000t\ufffd\ufffd}h\u0001\u000f\ufffd\ufffd\u0000\u0000\u0000H\ufffd}x\u0000t\nH\ufffdE@2\u0000\u0000\u0000\ufffd\t\ufffd\ufffd\ufffd\ufffd\ufffdH",
        "rva": "0x54aa71"
      },
      {
        "term": "protect",
        "offset": "0x4d65c2",
        "encoding": "utf-16le",
        "context": "ault bitness: 32-bit\u0000\u04b0\u0002\uffff\uffff2\u0000Dest file is protected by Windows File Protection.\u0000\u0000\u04b0\u0002\uffff\uffff\u001a\u0000Time stamp of our file: %s\u0000\u0000\u04b0\u0002\uffff\uffff(\u0000Time stamp of our file: (failed to read)\u0000",
        "rva": "0x4d71c2"
      },
      {
        "term": "protect",
        "offset": "0x4d65f6",
        "encoding": "utf-16le",
        "context": "\u0000Dest file is protected by Windows File Protection.\u0000\u0000\u04b0\u0002\uffff\uffff\u001a\u0000Time stamp of our file: %s\u0000\u0000\u04b0\u0002\uffff\uffff(\u0000Time stamp of our file: (failed to read)\u0000\u0000\u04b0\u0002\uffff\uffff\u0011\u0000Dest file exists.\u0000\u04b0",
        "rva": "0x4d71f6"
      },
      {
        "term": "protect",
        "offset": "0x4d6daa",
        "encoding": "utf-16le",
        "context": "stamp. Skipping.\u0000\u04b0\u0002\uffff\uffff@\u0000Existing file is protected by Windows File Protection. Skipping.\u0000\u0000\u04b0\u0002\uffff\uffff\u0004\u0000\r\n\r\n\u0000\u0000\u04b0\u0002\uffff\uffffJ\u0000User opted not to strip the existing file's read-only",
        "rva": "0x4d79aa"
      },
      {
        "term": "protect",
        "offset": "0x4d6dde",
        "encoding": "utf-16le",
        "context": "sting file is protected by Windows File Protection. Skipping.\u0000\u0000\u04b0\u0002\uffff\uffff\u0004\u0000\r\n\r\n\u0000\u0000\u04b0\u0002\uffff\uffffJ\u0000User opted not to strip the existing file's read-only attribute. Skipping.\u0000\u0000\u04b0\u0002\uffff",
        "rva": "0x4d79de"
      },
      {
        "term": "protect",
        "offset": "0x53557c",
        "encoding": "utf-16le",
        "context": "\u504b\u02ba\u0000\ue800\ub0d8\uffad\u8d48\u808b\u0000\ue800\uafec\uffad\u8d48\u988b\u0000\ue800\uafe0\uffad\u8d48\u2865\u5d5b\u00c3\u04b0\u0002\uffff\uffff\u0004\u0000.tmp\u0000\u0000\u04b0\u0002\uffff\uffff\n\u0000protected \u0000\u0000\u04b0\u0002\uffff\uffff!\u0000Created %stemporary directory: %s\u0000\u04b0\u0002\uffff\uffff\u0007\u0000_isetup\u0000\u5657\u4853\uec83\u4820\ucb89\u8948\u48d6\ufffd\u2be8\uadcd\u48ff\uc189\ua3e8\uaede\u85ff\u40c0\u950f\u40c7\uff84\u2e75\u23e8\uaee1\u83ff\u02f8\u2474\u19e8\uaee1\u83ff\u03f8\u1a74\u3fe8\uaee3\u2bff\u3dc6\u07d0\u0000\u0c73\u32b9\u0000\ue800\u349c\uffb1\ub7eb\u8940\u48f8\uc483\u5b20\u5f5e\uccc3\ucccc\ucccc\ucccc\ucccc\ucccc",
        "rva": "0x53617c"
      },
      {
        "term": "protect",
        "offset": "0x69bb86",
        "encoding": "utf-16le",
        "context": "Password:\u0000This installation is password protected.\u0000Please provide the password, then click Next to continue. Passwords are case-sensitive.\u0000&Path:\u0000You must be lo"
      }
    ]
  }
]
````

### Ek: evidence/static-profile/vtable.json

SHA-256: `9d98db9ff8b184ffaf5fa21552b98a936f07b830f94440dcb8f0bb3768bbe5f6`. Boyut: 1402 bayt.

````json
[
  {
    "method": "0xdc9618",
    "vtable": "0xdc94b8",
    "slot": "0x160",
    "col": "0xe540c0",
    "class": ".?AVCTSecuritySystemDlg@@",
    "slots": {
      "0x0": "0x1e4d0",
      "0x8": "0x382f30",
      "0x10": "0xb5cab0",
      "0x18": "0xb53620",
      "0x20": "0xb5b410",
      "0x28": "0xb52c70",
      "0x30": "0x384080",
      "0x38": "0xb52fe0",
      "0x40": "0xb52fd0",
      "0x48": "0xb577d0",
      "0x50": "0xb57430",
      "0x58": "0xb57430",
      "0x60": "0x14610",
      "0x68": "0xb53df0",
      "0x70": "0xb53dd0",
      "0x78": "0xb5b5d0",
      "0x80": "0x238be0",
      "0x88": "0xb532b0",
      "0x90": "0xb51c10",
      "0x98": "0xb5b5e0",
      "0xa0": "0x383630",
      "0xa8": "0xb5b900",
      "0xb0": "0xb5c740",
      "0xb8": "0xb5bf30",
      "0xc0": "0x238e70",
      "0xc8": "0x238e10",
      "0xd0": "0xb5c0c0",
      "0xd8": "0xb5c5c0",
      "0xe0": "0xb5c830",
      "0xe8": "0xb5c8d0",
      "0xf0": "0xb5c790",
      "0xf8": "0x3836d0",
      "0x100": "0xb5c020",
      "0x108": "0xb5c520",
      "0x110": "0xb5bb70",
      "0x118": "0xb5bf80",
      "0x120": "0xb5c480",
      "0x128": "0xb5b6b0",
      "0x130": "0xb5b770",
      "0x138": "0xb5b830",
      "0x140": "0xb553c0",
      "0x148": "0xd6fb0",
      "0x150": "0xb5b280",
      "0x158": "0xb5c650",
      "0x160": "0x3839d0"
    }
  }
]
````

### Ek: evidence/uac-manifest.xml

SHA-256: `45a4328147155fd936a4fd092230faa88effebf29ead960c72f266443e41bb73`. Boyut: 638 bayt.

````xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="4UnityPuzzleTest"></assemblyIdentity>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3"><security><requestedPrivileges><requestedExecutionLevel level="requireAdministrator" uiAccess="false"></requestedExecutionLevel></requestedPrivileges></security></trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1"><application><supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"></supportedOS></application></compatibility>
</assembly>
````

### Ek: evidence/window-tests.json

SHA-256: `a3b61864212a578509482167dd124a282680fcd367377e03d69cd75e83e4a6af`. Boyut: 148 bayt.

````json
{
  "pass": true,
  "checks": 23,
  "inputs": 4,
  "accepted": 4,
  "target_pid": 13064,
  "live_game_accessed": false,
  "complete": true
}
````

### Ek: Dosya envanteri

| Dosya | Bayt | SHA-256 |
|---|---:|---|
| .gitattributes | 109 | 3947acfc61c5d84e6ffadb347f0a204cbacf2657a21687325e6f2e7222d0ad57 |
| .gitignore | 64 | 7fb5110bfe1131d2fe27ddb4f5f6b60333d06934f78a2b8534a501524a0a9043 |
| evidence/auto-tests.json | 22522 | da760c7dcd94229ea8161d7d8789990324f371ea3442a9d5886826e7238fa693 |
| evidence/automatic-session.json | 255 | f1da3651bac056476e6c2f1b4aa0901bb86122099f18479a6086b64083a44eb3 |
| evidence/build.json | 467 | 11da975e2ca3793041b10bf0afc0e019c4c388c31030177cc0d766d7948eeeb6 |
| evidence/fixture-step-1.png | 8044 | 1975b5ccfc46c8b1d114e0c8ae3a8447645e322dea79853ad56c93fef4763bd6 |
| evidence/fixture-step-2.png | 8138 | d9862f360962d45bdd5a5fe7faa72fea904d8f7002ae04d35828dbbb5480f47d |
| evidence/fixture-step-3.png | 8211 | d8fc88350cf7796e3b65c0a057ad2bb77b2cbc76d5bbf71d1abeeb6a2c31594f |
| evidence/fixture-step-4.png | 8023 | 9280da8b5400e5900ce351071ba47a813b51e0667c1ab275ad2ddf7faf7d6d4b |
| evidence/image-readings.json | 5734 | 01abadddc756cd03ddfc0010977df6abc88d3b719872377df8d4c9bf7f7ae682 |
| evidence/memory-probe.json | 269 | aa4063fc0f711f1350aec9df682bd50bf3b339b49122fc8bf04b333a2c9479f7 |
| evidence/puzzle-test-ui.png | 49594 | 6dcd9f82b290cf6885f4daf36c7435f448c21e7d31a48d22b59f02ac2a33607d |
| evidence/self-tests.json | 3692 | 97575649e554c57e08b8cf29741aecaaba2060aaded72986dd14b26caee67d6a |
| evidence/static-profile/refs.json | 42078 | b24b9380c20125a2392bf3da5553e37836d7581b29f69bc69dfa8c67f5e4c771 |
| evidence/static-profile/strings.json | 23882 | dd624187a7f39df961bb542a4e027b4c2fb8d00fbd90238442442754f13cb040 |
| evidence/static-profile/vtable.json | 1402 | 9d98db9ff8b184ffaf5fa21552b98a936f07b830f94440dcb8f0bb3768bbe5f6 |
| evidence/uac-manifest.xml | 638 | 45a4328147155fd936a4fd092230faa88effebf29ead960c72f266443e41bb73 |
| evidence/window-tests.json | 148 | a3b61864212a578509482167dd124a282680fcd367377e03d69cd75e83e4a6af |
| README.md | 1952 | 83acad1012c8f444f29900b942d5c6be82884291db7f44326b57398010cd3be3 |
| references/step-1.jpg | 42541 | a0871e0847c9449171502ad604d2cb2dd56ec2d3a57dee46a4d28b449b78fa67 |
| references/step-2.jpg | 41421 | 4af89214a1bcd497a58af61c14c045a746ca07ac77a0169914b48dda8a2c107c |
| references/step-3.jpg | 37730 | fdc1ab5e2ecabff18001b61da72e9eb35e28771c1058b45192c41fa56068e921 |
| references/step-4.jpg | 34508 | be785458663b018cacdc363fd876fcab7fb7cad28ea0ae7e05413442b47e01ed |
| report-tools/captcha_report_body.md | 25512 | 3e05a66f25ac47ad99b716d4f907c3fd82152bce558bed739632e5e378275dc6 |
| report-tools/export_captcha_report.py | 5805 | 35d594385ff6fe131c4b1e0c7d648bb678345ef4f7997b2372c85fe2a683838c |
| report-tools/verify_captcha_archive.py | 1881 | 36a5e48976d4db0d7b7b54279e669ed76c9cf57e65cd04a5becbe809a72df0a2 |
| research/puzzle_static_probe.py | 2353 | b653f0e49152cf05ddccef075561b0ef67508f33419fdefddc23116daff87626 |
| research/puzzle_string_refs.py | 2075 | 089daf4c93fc440a4664c2db591488c0742d0a5c3bbab6127119157c39e31e55 |
| research/puzzle_vtable_probe.py | 1897 | 795d01ed9122c349a2dac305964363fe87aa745974290c96d8a9846c41ebd5f5 |
| research/requirements.txt | 18 | 94b6292179914274eb5d637b9681a4843da3e5bc2b007d2a5554f353a266a80c |
| src/4UnityPuzzleTest.csproj | 625 | 37fa1276bd1d9a5b26e18be4d581f5948512aaef0d9ff2771c22849f87bf468d |
| src/app.manifest | 562 | 434c83886041666b1e9715281478cfdb052bf624d6281eae342de185737b7f7f |
| src/AutoTests.cs | 5053 | ede745f0f5c26940734ec19a9879a7ea747dfe69fb48084fd9327ae43efefdbf |
| src/CropForm.cs | 3216 | 311f3f06b745ebf4ffb7300a82dc9b473500a1736197eba7942e17ae03c38dc3 |
| src/DemoForm.cs | 4044 | 7e1b4cf3358d49c910fea9ec9207c0a6072ecd7211f20cf50f2edfa17349e394 |
| src/docs/puzzle_detection_profile.md | 2375 | e63a2c4250b63b8c00d5130caebe6b9b4c346b57d5a9d51e161c95b31587c62c |
| src/KULLANIM.txt | 4255 | 8128b33b939df0e7f5c0572a6bc3deb64182ff3bc081c71829db6768e1d2dd31 |
| src/MainForm.cs | 20868 | aa955b9da3943642e544379ea1943b99b8390f20d9ea68354c4d2297335cdef5 |
| src/MemoryDetector.cs | 9552 | f14f167ac004a3870e328f965a62c23a4932aa2d24af3c504afb2c45510e838a |
| src/OcrReader.cs | 6397 | 02f59444e61ba8c970bda74a774fa4f74ee374ef202bb6fa0fca064997800f0f |
| src/Program.cs | 6165 | 2f7c12f3310deae1e4e53fe09863a88cc3598faad613744efa4772c8ceedb377 |
| src/PuzzleFlow.cs | 3692 | 77b7377fcd6dd61c3c4286f8cfce596e8ef7f9984cf0813aca3888a9241c1302 |
| src/PuzzleLocator.cs | 2527 | 54bb81a48c1c00936e8f6631e4d0ce06839c6774fd6fde8afe6e1f1493b01463 |
| src/README.md | 4903 | 014416459ce4c855cb0008be2dd80f496e0f737859b4cdcd699babb735c7abdd |
| src/SelfTests.cs | 10049 | af6f88a516518aa3644c2481405d640df2b5463c3a4da16d3ed0e99e742f5554 |
| src/TargetWindow.cs | 7137 | 82947640cf79cf6a2a54dd3958aad28f8ecf2c2c10063742453247b1c84002d5 |

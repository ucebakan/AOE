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

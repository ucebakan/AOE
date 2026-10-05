# Collection araştırması — 2026-10-05

Güncel TClient SHA:
`4DC9C526A895A10113C4CF23F2D199BFF283D2C7CE75F949DC49CEF15D27D622`.
Güncel FileMerger SHA:
`B770838B6E1EB731792202A520AE1CCFC73CAE249B0CB46EDEA5272B38822142`.

## Doğrulanan bulgular

- Güncel paket offline çözüldü; kayıt sayısı ve parser bitişi doğrulandı.
  Collection adlı skill 9909, hedef SELF, range type 0. Resource function
  kayıtlarında status type 6 / function 99 bulunuyor. Selector numarası tek
  başına otomatik toplama kararının hangi tarafça verildiğini kanıtlamaz.
- Collection, Collection (1H/1D/7D/30D), Gift of Gods Collection ve Autoloot
  adlarıyla birden fazla item çeşidi var. Her çeşidin aynı kullanım yolunu
  kullandığı varsayılmadı. İlk body WORD ile skill ilişkisi loader doğrulaması
  tamamlanana kadar candidate olarak tutulur.
- Kullanıcı Collection buff'ının aktif olduğunu, range 0–2 feet ve 11m
  gösterdiğini bildirdi.
- Salt okunur canlı kontrolde player+0x1248 ağacında 3 CTClientMaintain
  kaydı bulundu. RTTI `CTClientMaintain` vtable RVA 0xda99b0 ile doğrulandı.
  Bir kaydın +0x20 pointer'ı, bağımsız çözülen güncel FindTSkill ağacındaki
  Collection 9909 resource pointer'ıyla birebir eşleşti. Yani aktif Collection
  kaydının client'taki yeri bulundu. Süre/etki alanları henüz adlandırılmadı.
- FindTSkill yolu mevcut UseTItem type 33 dalındaki çağrıdan türetildi;
  disk/canlı lookup fingerprint'i ve bounded tree kimliği kontrol edildi.
- Bu tur oyun belleğine yazılmadı, oyun metodu çağrılmadı, item kullanılmadı
  ve breakpoint/debugger kurulmadı.

## Sunucu / client ayrımı

Aktif buff'ın client belleğinde bulunması, buff'ın burada serbestçe üretilebildiği
veya otomatik loot'un burada yönetildiği anlamına gelmez. Güncel sunucu kodu yok.
Bu nedenle **“Collection kesin client taraflı, eşyasız çalışır”** sonucu yok.

Elimizdeki [eski 4Story referansı](https://github.com/exmex/4s/tree/a36a5785cc20da19cd460afa92a3f96e18ecd026)
güncel 4Unity sunucusu değildir. Bu referansta ITEMUSE_REQ sunucuda envanter,
slot, item varlığı ve count denetimleri yapar (`CSHandler.cpp:7373` ve devamı).
Loot aktarımı `MONITEMTAKEALL_REQ` → `MonItemTake` üzerinden sunucuda yapılır;
eşya sahipliği ve çanta koşulları kontrol edilir (`TMapSvr.cpp:7013` ve devamı).
Bunlar yalnız eski referansın doğrulanmış davranışıdır; güncel sunucu aynı
kontrolleri yapıyormuş gibi kesin konuşulmaz.

Salesman'da NPC-list isteğini item kullanımından ayrı bir native yoldan
gönderebildik. Collection için aynı tür ayrı ve kabul edilen otomatik loot
yolu henüz bulunmadı. Eski loot opcode adayları güncel client'ta aynı isimle
eşleştirilemedi; bu, yeni client'ta loot isteği olmadığına dair kanıt değildir.

Sıradaki araştırma aktif Collection → otomatik loot isteği / server sonucu
bağlantısını kurmak ve buff kapalı durumla karşılaştırmaktır. Client yalnız
normal loot isteğini otomatik yolluyorsa otomasyon yolu incelenebilir. Sunucu
Collection hakkını veya buff'ı zorunlu kontrol ediyorsa client'taki ikon/kayıt
değişikliği bu hakkı sağlamaz. Henüz işlev ekleyen bir Collection butonu yoktur.

## Kanıtlar

Araştırma araçları ve raporlar `C:/Users/Public/Documents/4UnitySkillProfiler`
altındadır:

- `tools/collection_resource_research.py`, `logs/collection_resource_research.json`
- `tools/collection_static_research.py`, `logs/collection_static_candidates.json`
- `tools/collection_loot_paths.py`, `logs/collection_loot_paths.json`
- `logs/collection-live-identity.json` — salt okunur aktif resource kimlik eşleşmesi.

Birleşik uygulamadaki `--collection-probe <rapor.json>` yalnız bu araştırmanın
salt okunur snapshot yoludur; normal UI akışında çalışmaz.

## İkinci inceleme: güncel normal loot yolu

Güncel `CPacket::SetID` RVA `0xa20ca0` hedefli gerçek E8 instruction çağrıları
üzerinden 458 builder kaydı çıkarıldı. Bunlar packet numarası tahminiyle
adlandırılmadı; payload ve çağıran davranışları karşılaştırıldı.

- `0xa6d30`, `0x512e`: target pointer null değilse actor+0x770 DWORD kimliğini
  serileştirir ve owner üzerinden `0x1a83b0` dağıtım yoluna verir. Doğrulanmış
  tek direct E8 caller `0x1bedb0`'dır. İçeren mantıksal metod `0x1becc0` hedef,
  session, loot penceresi ve yatay uzaklık kontrollerini yapar. Uzaksa GM 0x9e
  için hareket ayarlar; yakınsa bu sender'ı çağırır. Pencere açık dalında tekil
  item alma ve para alma yollarını kullanır. Böylece normal GetAll semantiği
  mevcut client üzerinde eşleştirildi. `.pdata` parçaları tüm metod sanılmadı.
- Komşu yollar payload/caller karşılaştırmasıyla eşleştirildi: list `0xa6c30`
  / `0x5072` (BYTE open + DWORD mob), tekil item `0xa6da0` / `0x5074` (DWORD
  mob + 3 BYTE slot), para `0xa6e40` / `0x50f3` (target DWORD). Bunlar legacy
  opcode'lar değildir.
- Genel `CountMaintainFunc` eşdeğeri `0x21b400`, player+0x1248 maintain ağacını,
  maintain+0x20 skill pointer'ını, resource+c0/c8 function vector'ünü ve
  descriptor+1 type / +2 function selector'ını okur. 19 doğrulanmış direct
  E8 çağrıda `(type=6, function=99)` kullanımı görülmedi. İndirect/dinamik ve
  farklı helper yolları bu negatif sonucun kapsamı dışındadır.
- Güncel skill effect uygulaması `0x21b740` resource function vector'ünü
  dolaşır. Type 6 dalındaki `0x21b98e` function selector'ını okur; `selector-1`
  unsigned >0x46 ise `0x21bcea` skip yoluna gider. Yani bu status dispatch
  yalnız selector 1..71 aralığını işler; **Collection 99 bu dalda atlanır**.
  Maintain kaldırma eşdeğeri `0x225d70` type 6 dalında selector 2..71 aralığı
  dışında skip yapar; 99 burada da atlanır. Bu, bu iki genel dispatch için
  pozitif kontrol akışı kanıtıdır; client'ın tamamında özel Collection yolu
  bulunmadığı veya güncel sunucunun ne yaptığına dair mutlak kanıt değildir.

Bu bulgular native Collection otomatik aktarımının sunucuda yönetilmesi
ihtimalini güçlendiriyor. Gerçek mob ölümü sırasında client outgoing loot
isteği / inbound item güncellemesi henüz izlenmedi; ayrım kesinleşmedi.

## Buff kapalıyken normal loot deneyi

Kullanıcı buff'ın süresinin bittiğini ve artık aktif olmadığını bildirdi.
`4UnitySkillProfiler/tools/CollectionLootTest` altında ayrı, sınırlı test aracı
hazırlandı. Birleşik uygulamaya henüz Collection işlevi eklenmedi.

Araç güncel EXE ve archive SHA, canlı PE, PID/creation, owner/player/target,
RTTI vtable, native sender/koordinat/death instruction fingerprint'lerini
doğrular. Mob tipi 2, death action 6/7, player alive/ghost değil, player için
karşılıklı parent bağlantısı yok, uzaklık <=2 oyun birimi ve bounded maintain ağacında 9909 yok koşullarını
kontrol eder. Son koşullar oyun UI thread'inde çağrıdan hemen önce tekrar
okunur. Windows kısa pencere hook'u üzerinden `0xa6d30(owner,target)` yalnız
bir kez çağrılır; retry/döngü, breakpoint, kod patch'i veya buff yazımı yok.

Native çağrı dönüşü sunucu başarısı değildir. Kullanıcı yerde kalan eşyanın
çantaya girmesini gözlemlemelidir. Başarılı olursa normal loot yolunun buff
olmadan kullanılabildiği kanıtlanır; native Collection buff hakkı, sunucu
kontrolleri veya bütün haritadan sınırsız toplama kanıtlanmış olmaz.

Yerel fixture sonuçları: UI-thread hook tek çağrı, duplicate message, stale
PID/creation/deadline; canlı mob, uzaklık, hedef değişmesi, aktif 9909 ve cycle
ağaç reddi kontrolleri PASS. Testler ayrı test DLL'leriyle oyuna erişmeden
çalıştırıldı. Dağıtılan EXE'nin requireAdministrator/uiAccess=false manifesti
ayrıca doğrulandı. İki dosyalı dağıtımda yalnız EXE ve runtime bridge DLL bulunur.

Yeni kanıtlar:

- `logs/collection_packet_map.json`, `logs/collection_path_detail.json`
- `logs/collection_effect_paths.json`, `logs/collection_dispatch_scan.json`
- `logs/collection-getall-current-disassembly.txt`
- `logs/collection-apply-disassembly.txt`, `logs/collection-status-dispatch-disassembly.txt`
- `logs/collection_test_profile.json`, `logs/collection-loot-fixture-tests.json`
- `releases/collection-loot-test-v1/collection-loot-test.log` — varsa test çağrısı
  kaydı; oyun sonucu için tek başına başarı kanıtı değildir.

İlk canlı v1 denemesi guard 16 ile durdu; istek gönderilmedi. Kullanıcı yürüyerek
durduğunu, binekte olmadığını bildirdi. Nonnull player/monster parent pointer'ını
tek başına binek sayan kontrol fazla genişti. V2 player getter `0x138bf0` /
`0x138c90` koşulunu izler: yalnız parent+0x1438 == player karşılıklı link varsa
normal player matrix'i yerine parent matrix'i kullanılır, bu dar testte işlem
engellenir. Diğer nonnull player bağlantıları fallback player matrix yolunu
kullanır. Monster'ın doğrulanmış `0x21fcb0` / `0x21fcd0` getter'ları parent'tan
bağımsız kendi matrix'ini okur; monster parent pointer'ı engellenmez. Yeni
fixture testleri nonnull fakat karşılıksız player parent'ını kabul, karşılıklı
player parent'ını red ve nonnull monster parent'ını kabul durumlarında PASS.
`logs/collection-loot-fixture-tests-v2.json` ve v2 UAC manifesti doğrulandı.
V2 kullanıcı isteğiyle açıldı; oyun içi başarı sonucu henüz yok.

V3: kullanıcı hedef/konum hatasından sonra aynı pencerede tekrar deneyemediğini
bildirdi. Console akışı manuel yeniden denemeye dönüştürüldü: her Enter yeni
SHA/oturum/target/buff doğrulaması ve en fazla tek native istek başlatır; Q/EOF
çıkar. Guard hataları yeni Enter'ı bekler. Native çağrı/timeout veya hook kaldırma
sonucu belirsizse aynı pencerede başka istek engellenir; otomatik retry yoktur.
Yerel `TestRetries` hata→manuel ikinci deneme, EOF'ta sıfır çağrı, belirsiz
çağrıda başka deneme olmaması ve probe'un tek sefer kalması kontrollerini
geçti. `logs/collection-loot-fixture-tests-v3.json`: bridge/guard/retry PASS.
V3 UAC manifesti doğrulandı, ayrı v3 klasörüne paketlenip kullanıcı için açıldı.

## Yakından başarı ve uzak deneme — v4

Kullanıcı v3 ile Collection kapalıyken toplamanın çalıştığını doğruladı.
V3 log'unda bir uzaklık reddinden sonra guard=0, bridge_status=1, error=0 ile
5 manuel dispatch var (hedef ID'leri 4283040000 ve 4282908928). Bu sayılar beş
eşyanın alındığını kanıtlamaz; gerçek loot başarısı kullanıcı gözlemidir.
**Yakından normal loot yolu buff olmadan kullanılabiliyor.** Native Collection
buff'ı üretme, mob öldüğü anda otomatik aktarma veya uzak loot henüz kanıtlanmadı.

V3'ün 2 oyun birimi sınırı test aracı tarafından konmuştu; sunucu sınırı değildi.
Kullanıcı normal Collection'ın uzaktan da topladığını belirtti. V4 aynı native
normal loot sender'ına yalnız tek manuel istek çağıran uzak seçenek ekler:
Enter yakın <=2, console'da U+Enter uzak <=25 oyun birimi. Gerçek yatay mesafe
ekrana ve log'a yazılır. 25 de deney sınırıdır; sunucu hakkı/maksimum menzil veya
feet karşılığı gibi yorumlanmaz. Yeni, alınmamış loot bırakan ölü mob hedefi
seçili kalmalıdır. Collection yokluğu, oturum, RTTI, code, death ve bounded tree
guard'ları yakın ve uzak modlarda UI thread'inde yeniden kontrol edilir.

V4 iletişim version=2/message v2 ve CollectionLootBridgeV4.dll kullanır; oyunda
pinned kalmış eski bridge sürümüyle karışmaması için DLL adı ayrıdır. Yerel
testler yakın modun 5 birimi reddi, uzak modun 5/25'i kabulü, 25.1'i reddi,
bilinmeyen modun reddi ve U/Enter seçiminin doğru tek çağrı üretmesi dahil PASS.
`logs/collection-loot-fixture-tests-v4.json` ve UAC manifesti doğrulandı.
`releases/collection-loot-test-v4` paketlendi ve test için açıldı.

## Uzak loot sonucu doğrulandı

Kullanıcının v4 denemesinde 36.5915 ve 26.1792 oyun biriminde test aracının
25 birim guard'ı isteği göndermeden reddetti. 22.4206 oyun biriminde tüm
guard'lar geçti, native normal loot sender'ı bir kez çağrıldı. Kullanıcı
bu denemenin oyun içinde çalıştığını doğruladı. **Collection buff'ı kapalıyken
22.4206 yatay oyun birimi mesafeden normal loot isteğiyle toplama mümkün.**

Bu sonuç 25/26/36 birimin sunucu tarafından reddedildiğini göstermez; ilk iki
reddi test aracı yaptı. Maksimum sunucu menzili, tüm haritadan toplama, farklı
mob/ownership/party durumları ve native Collection'ın server/client otomatik
karar yeri bu denemeyle kesinleşmedi. Native buff kaydı üretmeden kullanılabilir
normal uzaktan loot yolu bulundu. Mob ölümünü gözleyip bu yolu tetikleyen
otomatik Collection işlevi henüz eklenmedi; mevcut v4 hâlâ manuel tek istektir.

## Hedef seçmeden otomatik deney — v6

Kullanıcı v4'te her ölü mobu tek tek seçmenin Collection davranışını sağlamadığını
belirtti; araçtaki tüm mesafe eşiklerinin kaldırılmasını istedi. Ardından ilk
otomatik deneyde kendi kill'lerini ayırmadan gözlenen yeni ölü moblara normal
loot isteği gönderilmesini, loot hakkını sunucunun kontrol etmesini açıkça seçti.

Güncel exact SHA için native FindMonster `0x171960..0x1719a3` diskte doğrulandı:
owner+0x1160 MSVC DWORD->monster pointer ağacı, node+0x20 key, node+0x28 actor.
Mevcut MobTP'nin aynı SHA için çözdüğü registry/actor/RTTI yollarıyla örtüşür.
V6 protocol/message ve ayrı CollectionLootBridgeV6.dll eski pinned bridge
sürümlerinden ayrılır. İstek 96 byte: mode=1, head/node/ID/actor kayıtları;
UI thread'inde bounded BST membership, parent/key aralığı/cycle, RTTI, death,
player/session, buff yokluğu ve exact native sender/lookup kodu yeniden okunur.
Mode=1 owner+0x1eb0 seçili hedefini kullanmaz ve hedefe yazmaz. Mode=0 manuel
test için seçili hedef koşulunu korur. Hiçbir modda araç mesafe eşiği yoktur.

200 ms read-only registry taraması canlı->ölü geçişlerini kuyruğa alır; ölüm
sonrası en az 200 ms bekler ve tur başına en fazla bir normal istek çağırır.
Başlangıçta/ilk görülüşte zaten ölü olanlar atlanır. Kimlik değişimi/despawn ve
yeniden canlanma eski kuyruğu geçersizleştirir. Aynı ölüm bir kez dispatch edilir;
server reddine otomatik retry yoktur. İstemciye yüklenmeyen mobları kapsamaz.
Killer/loot sahibi bilgisi bulunmuş gibi sunulmaz: bu kullanıcı tarafından
seçilen server ownership denetimli ilk otomasyon deneyidir.

Varsayılan EXE açılışı yalnız read-only liste kontrolü yapar; Enter izlemeyi
başlatır. Console'da S durdurur, Q/close/Ctrl+C çıkar. Oyun hotkey'i yoktur.
Oturum/kod/karakter veya belirsiz native call/cleanup hatasında yeni istek
engellenir. Oturum başına mutex ikinci otomasyonun aynı anda çalışmasını
engeller. Sürekli oyun içi patch/breakpoint/buff yazımı kullanılmaz.

`logs/collection-loot-fixture-tests-v6.json`: UI-thread bridge protocol,
seçili hedef olmadan registry membership ve sınırsız mesafe, stale node/ID,
baseline corpse atlama, çoklu ölüm, duplicate reddi, respawn, actor ID reuse,
despawn, registry cycle/parent/count bozukluğu ve manuel retry kontrolleri PASS.
UAC requireAdministrator/uiAccess=false doğrulandı. Otomatik sürümün gerçek
oyun içi loot aktarımı henüz kullanıcı testiyle doğrulanmadı. Kaynak ve paket
SkillProfiler tools/CollectionLootTest ve releases/collection-loot-test-v6'dadır;
birleşik 4UnityTools uygulamasına henüz Collection düğmesi eklenmedi.

V6 açıldı; read-only başlangıç kontrolü canlı PID 4052 oturumunda seçili hedef
gerektirmeden 16 registry kaydını doğruladı (`world_probe ... sends=0`). Bu
kontrol otomatik loot dispatch veya server başarı kanıtı değildir. Paket:
SkillProfiler/releases/4UnityCollectionLootTest-v6.zip. İzleme Enter bekler.

## Otomatik başarı ve grup hızlandırma — v7

Kullanıcı v6'nın hedef seçmeden otomatik topladığını oyun içinde doğruladı;
tek tek gecikmeli gönderim yerine Collection gibi hızlı grup işleme istedi.
V6'nın her mobdan sonra 200 ms timer beklemesi ve ölüm sonrası 200 ms kuyruğu
gecikmenin araç kaynaklı bölümüdür. Doğrulanan native sender hâlâ opcode 0x512e
ve tek DWORD mob ID'si taşır; çok-mob network paketi keşfedilmiş gibi sunulmaz.

V7 taramayı 50 ms'ye indirir, ölüm sonrası araç beklemesini kaldırır. Hazır
mobları aynı UI-thread callback içinde ardışık native sender çağrılarına
dönüştürür. IPC Batch 64 Request kapasitelidir; daha fazla hazır mob varsa
yeni grup araya timer beklemesi koymadan devam eder. Her native çağrı öncesi
aynı güncel registry/session/death/buff/code doğrulaması çalışır. Tekil paket
formatı korunur, mesafe sınırı yoktur. Ağ ve sunucu işlem süresi ayrı kalır.

Batch version/message/DLL v7 ayrıdır. Header CAS ve request CAS çift çağrıyı
engeller. Baştan karma oturum/duplicate ID reddedilir. Güvenli pre-call stale
mob hatası yalnız o üyeyi atlar; oturum/kod/native hata kalan grubu durdurur;
belirsiz veya kısmi gruba retry yapılmaz. Başarılı native dispatch ve atlanan
üyeler, grubun elapsed_ms değeriyle birlikte log'lanır. Oturum mutex'i v6 ile
ortaktır; eski izleme aktifse v7 menüye dönerek tekrar Enter bekler.

`logs/collection-loot-fixture-tests-v7.json`: single bridge ve batch bridge,
64 üyelik UI callback, duplicate mesaj/ID, pre-call üye atlama, ikinci üyede
fatal hata sonrası üçüncünün çağrılmaması, mixed-session reddi, registry ve
mesafesiz guard'lar, 150 aynı-anda ölümün 64/64/22 olarak beklemesiz boşalması
ve duplicate/respawn/despawn davranışları PASS. UAC manifesti korundu.
Kullanıcı v7 denemesi sonrası "çalışıyor" diyerek beklemesiz grup akışının
oyun içinde çalıştığını doğruladı. Gerçek sunucu aktarım gecikmesi sayısal
olarak ölçülmedi; batch elapsed_ms yalnız native çağrı süresidir. Hedef seçmeden,
araç mesafe sınırı olmadan normal loot isteklerini otomatik ve beklemesiz
gruplar halinde gönderme yolu kullanıcı testiyle doğrulandı.

## Birleşik uygulama · 0.8.0

Collection, 4UnityTools genel bakışında aç/kapat düğmesi ve ayrı doğrulama
sayfası olarak eklendi. V7'nin gözlem ve grup akışı, dinamik SHA/AOB/semantik
profil çözümleme, read-only başlangıç taraması, SafeMode ve sıralı kapanışla
birleştirildi. Durdurma callback/mapping temizliğini bekler; eski V6/V7
otomasyonu ile ortak mutex korunur. Ayrıntılar: [Collection 0.8.0](COLLECTION-0.8.0.md).

Sender ilişkisi GetAll'ın ilk `.pdata` aralığında bulunmaz; chained-unwind
ile aynı fonksiyona bağlı ileriki dispatch bloğundadır. Recovery bunu root
eşitliği ve gerçek E8 sender hedefiyle doğrular. Parent/maintain alanları
CTClientChar'a aittir; CTClientMonster nesne boyutuyla sınırlanmaz.

0.8.0 testleri native bridge/guard/cleanup, SHA cache ve relocation recovery,
grup gözlem/deduplication, SafeMode ve arayüzü kapsar. V7'nin önceki oyun içi
başarısı yeni birleşik EXE'nin sunucu başarı testi olarak sunulmaz.

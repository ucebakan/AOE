# 4Saga AOE Runtime Record Semantiği

Tarih: 2026-09-25  
Kapsam: Yalnız arkadaşın 4Saga AOE özelliğinin bulduğu çalışma zamanı kayıtlarının kimliği ile `+0x60` ve `+0x8C` alanlarının anlamı. Hiçbir süreç belleğine yazılmadı; gameplay patch uygulanmadı.

## İncelenen yapılar ve bütünlük

- 4Saga istemcisi: `C:\Program Files\4Saga Official\TClient.exe`
- 4Saga TClient SHA-256: `CD8FDA20BB99A6689DB99FCFE8B568DF027A0900927B797D8D5AB2FA3CC00EC7`
- 4Saga skill tablosu: `C:\Program Files\4Saga Official\Tcd\TSkill.tcd`
- 4Saga `TSkill.tcd` SHA-256: `BD6C4CA840A1DC06259391513214E61D8D91E459D390B3DAF8E94BA9914554D2`
- 4Saga efekt tablosu: `C:\Program Files\4Saga Official\Tcd\TSFX.tcd`
- 4Saga `TSFX.tcd` SHA-256: `D8C083908681C2FAA21506C06A9AE742F17B3FC96564B7E5C5978F96C6657E57`

4Saga ETCD v2 dosyaları, TClient içindeki format okuyucusu izlenerek salt-okunur biçimde çözüldü. `TSkill.tcd` açıldığında 146.114 byte ve 796 kayıt elde edildi; başlıktaki SHA-256 doğrulaması geçti. Her kayıt, bir byte uzunluklu ad + ad metni + sabit `0xA6` byte scalar gövde biçimindedir. 796 kaydın tamamı bu gramerle ayrıştırıldığında imleç dosyanın sonuna tam olarak ulaştı. Bu gramer, mevcut 4Unity TSkill araştırmasında kanıtlanan gramerle aynıdır.

## OBJECT IDENTITY

### Priest ID 425

- Gerçek TSkill adı: `Ice Rain - User Dependent`
- 4Saga `TSkill.tcd` kayıt indeksi: 176
- Skill ID: 425 (`0x01A9`)
- Arkadaş aracındaki sınıf etiketi: `Priest`

### Mage ID 322

- Gerçek TSkill adı: `Rain of Arrows - User Dependent`
- 4Saga `TSkill.tcd` kayıt indeksi: 135
- Skill ID: 322 (`0x0142`)
- Arkadaş aracındaki sınıf etiketi: `Mage`

### Archer ID 522

- Gerçek TSkill adı: `Shadow Thunderstorm - Evocation`
- 4Saga `TSkill.tcd` kayıt indeksi: 212
- Skill ID: 522 (`0x020A`)
- Arkadaş aracındaki sınıf etiketi: `Archer`

`Priest/Mage/Archer` adları arkadaş uygulamasındaki değişken adlarıdır; mevcut veri tablosundaki skill adlarıyla tutarlı bir sınıf eşlemesi oldukları ayrıca kanıtlanmamıştır. Bu nedenle sınıf etiketi ile gerçek skill kimliği birbirinden ayrılmalıdır.

### Object/record type

Üç eşleşme de TClient'in çalışma zamanındaki **TSkill kayıtlarıdır**. Native kod adresi, function/action kaydı veya instruction dizisi değildir.

### Evidence

1. AOB'lerin ilk alanları, 4Saga `TSkill.tcd` içindeki gerçek kayıtların `m_wID` değerleriyle bire bir eşleşir.
2. Aynı kayıtlardaki sonraki scalar alanlar AOB'nin sabit ve wildcard bölümleriyle yapısal olarak uyuşur.
3. 4Saga'nın tüm 796 TSkill kaydı, 4Unity'de kanıtlanan ad + `0xA6` gövde formatıyla eksiksiz ayrıştırılır.
4. TClient xref'leri aynı runtime nesnesindeki range, delay ve SFX üyelerini TSkill davranışı içinde tüketir.
5. AOB'lerin TClient disk imajında sıfır eşleşme vermesi ve tüm process address space içinde aranması, bunların heap/runtime veri nesneleri olduğu sonucuyla uyumludur.

## STRUCTURE COMPARISON

4Unity karşılaştırma kaydı: Skill 521, `Shadow Thunderstorm`.

| Offset | 4Saga | 4Unity TSkill | Same semantic field? |
|---:|---|---|---|
| `+0x00` | `uint16 m_wID`: 322 / 425 / 522 | `uint16 m_wID`: 521 | Evet |
| `+0x08` | Runtime ad/string üyesi; adlar yukarıda | Runtime ad/string üyesi; `Shadow Thunderstorm` | Evet; absolute pointer değeri oturuma özgüdür |
| `+0x18` | `dword`: üç kayıtta 63 | `dword`: 16 | Yapısal konum/tür aynı; kesin sembolik ad bu çalışmada kanıtlanmadı |
| `+0x48` | `float m_fAtkRange`: 0.0 | `float m_fAtkRange`: 12.0 | Evet |
| `+0x50` | `float m_fMinRange`: 0.0 | `float m_fMinRange`: 0.0 | Evet |
| `+0x54` | `float m_fMaxRange`: 1.0 | `float m_fMaxRange`: 22.0 | Evet |
| `+0x60` | `dword`: üç kayıtta 1000 | `dword`: 18000 | Evet; base reuse/cooldown delay, ms |
| `+0x8C` | `dword`: ID 322→314, 425→324, 522→328 | `dword`: 0 | Evet; `m_dwSFX[TSKILLSFX_DEFEND]` |

Serileştirilmiş dosyada ad, scalar gövdeden önce ayrı tutulur; runtime `+0x08` bu adın nesne/string temsilidir. Bu nedenle disk offset'i ile runtime pointer değeri bire bir karşılaştırılmamalıdır.

## FIELD +0x60

### Observed values

- Skill 322: 1000
- Skill 425: 1000
- Skill 522: 1000
- 4Unity Skill 521: 18000

4Saga tablosunun genel dağılımında 600, 800, 1000, 1200, 5000, 9000, 18000, 40000, 600000, 720000, 960000 ve 1200000 gibi zaman ölçekli değerler bulunur. Örnek olarak normal Attack 600, Shoot 1200, Mobile Shot 800, Magic Attack 1200 ve Back to Village 1200000 değerini taşır.

### Readers/xrefs

- Eski/homolog kaynakta `CTClientSkill::GetReuseTick`, bu üyeyi temel gecikme olarak kullanır: temel delay'e seviye başına artışı ekler ve saldırı gecikme değiştiricilerini uygular.
- Sunucu tarafındaki homolog `CTSkill::GetReuseDelay` aynı kavramı `m_dwReuseDelay` olarak tüketir.
- Güncel 4Unity TClient'te `TClient.exe+A4DD2` çevresindeki TSkill yolu `+0x60` değerini Skill ID ve `+0x48/+0x50/+0x54` range üyeleriyle beraber okur; `+A4E49..+A4E4C` aralığında outbound `0x5031` işlemine serileştirir.
- 4Saga kayıt grameri ve alan sırası 4Unity ile homologdur; veri dağılımı da milisaniye cinsinden reuse/cooldown gecikmesiyle uyumludur.

### Dataflow

TSkill kayıt alanı → reuse/cooldown hesaplama veya skill-operation serileştirmesi → istemcinin normal skill yürütümü. Alan bir float değildir; range geometrisi alanları olan `+0x48/+0x50/+0x54` yoluna dahil değildir.

Arkadaş aracının `+0x60 = 1000` yazması, bu temel gecikmeyi 1000 ms'ye zorlamayı amaçlar. Ancak incelenen güncel 4Saga veri tablosunda üç hedef kaydın özgün değeri zaten 1000'dir. Dolayısıyla bu kesin build üzerinde üç yazının tamamı **değer bakımından no-op** durumundadır. Farklı bir sürümde özgün değer daha yüksekse tekrar kullanım/cooldown zamanını kısaltabilir; execution count, radius veya damage'i doğrudan artırmaz.

### Most precise evidence-backed semantic description

Milisaniye cinsinden TSkill temel reuse/cooldown gecikmesi (base reuse delay).

### Confidence

**Yüksek.** Kayıt homologluğu, eski kaynak dataflow'u, güncel istemci serileştirme xref'i ve bütün tablo değer dağılımı aynı yorumu destekler. Güncel 4Saga TClient içinde bu üyeye sembolik C++ ad veren debug bilgisi bulunmadığından ad, tek bir sembolden değil birleşik kanıttan türetilmiştir.

## FIELD +0x8C

### Observed values

Özgün 4Saga `TSkill.tcd` değerleri:

- Skill 322 `Rain of Arrows - User Dependent`: 314
- Skill 425 `Ice Rain - User Dependent`: 324
- Skill 522 `Shadow Thunderstorm - Evocation`: 328

Arkadaş aracı değişken eşleşmesine göre şunları yazar:

- `Priest`/Skill 425: 328 (özgün 324 yerine)
- `Mage`/Skill 322: 324 (özgün 314 yerine)
- `Archer`/Skill 522: 314 (özgün 328 yerine)

Yani üç TSFX referansını kayıtlar arasında yeniden eşler.

### Readers/xrefs

Güncel 4Unity TClient'teki kesin TSkill xref'i:

- `TClient.exe+7F2DD6`: TSkill `+0xA5` range type değerini `CIRCLE (2)` ile karşılaştırır.
- `TClient.exe+7F2DE3`: `dword [TSkill+0x8C]` değerini yükler.
- Değer sıfır değilse `TClient.exe+A6D5B0` çağrısıyla TSFX template tablosunda lookup yapar.
- Dönen template geçerliyse `0x230` byte'lık SFX nesnesi oluşturur, hedef/zemin XYZ'sini `+0x68/+0x6C/+0x70` alanlarına koyar, efekti başlatır ve kaydeder.

Eski/homolog kaynak aynı akışı açık adlarla gösterir: CIRCLE range type için `m_dwSFX[TSKILLSFX_DEFEND]` kontrol edilir, `CTChart::FindTSFXTEMP(...)` çağrılır, `CTachyonSFX` oluşturulur ve skill'in ground koordinatında oynatılır.

4Saga `TSFX.tcd` ayrıca bağımsız tablo kanıtı sağlar. Dosya 257 adet sabit 12-byte TSFX kaydı içerir ve 314, 324, 328 değerlerinin üçü de geçerli TSFX kayıt kimliğidir:

- TSFX 314 → resource alanı 74139627
- TSFX 324 → resource alanı 3014844
- TSFX 328 → resource alanı 3014867

### Dataflow

`TSkill+0x8C` → TSFX ID/key → TSFX template lookup → CIRCLE skill için ground/defend/impact görsel efekt nesnesi → normal skill yürütümü.

Bu alan linked skill/action seçmez; timing, radius, damage veya execution count değildir. Arkadaş aracının yazıları, üç hedef skill'in yerde gösterilen defend/impact/AOE görsel efekt şablonlarını birbirleri arasında değiştirir. Görünür sonuç efekt görünümü/asset seçimi olabilir; mekanik AOE kapsamı veya kaç defa çalıştığı bu alandan çıkarılamaz ve xref bunu desteklemez.

### Most precise evidence-backed semantic description

TSkill'in `m_dwSFX[TSKILLSFX_DEFEND]` üyesi: CIRCLE/zemin hedefli skill için defend/impact/ground-area görsel efekt template kimliği.

### Confidence

**Çok yüksek.** Alanın TSkill içindeki sıralı SFX üyelerinden biri olması, doğrudan TSFX lookup dataflow'u, eski kaynaktaki sembolik üye adı ve 4Saga `TSFX.tcd` içindeki geçerli 314/324/328 anahtarları birbirini doğrular.

## ID 425 / 322 / 522

### Meaning

Üçü de **Skill ID**'dir; function ID, action ID veya yalnızca rastlantısal genel record ID değildir.

### Names if recovered

- 425: `Ice Rain - User Dependent`
- 322: `Rain of Arrows - User Dependent`
- 522: `Shadow Thunderstorm - Evocation`

### Evidence

- Kimlikler, doğrulanmış `TSkill.tcd` kayıtlarının ilk `uint16` alanıdır.
- Aynı kayıtlar tam TSkill grameriyle ayrıştırılır ve beklenen range/delay/SFX alanlarını taşır.
- TClient tüketicileri bu nesneleri skill range, reuse ve effect yollarında kullanır.
- Aynı sayısal değerlerin başka tablolarda bulunabilmesi tek başına kimlik türünü değiştirmez; belirleyici olan AOB'nin eşleştiği bütün kayıt yapısı ve xref dataflow'udur.

## FRIEND AOE ACTUAL MECHANISM

1. Arkadaş uygulaması tüm process address space içinde üç TSkill runtime kayıt imzasını arar.
2. Her sonuç, kod RVA'sı değil dinamik TSkill nesnesinin başlangıç adresidir.
3. `aoehigh=true` yolunda üç kaydın `+0x60` integer alanına 1000 yazar. Mevcut doğrulanmış build'in on-disk özgün değerleri zaten 1000 olduğundan bu build'de etkisizdir.
4. Diğer AOE yolunda `+0x8C` TSFX ID alanlarına sınıf etiketi sırasıyla 328/324/314 yazar. Mevcut kayıtlarda bu, üç ground/impact SFX seçiminin yeniden eşlenmesidir.
5. Sonrasında TClient normal skill execution akışını sürdürür; helper bir native execution instruction'ını replay etmez ve execution sayacı yazmaz.

Kanıtlanan sınıflandırma:

- **B — cooldown/timing:** `+0x60` alanının kavramsal etkisi.
- **E — skill configuration record:** Her iki yazı da TSkill runtime konfigürasyon kaydınadır.
- `+0x8C` için ayrıca görsel/presentation TSFX seçimi.
- **A değil:** execution count değiştirilmez.
- **C değil:** linked operation/function/skill seçimi değildir.
- **D değil:** geometry/range alanları `+0x48/+0x50/+0x54` olup yazılmıyor.

## COMPARISON WITH 4UNITY AOE

### 4Unity

Bir doğal `0x0209` başlangıç işlemi → execution chain → RIP'i `N-1` kez replay → toplam `N` AOE yürütümü. Mekanizma doğrudan tekrar/execution count üretir.

### 4Saga

Runtime TSkill kayıt lookup'u → `+0x60` delay veya `+0x8C` TSFX field write → TClient'in normal yürütümü. Mevcut build'de delay yazıları no-op; pratik değişiklik TSFX yeniden eşlemesidir.

### Shared subsystem if any

Ortak nokta yalnızca skill/TSkill konfigürasyon katmanıdır. Aynı execution-count veya RIP-replay alt sistemi kullanılmaz. Arkadaş aracının yolu, 4Unity'nin çalışan repetition mekanizmasının mimari eşdeğeri değildir.

## PREVIOUS REPORT CORRECTION

Önceki, plaintext AOB'ler çözülmeden yapılan “altı native patch location” yorumu yanlıştı. Üç AOB opcode/talimat imzası değildir; TSkill runtime veri kayıtlarının kısmi byte düzenidir. `anchor+0x60` ve `anchor+0x8C` de instruction adresleri değil, bu kayıtların iki integer veri üyesidir.

Dolayısıyla altı işlem:

- native code patch değildir,
- original instruction byte/restore çifti değildir,
- runtime TSkill record field write işlemidir.

Eski rapor tarihçe için korunmuştur; bu belge düzeltilmiş sonuçtur.

## LIVE READ-ONLY TEST NEEDED?

**NO.** Nesne türü, üç kimliğin anlamı ve iki alanın semantiği; doğrulanmış TSkill/TSFX tabloları, eksiksiz kayıt ayrıştırması ve TClient xref/dataflow kanıtıyla belirlenmiştir. Canlı tarama yalnızca belirli bir oturumdaki heap adreslerini ve o anda bellekte bulunan değerleri gösterebilir; semantik sonuç için eksik kanıt değildir.

Ayrıca kontrol anında çalışan `TClient.exe` yolu `C:\Games\4Unity\TClient.exe` olarak çözüldü; 4Saga istemcisi çalışmadığı için 4Saga canlı taraması yapılmadı. Hiçbir istemciye attach olunmadı ve süreç belleği okunmadı/yazılmadı.

## MODIFICATIONS

**NONE.** Kod, oyun dosyası, süreç belleği ve mevcut 4Unity AOE implementation'ı değiştirilmedi. Yalnız bu salt-okunur analiz raporu oluşturuldu.

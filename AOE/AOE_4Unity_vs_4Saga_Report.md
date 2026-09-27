# 4Unity AOE ve 4Saga AOE Teknik Karşılaştırma Raporu

Tarih: 2026-09-25  
Kapsam: yalnız AOE sistemleri; salt-okunur statik/source analizi. TClient süreçlerine attach/hook/write yapılmadı, paket değiştirilmedi ve mevcut 4Unity kaynakları değiştirilmedi.

## INPUT FILES

| Rol | Kesin yol | SHA-256 |
|---|---|---|
| 4Unity source | `C:\Users\Public\Documents\4UnityAOEManager` | dizin |
| 4Unity helper | `C:\Users\Public\Documents\4UnityAOEManager\build\Release\4UnityAOEManager.exe` | `CA24F4F542780EFA1B0D704FD2018A322E5B5DD154889E8CA80E8F9690529A50` |
| 4Unity TClient | `C:\Games\4Unity\TClient.exe` | `9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28` |
| 4Saga helper | `C:\Users\xaofx\Downloads\UnConfuserEx-main\UnConfuserEx-main\4Saga.exe` | `D4B05B2A03F7B1A491EB5FE155B1F95D09D75C4F661275F1E10A359D769B9879` |
| 4Saga clean metadata | `C:\Users\xaofx\Downloads\UnConfuserEx-main\UnConfuserEx-main\4SAGA.clean.exe` | `8111161101F215921DBA9F87449E813D72E24B0E731B0A4D675AD533061CAD0E` |
| 4Saga mapped memory image | `C:\Users\xaofx\Downloads\UnConfuserEx-main\UnConfuserEx-main\4SAGA.memory.safe.bin` | `5D89CFB820DC0FB2541A30C5F550E2C6058D665D671DA231F0116CC6FCF29D44` |
| 4Saga AOE MoveNext | `C:\Users\xaofx\Downloads\UnConfuserEx-main\UnConfuserEx-main\AOE_MoveNext_RVA_18BF48_SAFE.bin` | `B0695E46B3A4CA177D8EB105E4E12F6AA3CFBEAA0D6D8A3085117411117DA0FB` |
| 4Saga Memory.dll | `C:\Users\xaofx\Downloads\UnConfuserEx-main\UnConfuserEx-main\4Saga\Memory.dll` | `EA6DC302B177DCD3974599E9512FE3A8994E60492CDE0DBC058E9345638F4434` |
| 4Saga TClient | `C:\Program Files\4Saga Official\TClient.exe` | `CD8FDA20BB99A6689DB99FCFE8B568DF027A0900927B797D8D5AB2FA3CC00EC7` |

Seçilen clean varyantı `4SAGA.clean.exe`'dir; aynı dizinde `(1)` varyantı yoktur. AOE dışındaki MoveNext dump'ları kullanılmadı.

## 4UNITY AOE

### Trigger ve repeat mekanizması

Yetkili kaynak akışı `src/main.cpp` → `src/tracer.cpp` → `src/initial_2x.cpp` şeklindedir. Kullanıcı sayacı `MinInitialCalls=1`, `MaxInitialCalls=100`, varsayılan `2` olarak doğrulanır. İlk doğal `0x0209` çağrısı kabul edilir. Aynı thread'deki dönüşte RSP ile R12/R13/R14/R15/RSI/RDI ilk çağrı ile eşleşirse, istenen toplam sayıya ulaşılıncaya kadar thread RIP'i `initialPrepRva`'ya çevrilir. Böylece `N` toplam çağrı için tam `N-1` yönlendirme yapılır. Helper operation ID, hedef listesi veya paket yazmaz.

Bu uygulama remote stub veya oyun içi sayaç kullanmaz. Repeat state helper içindeki `Initial2xExperiment` nesnesindedir: `targetInitialCalls`, `initialCallsObserved`, `redirectsPerformed`, `repeatPending`, `awaitingReplayedCall`. Uygulama breakpointleri kaldırırken sahip olduğu DR slotlarını özgün adres/kontrol değerlerine döndürür ve read-back ile doğrular.

### Güncel fonksiyonlar ve statik imza doğrulaması

`aoe_locator_tests.exe`: **39/39 PASS**. `4UnityAOEManager.exe --aoe-diagnostic --offline`: profil ve tüm zorunlu statik anchorlar **PASS**, gameplay write `0`, debugger attached `NO`.

| Ad | Güncel RVA | AOB özeti | Raw / semantik eşleşme | Amaç |
|---|---:|---|---:|---|
| AoeLifecycleCaller | `0x7F2F9D` | `41 8B 46 24 ... 49 8B CD E8 ?? ?? ?? ?? 90` | 1 / 1 | hazırlanmış operation'ı shared worker'a çağırır |
| AoeFunctionRecordDispatch | `0x7F2EE5` | `80 79 01 06 ... 0F B7 49 06 E8 ...` | 1 / 1 | `+1=6,+2=0x1D,+8=0`, linked WORD `+6`, invocation `1` |
| SharedOperation020A | `0x7BC900` | `41 54 41 55 41 56 41 57 48 81 EC ...` | 1 / 1 | initial/lifecycle operation ortak yürütücüsü |
| TypedActorLookup | `0x7BB3A0` | `85 D2 74 ?? 41 0F B6 C0 ...` | 1 / 1 | ID + type namespace actor çözümü |
| SharedTypedActorCall | `0x7BCA5A` | `8B 10 85 D2 ... E8 ?? ?? ?? ?? 48 8B F0` | 1 / 1 | record ID/type → typed lookup |
| OperationProducer | `0x99150` | `48 89 5C 24 08 ... 4C 8B E9 E8 ...` | 1 / 1 | operation/target/XYZ serileştirme üreticisi |
| ProducerCall | `0x7BCC57` | `66 89 6C 24 40 ... E8 ?? ?? ?? ??` | 1 / 1 | operation WORD ve target vector'u producer'a aktarır |
| RecordSerialization | `0x992DE` | `8B 10 85 D2 ... 40 FE C5` | 1 / 1 | record ID ve type serileştirme |
| TemplateLookup | `0xA6D600` | `4C 8B 05 ?? ... 48 8B 42 28 C3` | 10 / 1 | WORD anahtarından function record payload bulur |
| LinkedAcquisitionPath | `0x7F2F2B` | `4C 8B C8 ... 49 8B CD E8 ?? ?? ?? ??` | 1 / 1 | linked lookup sonucu, invocation `1` |
| AcquisitionType2Insertion | `0x7DA977` | `8B BF 68 07 00 00 ... C6 40 04 02` | 1 / 1 | type-2 hedef kaydı ekler |
| AcquisitionRadiusGuard | `0x7DA935` | `F3 41 0F 10 46 48 0F 2F C1 ...` | 3 / 1 | radius guard; insertion bağıyla tekilleşir |
| TargetVectorBuilder | `0x6CB40` | `48 8D 8F 60 06 00 00 ...` | 1 / 1 | owner+`0x660` target vector |
| ParsedActionBoundary | `0x6CB73` | `66 47 39 74 6F 3C` | 1 / 1 | initial action handler parse sınırı |

Doğrudan call çözümü: `0x7F2F9D -> 0x7BC900`; `0x7BCC57 -> 0x99150`; linked acquisition çağrısı `0x7F2F2B -> 0x7DA420`; type-2 insertion `0x7DA977`. Initial hazırlık `0x7F2F80`, call dönüşü `0x7F2FA2`'dir.

### Action/state ve timing

`0x0209` initial operation olarak `ObserveInitialCall` tarafından zorunlu tutulur. `0x020A` producer/live-validation tarafında periodic/lifecycle operation olarak sayılır. `0x020B` mevcut Initial-Nx yürütme kararında kullanılmaz; tarihsel linked observation ailesindedir. Bunlar skill ID olarak yorumlanmamıştır.

Yaklaşık 997–1000 ms, doğal `0x020A` lifecycle gözlemidir. **Initial Nx tekrarının timer'ı değildir**: kaynakta tekrar için Sleep/Delay/timer yoktur; dönüş breakpointinde hemen `RIP=base+0x7F2F80` yapılır. Kaynakta ayrı, deneysel bir `1000 -> 100 ms` tick-patch modülü bulunsa da son çalışan Initial Nx yolu bunu çağırmaz.

## 4SAGA AOE

### `AoEHackBtn.MoveNext` IL gövdesi

Metadata: `StoryMultihackV2.full/<AoEHackBtn>d__151::MoveNext`, token `0x06000442`, runtime RVA `0x18BF48`.

- Fat header: flags/size `0x301B`, header `12` byte
- MaxStack: `30`
- CodeSize: `0x574`
- LocalVarSig: `0x110000F8`
- Üç async `AoBScan(0, Int64.MaxValue, pattern, false, true, true, "")`
- Her sonuçta `Enumerable.FirstOrDefault<Int64>`; sonuçlar `aoe_priest` (`0x04000378`), `aoe_mage` (`0x04000379`), `aoe_archer` (`0x0400037A`) alanlarına yazılır
- `aoesearch` ilk başarılı arama akışında `true` yapılır; `aoehigh` düğme durumunda terslenir
- Arama async state machine state'leri `0/1/2` ve `TaskAwaiter<IEnumerable<Int64>>` ile yürür; uygulamaya ait repeat loop/count yoktur

Pattern stringleri üç decryptor çağrısıyla alınır (şifreli anahtarlar `0x9C19F98E`, `0xD761D162`, `0x33E0B153`). String getter'lar module static `Byte[]` nesnesini kullanır. Sağlanan `memory.safe.bin` yalnız mapped modül image'ıdır; module ctor'un GC heap'te ürettiği `Byte[]` nesnesi bu dosyada yoktur. `4SAGA.clean.exe` de eksik-RVA methodlar nedeniyle CLR tarafından yüklenemedi. Bu yüzden üç plaintext AOB'yi güvenilir biçimde üretmeden tahmin edilmedi.

### Asıl patch akışı

AOE alanlarının ikinci ve tek diğer tüketicisi, token `0x060003C8`, runtime RVA `0x184248` olan `StoryMultihackV2.full::InviCheckTimer_Tick` metodudur. Adı başka bir özelliği çağrıştırsa da burada yalnız AOE alanlarına temas eden bloklar incelendi.

Bu periyodik handler `aoehigh` durumuna göre üç bulunan base adresin her birinde iki noktaya `Memory.Mem.WriteMemory` eşdeğeri proxy çağrısı yapar:

| Sınıf anchor'u | Patch adresleri |
|---|---|
| `aoe_priest` | `match + 0x60`, `match + 0x8C` |
| `aoe_mage` | `match + 0x60`, `match + 0x8C` |
| `aoe_archer` | `match + 0x60`, `match + 0x8C` |

Toplam patch yüzeyi: üç scan anchor'u ve altı doğrudan native byte-write noktası. IL'de `Task.Delay`, `Sleep`, AOE repeat sayacı veya AOE execution fonksiyonunu N kez çağıran loop yoktur. Restore/original değerler de aynı timer handler'da karşı dal üzerinden yazılır; ancak plaintext write type/value sabitleri heap dışı şifreli constant blob nedeniyle kanıtlanamadığından raporda uydurulmamıştır.

### Güncel 4Saga TClient eşleşmesi

Plaintext historical AOB'ler güvenilir olarak çözülemediği için güncel `C:\Program Files\4Saga Official\TClient.exe` üzerinde semantic wildcard taraması için gerekli başlangıç patternleri mevcut değildir. Bu nedenle:

- Historical pattern: **NOT RECOVERED**
- Current robust pattern: **UNRESOLVED**
- Match count / current RVA: **NOT PROVEN**
- Confidence: exact current match için **LOW / unresolved**

Bu sınırlama mekanizma sınıflandırmasını değiştirmez: IL'nin üç `AoBScan` + altı `WriteMemory` adres hesabı doğrudan kanıttır; yalnız native hedef RVAları belirlenememiştir.

4Saga modeli: **MODEL E — birden fazla farklı patch'in birleşimi**. Daha ayrıntılı tanımı: üç class-specific native anchor, her anchor'da `+0x60/+0x8C` çift patch ve timer tabanlı enable/disable/restore uygulaması. Model A değildir; helper'da AOE execution function tekrar çağrısı yoktur. Model B'deki bir repeat-count alanı da yoktur. Model C/D ile aynı olduğu kanıtlanmamıştır.

## EXECUTION GRAPH

### 4Unity

Player AOE trigger  
→ parsed initial handler / target-vector hazırlığı (`0x6C830`, `0x6CB40`)  
→ function-record / linked acquisition (`0x7F2EE5`, `0x7F2F2B`)  
→ initial preparation `0x7F2F80`  
→ lifecycle call `0x7F2F9D` → shared worker `0x7BC900`  
→ producer `0x99150`  
→ validated return `0x7F2FA2`  
→ helper aynı thread RIP'ini `0x7F2F80`'e `N-1` kez yönlendirir  
→ toplam N initial AOE operation.

### 4Saga

Kullanıcı `AoEHackBtn`  
→ üç full-range async AOB scan  
→ FirstOrDefault adreslerini `aoe_priest/mage/archer` alanlarına sakla  
→ `aoehigh` durumunu değiştir  
→ mevcut periyodik `InviCheckTimer_Tick`  
→ her class anchor'unda `+0x60` ve `+0x8C` adreslerine enable veya restore bytes yaz  
→ TClient'in patchlenmiş kendi native akışı AOE davranışını üretir.

## ACTION VALUE COMPARISON

| Değer | 4Unity | 4Saga |
|---|---|---|
| `0x0209` | Initial operation; Initial Nx kabul koşulu ve tekrar oynatılan operation | AOE IL veya çözülebilen patch akışında ilişki **NOT FOUND / NOT PROVEN** |
| `0x020A` | Doğal periodic/lifecycle operation; live validation ve producer observation | AOE IL/patch akışında ilişki **NOT FOUND / NOT PROVEN** |
| `0x020B` | Tarihsel linked family; mevcut Nx kararında kullanılmıyor | AOE IL/patch akışında ilişki **NOT FOUND / NOT PROVEN** |

Her iki TClient dosyasında bu WORD değerlerinin ham byte dizileri çok sayıda bulunur (4Unity: 273/267/135; 4Saga: 255/230/99). Disassembly'de doğrudan immediate operand xref'i çıkmadı. Dolayısıyla ham byte sayıları AOE xref kanıtı sayılmadı.

## FUNCTION CORRESPONDENCE

| 4Unity function | 4Saga function | Semantik ilişki | Kanıt | Güven |
|---|---|---|---|---|
| `0x7F2F9D` lifecycle caller / `0x7BC900` shared worker | Doğrudan karşılık bulunmadı | 4Unity operation execution katmanı; 4Saga helper bunu çağırmıyor | native call target + IL call yokluğu | Yüksek |
| `0x7F2EE5` function-record dispatch | Doğrudan karşılık bulunmadı | 4Unity linked action dispatch | record koşulları; 4Saga AOE IL'de eşdeğer state/action yok | Orta-yüksek |
| `0x99150` producer | Doğrudan karşılık bulunmadı | 4Unity operation serialization | producer signature/call | Yüksek |
| N/A | `AoEHackBtn.MoveNext` RVA `0x18BF48` | üç native patch anchor'u keşfi | gerçek decrypted IL | Yüksek |
| N/A | `InviCheckTimer_Tick` RVA `0x184248` AOE blokları | altı patch'i enable/restore durumuna uygular | mapped-image IL ve alan xrefleri | Yüksek |

## IMPLEMENTATION DIFFERENCES

| Property | 4Unity | 4Saga |
|---|---|---|
| Discovery method | exact SHA profil + 14 semantik locator anchor'u | 3 async AOB scan |
| AOB count | 14 doğrulanan anchor | 3 AOE scan patterni |
| Hook count | kod hook'u 0; HW execution breakpointleri | hook kanıtı 0 |
| Direct byte patches | Initial Nx yolunda 0 | 6 (`3 x (+0x60,+0x8C)`) |
| Allocated remote stub | yok | yok / kanıt yok |
| Function invocation | doğal çağrı dönüşünde RIP replay | helper tarafından tekrar native function invocation yok |
| Repeat-count storage | helper `targetInitialCalls`, 1–100 | AOE repeat count yok |
| Original-byte restore | DR slot ownership + read-back; code byte patch yok | timer'ın karşı dalında restore değerleri yazılıyor; plaintext bytes unresolved |
| Timing mechanism | Nx için delay yok; doğal `0x020A` cadence ~1 s | mevcut UI timer tick patchleri tekrar uygular; explicit Delay/Sleep yok |
| Action/state dependency | `0x0209` zorunlu, `0x020A` validation/lifecycle | `aoehigh`, üç class address; 0209/020A/020B bağı kanıtlanmadı |
| Linked-action dependency | function-record/linked acquisition yolu kanıtlı | kanıtlanmadı |
| Build resilience | exact SHA + wildcard/semantic validation, fail-closed | plaintext patternler şifreli; FirstOrDefault tekillik doğrulaması yok |
| Current TClient match status | 14/14 semantic PASS | exact current RVA/match count unresolved |

## REPETITION VE TIMING KARŞILAŞTIRMASI

4Unity sayacı doğrudan kullanıcı kontrollü helper değeridir. Remote stub counter değildir; lifecycle field değişimi değildir. Aynı initial execution hazırlığı N-1 kere dönüşten yeniden yürütülür. Kod limiti 100'dür.

4Saga AOE kodunda repeat-count kaynağı yoktur. Üç class-specific native yol iki noktadan patchlenir. Bu nedenle 4Unity'deki “tek initial event → N execution” semantiğini uyguladığı kanıtlanamaz. Timer, patch durumunu periyodik uygulayan mevcut UI timer handler'ıdır; AOE operation'larını sayan veya ~1 saniyelik lifecycle tekrarını üreten bir loop kanıtı yoktur.

## SAME UNDERLYING MECHANISM?

**NO — DIFFERENT AOE MECHANISMS**

4Unity, doğrulanmış `0x0209` initial operation akışında dönüş RIP'ini aynı preparation noktasına N-1 kez yönlendirerek operation execution/serialization zincirini tekrar çalıştırır. 4Saga ise üç class-specific native anchor bulur ve her birinde iki kod/veri noktasını doğrudan patchleyerek motorun davranışını değiştirir. 4Saga helper'da N sayacı, `0x0209` replay'i, shared operation çağrısının tekrarı veya 4Unity function-record/linked dispatch bağı bulunmamıştır.

## IMPORTANT DISCOVERIES

1. 4Saga `AoEHackBtn.MoveNext` tek başına patch yazmaz; üç adresi async olarak keşfeder ve durumu değiştirir.
2. Asıl AOE byte-write işlemleri `InviCheckTimer_Tick` içindeki AOE alan bloklarındadır: üç adres × iki offset.
3. 4Saga AOE, “repeat count” uygulaması değildir; class-specific native patch setidir.
4. 4Unity'nin ~1 saniyelik `0x020A` gözlemi Nx replay timer'ı değildir.
5. Sağlanan mapped image GC heap'i içermediğinden runtime-decrypted string array'i yoktur; exact 4Saga AOB/value metinleri tahmin edilmemiştir.

## REPORT

`C:\Users\Public\Documents\4UnityAOEManager\AOE_4Unity_vs_4Saga_Report.md`

## MODIFICATIONS

NONE — oyun binaryleri, processler, helper executable'ları ve mevcut 4Unity source değiştirilmedi. Yalnız bu istenen rapor oluşturuldu.

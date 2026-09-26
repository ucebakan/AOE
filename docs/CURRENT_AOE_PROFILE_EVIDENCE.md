# Güncel AOE profili — yalnız statik kanıt

İncelenen dosya: `C:\Games\4Unity\TClient.exe`.

SHA-256: `9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28`.

Bu profil mevcut schema 2 formatını kullanır. Bütün RVA'lar bu dosyadaki
talimatlardan ve çağrı/veri akışından çıkarılmıştır; tek bir sabit RVA kayması
varsayılmamıştır. Aşağıdaki HIGH dereceleri statik semantik eşlemeyi anlatır;
canlı AOE, hasar veya sunucu kabulü doğrulaması değildir. Analiz dosya-okuma
ile yapıldı; TClient process belleğine yazılmadı ve debugger bağlanmadı.

## Gerekli anchor'lar

| Profil alanı | 2026-09-20 FB13 profili | Güncel RVA | Güven ve kanıt |
|---|---:|---:|---|
| initialPrepRva | 7F2760 | 7F2F80 | HIGH: `[r14+24]` değerini ve özgün stack argümanlarını hazırlar; R9=RSI, R8=R14, RDX=R15, RCX=R13 |
| initialCallRva | 7F277D | 7F2F9D | HIGH: `E8 5E 99 FC FF` doğrudan 7BC900'a çözümlenir |
| initialReturnRva | 7F2782 | 7F2FA2 | HIGH: önceki 5-byte CALL'ın hemen ardından NOP; sonra özgün epilog |
| sharedWorkerRva | 7BC4C0 | 7BC900 | HIGH: aynı register/prolog, type-resolved record filtering, operation-word ve producer veri akışı |
| producerRva | 99150 | 99150 | HIGH: worker'ın doğrudan çağırdığı 18-arg typed serializer; record ID/type döngüsü |
| producerCallRva | 7BC817 | 7BCC57 | HIGH: `E8 F4 C4 8D FF` doğrudan 99150'ye çözümlenir |
| typedActorLookupRva | 7BAF60 | 7BB3A0 | HIGH: sıfır-ID reddi; type-1 index ve 19-entry jump table; aşağıdaki tree eşlemeleri |
| targetBuilderRva | 6C830 | 6C830 | HIGH: `.pdata` function start; 8-byte ID/type record oluşturma ve owner vector append |
| targetBuilderVectorRva | 6CB40 | 6CB40 | HIGH: `lea rcx,[rdi+660]`; end +8 / capacity +10; 8-byte pointer append |
| differentialParsedRva | 6CB73 | 6CB73 | HIGH: aynı builder'da `66 47 39 74 6F 3C`; executable bölümlerde tek eşleşme |
| idSerializationRva | 992EC | 992EC | HIGH: 992DE `[record+0] -> EDX`; 992F1 CALL -> 9F6980 |
| typeSerializationRva | 992F6 | 992F6 | HIGH: 992E4 `[record+4] -> BL`; 992F9 BL -> EDX; 992FC CALL -> 9F6800 |
| templateLookupRva | A6B9B0 | A6D600 | HIGH: word-key ordered tree; sentinel +19, key +20, payload +28; wrapper/linked/periodic yolların ortak lookup'ı |
| wrapperRva | 7F2810 | 7F3030 | HIGH: template word kontrolü; owner+660 vector; 7F30B0 CALL -> 7F2D60 |
| initialHandlerRva | 6C830 | 6C830 | HIGH: 6CD26 CALL -> wrapper 7F3030; aynı builder/handler |
| dispatchRva | 7D3E3E | 7D449E | HIGH: dispatcher branch'te RCX=RBP,RDX=RSI,R8=RDI; CALL -> handler6C830 |
| linkedActivationRva | 7AA8F0 | 7AAC20 | HIGH: state+1309==4; entity+12E0 -> linked+20; word propagation; A6D600 lookup; 7AAD08 CALL -> 7F3030 |
| periodicUpdateRva | 79DF69 | 79E129 | HIGH: existing update block; record+34 delta accumulator, linked+20 template+60 period compare/subtract; 79E21A CALL -> 7F2D60 |
| monsterCategoryWriteRva | 8417E6 | 842376 | HIGH: constructor-region `mov byte ptr [rbx+7E1],2`; category-write pattern tek eşleşme |
| charCategoryWriteRva | 7807C5 | 7807F5 | HIGH: constructor-region `mov byte ptr [rdi+7E1],1`; tek eşleşme |
| npcCategoryWriteRva | 852B60 | 8536F0 | HIGH: constructor-region `mov byte ptr [rdi+7E1],3`; tek eşleşme |
| recallCategoryWriteRva | 8740BD | 874C72 | HIGH: constructor-region `mov byte ptr [rbx+7E1],7`; tek eşleşme |

Kategori isimleri önceki schema'nın isimleridir. Buradaki doğrudan kanıt,
ilgili actor-category alanına aynı sabitleri yazan constructor instruction'larıdır.

## Function boundary uyarısı

MSVC chained unwind parçaları ayrı işlevler gibi değerlendirilmedi.
7BCC57'deki producer call'ın `.pdata`/UNW_FLAG_CHAININFO zinciri:

`7BCB2C -> 7BCB05 -> 7BC9A9 -> 7BC985 -> 7BC900`.

Linked activation içindeki 7AAC9D bloğu:
`7AAC9D -> 7AAC3A -> 7AAC20`.

Periodic block 79E129, function başlangıcı değil;
`79E129 -> 79DC11 -> 79DBE0` chained unwind zincirindeki bloktur.
Önceki profil de bu alanı periodic-update block olarak saklıyordu.

## Gerekli layout alanları

| Alan | Değer | Güven ve güncel talimat kanıtı |
|---|---:|---|
| targetVectorOffset | 660 | HIGH: 6CB40 append; 7F3098 ve 79E202 aynı owner vector'u hazırlığa geçirir |
| targetRecordBytes | 8 | HIGH: 6CB11 allocate8; record+0 uint32 ID, +4 uint8 type; pointer vector adımı8 |
| targetMaximumEntries | 32 | HIGH: 7F2DBF..7F2DD0 vector span>>3, `cmp rax,20h`, fazlasını reddeder; worker/producer ayrı üst sınırı64 olsa da mevcut prep sınırı32 korunur |
| actorIdOffset | 768 | HIGH: 7BC98D local ID; 7BCBD1 producer R8D; 7BB4E7 type1 direct lookup |
| actorTypeOffset | 7E1 | HIGH: 7BC9D6/7BCBC6 worker/producer category akışı; yukarıdaki constructor writes |
| actorEligibilityOffset | 7CA | HIGH: 7BCA6F type-resolved actor filter |
| actorStatusOffset | 7E4 | HIGH: 7BCA95 aynı actor filter |
| localActorOffset | 2710 | HIGH: 7BC96D/7BC97B worker guard/load; 7BB4E0 type1 lookup |
| type1TreeOffset | 1140 | HIGH: 7BB3F4 jump table type1 -> 7BB3DA -> 7BB4E0; +2710 local-ID kontrolü sonrası 7BB4EF load |
| type2TreeOffset | 1120 | HIGH: type2 -> 7BB3D5 -> 7BB350 load |
| type7TreeOffset | 11B0 | HIGH: type7 -> 7BB3CB -> 7BB5D0 load |
| type9TreeOffset | 1130 | HIGH: type9 -> 7BB3DF -> 7BC5D0 load |
| type10TreeOffset | C6FF00 | HIGH: type10 -> 7BB3E4 -> 7BAE70 load; bu büyük offset güncel talimattan okundu, kopyalanmadı |
| type11TreeOffset | 11A0 | HIGH: type11 -> 7BB3D0 -> 7BAE20 load |
| type17TreeOffset | 1150 | HIGH: type17 -> 7BB3E9 -> 7BC580 load |
| type19TreeOffset | 11E0 | HIGH: type19 -> 7BB3C6 -> 7BC4A0 load |

## Fingerprint'ler ve invocation ayrımı

Profildeki 11 exact fingerprint'in tamamı güncel dosyadan okunmuştur. Exact
SHA profiline ait CALL rel32 byte'ları burada bilerek exact'tır; keşif için
kullanılan masked AOB ile runtime bütünlük fingerprint'i farklı görevlerdir.
Initial CALL -> shared worker ve producer CALL -> producer ilişkileri parser/
runtime validation tarafından ayrıca decode edilmelidir.

AOE-specific function-record dispatch bu dosyada 7F2EE5'te:
`+1 == 6`, `+2 == 1Dh`, `word +8 == 0`, linked-key `word +6`.
7F2EFC CALL -> A6D600. 7F2F0E `[rsp+28]=1`;
7F2F2B CALL -> 7DA420 (linked acquisition). Normal lifecycle/shared path'te
EDI sıfırlanmıştır (7F2EB5/7F2F34); 7F2F88 `[rsp+28]=EDI=0`;
7F2F9D CALL -> 7BC900. Böylece mevcut farklı çağrı argümanları korunur.
Helper invocation değerlerini yeniden üretmez; özgün preparation bloğunu
yeniden yürütür. Operation ID'lerinin runtime'da 0209/020A/020B olduğunu
bu salt statik kontrol yeniden gözlemlemiş değildir.

## Kapsam ve fail-closed

`initialNxAvailable=true` statik profil kapasitesidir; `liveValidationRequired=true`
korunur. Mevcut aynı-session prep/call/return, initial0209 register invariant
ve periodic020A live-validation kapısı geçilmeden replay ARM edilmez.
`aoeLocatorRequired=true`, bu yeni profil için ayrıca semantik AOB/çağrı ilişkisi
doğrulamasını zorunlu tutar; exact fingerprint kontrollerinin yerine geçmez.

`targetInspectorAvailable=false`: bu görev research/provenance onarımı değildir.
Actor position/world-link, selected-target writer/readers, global context pointer
gibi opsiyonel alanlar eski build'den kopyalanmadı. Eski SHA profilleri değiştirilmedi.
Tick100 ve BudgetWrite modları bu profil onarımında yeniden etkinleştirilmedi.

Canlı diagnostic, ARM, repetition veya gameplay yazısı bu profil hazırlığı
sırasında çalıştırılmadı. Sunucu kabulü/hasar sayısı hakkında sonuç çıkarılamaz.

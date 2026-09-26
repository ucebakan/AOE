# AOE locator onarımı — 2026-09-25

## CURRENT TCLIENT

- Path: C:\Games\4Unity\TClient.exe
- SHA256: 9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28
- Dosya boyutu: 15.408.128 byte
- Architecture: AMD64 / PE32+
- PE timestamp: 0x6AB39C06 — 2026-09-23 09:29:42 UTC
- Preferred image base: 0x140000000
- SizeOfImage: 0xF48000
- Salt-okunur canlı tanı: PID 12128, creation FILETIME 134347632899876918, base 0x7FF76DF30000.
- Bütün runtime adresleri doğrulanmış oturumun module base + RVA hesabıdır.

## WHY OLD AOE BROKE

İstenen repetition motoru tarihî MultiTargetResearch dizininde değil,
mevcut 4UnityAOEManager projesinde bulundu. MultiTargetResearch gözlem/template
editörüdür; oradaki repeat_count breakpoint-storm koruma sayacıdır.

Manager SelectBuildProfile, SHA adlı JSON olmadığı için yeni 9CD77... build'i
imza taramasından önce UNKNOWN BUILD / PROFILE RECOVERY REQUIRED ile reddediyordu.
Eski signatures JSON yürütülen bir runtime tarayıcı değildi. Son FB13 profilinin
11 fingerprint'inden 8'i eski RVAlarda güncel byte'larla uyuşmuyordu; producer,
vector builder ve parsed boundary aynı kalmıştı. Serializer RVAları aynı kalsa
da içlerindeki CALL rel32 byte'ları değişmişti.

Eski AOB kataloğu tarama sonucu:

| İmza | Beklenen semantik sonuç | Güncel ham eşleşme | Sınıf |
|---|---:|---:|---|
| initialSharedCall | 1 | 1 | D: mask korundu, çağrı RVA/hedefi taşındı |
| sharedWorker | 1 | 4 | B: tek başına belirsiz |
| typedActorLookup | 1 | 1 | D: mask korundu, RVA taşındı |
| targetVectorBuilder | 1 | 1 | D: aynı yapısal nokta |
| producer | 1 | 975 | B: kısa genel prolog |
| templateLookup | 1 | 48 | B: aynı tree-lookup ailesi |

Hiçbir locator İlkEşleşme/FirstOrDefault kullanmaz. Eski pattern metinleri
signatures/aoe_signatures.json içindeki legacyAnchorPatterns alanında korunur.
Keşif maskeleri ile exact-SHA runtime fingerprint'leri farklıdır: keşifte
rel32/RIP displacement maskelenir; canlı bütünlükte wildcard byte'ları da
dahil güncel dosyadan alınmış gerçek byte'ların tamamı karşılaştırılır.

## OLD → CURRENT RVA MAP

| Verilen/eski RVA | Güncel RVA | Amaç | Güven / kanıt |
|---|---|---|---|
| 7F0900 | 7F2F80 | Initial Nx preparation | HIGH; aynı stack/register hazırlığı |
| 7F091D | 7F2F9D | Shared lifecycle CALL | HIGH; E8 -> 7BC900 |
| 7F0922 | 7F2FA2 | Shared CALL dönüşü | HIGH; CALL+5 NOP ve epilog |
| 7BB160 | 7BC900 | Shared operation worker | HIGH; typed record filter + operation WORD + producer |
| 7F2EE5..7F2F2B | 7F2EE5..7F2F2B | Verilen AOE dispatch anchor'ları | HIGH; güncel dosyada yeniden doğrulanan +1/+2/+8/+6 koşulları |
| 7F2F9D | 7F2F9D | Verilen downstream shared CALL | HIGH; güncel dosyada aynı anlamda doğrulandı |
| 7F2FC0 | 7F2FC0 | Verilen downstream typed-record döngüsü | HIGH; record ID/type -> typed resolver |
| 7DA617 | 7DA977 | Type-2 actor record insertion | HIGH; ID+768, allocate8, record+4=2 |
| 7BC907 | 7BCC57 | Serializer handoff | HIGH; operation WORD/vector/XYZ argümanları -> 99150 |
| 7F2B4B | 7F2F2B | Linked acquisition CALL, R9 template | HIGH; lookup sonucu R9, invocation=1 -> 7DA420 |

Verilen anchor listelerine tek bir RVA kayması uygulanmadı; aynı kalmış görünen
adresler de güncel talimat/veri akışıyla yeniden kontrol edildi. Son FB13
profilinden güncele 22 alanın ayrıntılı eşlemesi CURRENT_AOE_PROFILE_EVIDENCE.md'dedir.

7BC900'a güncel, instruction-boundary doğrulanmış doğrudan çağrılar:
81104, 798FAB, 7AAAF7, 7C86FD, 7DAF28, 7F2F9D, 8596A9.
Her tarihî çağrıya yalnız sıralamasına bakarak bire bir isim ataması yapılmadı.

## REPAIRED SIGNATURES

Çalışan tanımlar src/aoe_locator.cpp'dedir; signatures/aoe_signatures.json aynı
pattern/semantik sonuçları belgeleyen katalogdur. 14/14 zorunlu sonuç PASS.
TemplateLookup 10 ham adaydan, AcquisitionRadiusGuard 3 ham adaydan tam bir
geçerli eşleşmeye düşer; diğerleri ham olarak da tekildir.

### AoeLifecycleCaller

Pattern: `41 8B 46 24 89 44 24 30 89 7C 24 28 4C 89 64 24 20 4C 8B CE 4D 8B C6 49 8B D7 49 8B CD E8 ?? ?? ?? ?? 90`

Güncel RVA: `0x7F2F9D`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: R9=RSI; RCX/RDX/R8 preserved prep; direct CALL; NOP and stack-unwind epilogue; semantic_matches=1

### AoeFunctionRecordDispatch

Pattern: `80 79 01 06 75 ?? 80 79 02 1D 75 ?? 66 83 79 08 00 75 ?? 0F B7 49 06 E8 ?? ?? ?? ?? 48 85 C0 74 ?? 41 8B 4E 24 89 4C 24 30 C7 44 24 28 01 00 00 00 89 7C 24 20 4C 8B C8 4D 8B C6 48 8B 94 24 C0 00 00 00 49 8B CD E8 ?? ?? ?? ??`

Güncel RVA: `0x7F2EE5`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: record+1=6, +2=1D, +8=0; linked WORD+6; common reject branch; invocation=1; semantic_matches=1

### SharedOperation020A

Pattern: `41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 C0 00 00 00 4C 8B AC 24 A0 01 00 00 4D 8B F1`

Güncel RVA: `0x7BC900`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Unique independently patterned direct target of lifecycle call; operation pointer R9 retained; semantic_matches=1

### TypedActorLookup

Pattern: `85 D2 74 ?? 41 0F B6 C0 FF C8 83 F8 12 77 ?? 4C 8D 0D ?? ?? ?? ?? 48 98 45 8B 84 81 ?? ?? ?? ?? 4D 03 C1 41 FF E0`

Güncel RVA: `0x7BB3A0`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Zero ID rejected; R8B namespace jump table; semantic_matches=1

### SharedTypedActorCall

Pattern: `8B 10 85 D2 74 ?? 44 0F B6 40 04 45 84 C0 74 ?? 49 8B CC E8 ?? ?? ?? ?? 48 8B F0`

Güncel RVA: `0x7BCA5A`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Shared worker record+0 ID, record+4 type -> independent typed resolver; semantic_matches=1

### OperationProducer

Pattern: `48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC ?? 41 0F B6 E9 45 8B F8 44 8B E2 4C 8B E9 E8 ?? ?? ?? ??`

Güncel RVA: `0x99150`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Independent producer prologue and parameter register flow; semantic_matches=1

### ProducerCall

Pattern: `66 89 6C 24 40 4C 89 6C 24 38 F3 44 0F 11 4C 24 30 F3 44 0F 11 54 24 28 F3 44 0F 11 5C 24 20 E8 ?? ?? ?? ??`

Güncel RVA: `0x7BCC57`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Operation WORD argument9, vector argument8, XYZ stack handoff -> producer; semantic_matches=1

### RecordSerialization

Pattern: `8B 10 85 D2 74 ?? 0F B6 58 04 84 DB 74 ?? 48 8D 4C 24 28 E8 ?? ?? ?? ?? 48 8B C8 0F B6 D3 E8 ?? ?? ?? ?? 40 FE C5`

Güncel RVA: `0x992DE`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Record+0 ID and record+4 type feed distinct serializer calls; semantic_matches=1

### TemplateLookup

Pattern: `4C 8B 05 ?? ?? ?? ?? 49 8B D0 49 8B 40 08 80 78 19 00 75 ?? 66 39 48 20 73 ?? 48 83 C0 10 EB ?? 48 8B D0 48 8B 00 80 78 19 00 74 ?? 80 7A 19 00 75 ?? 66 3B 4A 20 73 ?? 49 8B D0 49 3B D0 74 ?? 48 8B 42 28 C3 33 C0 C3`

Güncel RVA: `0xA6D600`. Ham eşleşme: **10**; semantik geçerli: **1**.

İkincil doğrulama: Function-record WORD+6 feeds lookup; WORD key+20, sentinel+19, payload+28; semantic_matches=1

### LinkedAcquisitionPath

Pattern: `4C 8B C8 4D 8B C6 48 8B 94 24 C0 00 00 00 49 8B CD E8 ?? ?? ?? ??`

Güncel RVA: `0x7F2F2B`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: R9 receives linked lookup result; stack invocation=1, unchanged preparation flow; semantic_matches=1

### AcquisitionType2Insertion

Pattern: `8B BF 68 07 00 00 85 FF 0F 84 ?? ?? ?? ?? 48 8B 44 24 60 48 2B 44 24 58 48 C1 F8 03 48 83 F8 40 0F 83 ?? ?? ?? ?? B9 08 00 00 00 E8 ?? ?? ?? ?? 4C 8B F8 89 38 C6 40 04 02`

Güncel RVA: `0x7DA977`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: actor+768 ID, capacity64, allocation8, record+0 ID/+4 type2; semantic_matches=1

### AcquisitionRadiusGuard

Pattern: `F3 41 0F 10 46 48 0F 2F C1 0F 86 ?? ?? ?? ??`

Güncel RVA: `0x7DA935`. Ham eşleşme: **3**; semantik geçerli: **1**.

İkincil doğrulama: Template+48 radius comparison immediately precedes the uniquely validated type2 insertion path; semantic_matches=1

### TargetVectorBuilder

Pattern: `48 8D 8F 60 06 00 00 48 8B 51 08 48 3B 51 10`

Güncel RVA: `0x6CB40`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Owner+660 vector begin/end/capacity; independently unique handler sequence; semantic_matches=1

### ParsedActionBoundary

Pattern: `66 47 39 74 6F 3C`

Güncel RVA: `0x6CB73`. Ham eşleşme: **1**; semantik geçerli: **1**.

İkincil doğrulama: Same initial action handler as target-vector creation; semantic_matches=1

## EXISTING AOE ARCHITECTURE

Mevcut Initial Nx motoru korunmuştur:

1. Kullanıcının doğal ilk 0209 çağrısı beklenir.
2. Çağrı dönüşünde aynı thread ve RSP/R12/R13/R14/R15/RSI/RDI doğrulanır.
3. Toplam N tamamlanmadıysa yalnız RIP özgün preparation bloğuna yönlenir.
4. Oyun kendi argümanlarını yeniden hazırlar. N orijinali içerir; redirect en çok N-1.
5. 1/5/100 değerleri sırasıyla 0/4/99 redirect anlamındadır.

Bu motor doğal, yaklaşık 1 saniyelik 020A zincirine yeni bir timer eklemez.
0209/020A/020B skill ID olarak yorumlanmadı. Linked dispatch record+1=6,
+2=1D, word+8=0, linked word+6 koşullarını korur. 7F2F0E'de invocation=1;
normal shared lifecycle hazırlığında EDI=0 -> stack+28 -> invocation=0.
Helper bu değerleri yazmaz, özgün game hazırlığını tekrar yürütür.

Yeni profil liveValidationRequired=true kalır: aynı oturumda prep/call/return,
ilk0209 register invariants ve mevcut en az9 periodic020A call/return
kanıtı gelmeden ARM olmaz. Statik benzerlik runtime operation ID gözlemi değildir.

Nx motoru code patch, allocated stub veya gameplay WriteProcessMemory kullanmaz.
Sahip olunan DR slotlarını saklama, readback ile geri yükleme, Disable/Detach/
normal exit cleanup mekanizması değişmedi. Ayrı tarihî Tick100/BudgetWrite
deneyleri current build için genişletilmedi. TClient dosyasına yazılmadı.

## CODE CHANGES

- src/aoe_locator.hpp/.cpp: executable-section mask taraması, signed E8 decode,
  chained unwind kökü, semantik tekillik, exact profile ve salt-okunur live kanıt.
- src/common.cpp / VerifyTarget: yeni profil için debugger öncesi locator gate.
- src/model.hpp + src/profile.cpp: optional aoeLocatorRequired capability; eski
  SHA profillerinin davranışı değişmedi.
- profiles/9CD77...C28.json: ayrı exact-SHA profil, mevcut schema2, canlı validation
  zorunlu. Kapsam dışı target inspector/provenance alanları tahmin edilmedi.
- src/aoe_diagnostic.hpp/.cpp + src/main.cpp: App/settings oluşmadan diagnostic
  CLI; güvenli manuel açılış bayrağı.
- src/diagnostics.hpp/.cpp + src/main.cpp: mevcut observer/replay sayaçlarının
  açık kapsamlı görünümü; her-tick log yok.
- tests/aoe_locator_tests.cpp: sentetik ve mutasyon testleri.
- tests/manager_tests.cpp: gerçek dosya fingerprint testi eski sabit SHA yerine
  okuduğu dosyanın exact-SHA profilini seçer; eski profil fixture'ları korunur.
- CMakeLists.txt: aynı canonical EXE'ye locator/diagnostic ve test hedefi.
- tools/inspect_aoe.py: process açmayan offline PE/disassembly denetim aracı.
- İlgili README, signatures kataloğu ve bu statik raporlar.

Repetition/redirect, breakpoint kurulum ve restore motorlarının kaynakları
(initial_2x.cpp, initial_nx_lifecycle.cpp, tracer.cpp) değiştirilmedi.

## DIAGNOSTIC RESULT

- Required signatures: PASS (14/14, her biri 1 semantik sonuç).
- Offline CLI: exit0.
- Query/read-only live CLI: exit0, StaticProfileValidation=PASS,
  LiveSignatureValidation=PASS.
- AOE READY: YES yalnız locator/profil açısından.
- RuntimeArmed=NO; ReplayLiveValidation=REQUIRED_NOT_PERFORMED.
- DebuggerAttached=NO; HardwareBreakpoints=0; GameplayWrites=0.
- Locator tests: 46/46.
- Existing --analysis-only regression: 224/224.
- Mutation fixture: yalnız test-artifacts/aoe-locator-mutated.bin kullanıldı ve
  test tarafından kaldırıldı; target dosyası byte-for-byte aynı doğrulandı.
- Canlı repetition, server-side hasar veya count3/5/100 başarısı iddia edilmedi.

Sayaç sınırı: RequestedRepeatCount ve CompletedRepeatCount (doğrulanmış dönüş)
mevcut motordan gelir. Initial0209Count/Repeated020ACount ve CALL-site hit
sayaçları mevcut disarmed, aynı-session observation kapsamını açıkça gösterir.
Linked020BCount=UNAVAILABLE: mevcut breakpoint düzeni linked acquisition'ı ayrı
gözlemlemiyor. Yanlış sayı üretmek veya repetition/breakpoint mimarisini
genişletmek yerine bu eksiklik açık bırakıldı. Task10'un bu sayacı tamamlanmadı.

## BUILD

- EXE: C:\Users\Public\Documents\4UnityAOEManager\build\Release\4UnityAOEManager.exe
- Configuration: Release x64, PE Machine8664, GUI subsystem2.
- SHA256: CA24F4F542780EFA1B0D704FD2018A322E5B5DD154889E8CA80E8F9690529A50
- Mevcut 1.1.12/asInvoker manifest ve executable kimliği korundu.
  Yükseltilmiş TClient için manuel olarak yönetici başlatılmalıdır.
- Önceki canonical EXE SHA: 1BFC1025DD188114BBD832D8EB81D7BFA5A42FDAEBD1EA069AC409D49938CC5A.
- Yeni alternatif helper/v2 executable oluşturulmadı; test executable'ları gameplay helper değildir.

## MEMORY WRITES DURING ANALYSIS

NONE — TClient için. Debugger attach, HWBP, SetThreadContext veya oyun girdisi
analiz sırasında yapılmadı. Sentetik testlerin kendi veri/byte fixture'ları
TClient'e ait değildir. Hedef SHA analiz sonunda aynıdır.

## MANUAL TEST

1. TClient oyunda açıkken yönetici PowerShell'den canonical EXE'yi
   --aoe-diagnostic ile çalıştır; logs/aoe-diagnostic.txt içindeki gerekli
   imzaların PASS ve AOE READY=YES olduğunu doğrula.
2. GUI'yi --safe-manual ile yönetici aç. Bu açılışta Auto Attach ve Auto Arm
   kapalıdır; kaydedilmiş ayarları değiştirmez.
3. ATTACH NOW. ARM yapmadan bir normal AOE kullan; mevcut aynı-session
   Live Validation PASSED koşulunu bekle (ilk0209 + periodic020A call/return).
4. Initial Calls=3; ARM INITIAL Nx; bir normal AOE tetikle.
   RequestedRepeatCount=3, CompletedRepeatCount=3, observed initial=3,
   redirects=2 ve COMPLETED bekle. EXPORT JSON / COPY DIAGNOSTICS ile kaydet.
5. Yalnız 3 doğruysa Initial Calls=5 seç, yeni deney ready olduğunda
   ARM INITIAL Nx ve tek AOE kullan. Requested=5, Completed=5, redirects=4
   ve COMPLETED kontrol et.
6. DETACH (Disable/Restore). Debugger Attached=NO,
   Breakpoints Installed=NO ve CleanupBlocked/restore hatası olmadığını doğrula.
   Bir cleanup hatası varsa zorla kapatma; tanıyı paylaş.
7. 3 ve5 doğrulanmadan100 deneme. Görsel efekt veya client çağrı sayısı tek
   başına server kabulü/hasar kanıtı değildir.

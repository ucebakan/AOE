# Current authoritative checkpoint — Speed + Jump (2026-09-28)

This update supersedes older research priorities and unproven-effect conclusions below.
Evidence provenance: the user reports repeated live CE causal tests; this implementation session has not independently repeated them.

Build SHA-256: 9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28.
CTClientGame vtable RVA CDC970; CTClientChar vtable RVA CDBCC0; owner->local player +2710.
Heap addresses and module base are session-specific and must never enter build profiles.

GROUND SPEED: PROVEN by user-reported repeated ON/OFF live tests.
Local P+1200 = 542C; pin int32 23452 (5B9C) at P+1204 every ~25ms.
OFF stops/joins the writer without writing a fixed original value. Natural game updates restore normal behavior.
A one-time observation of value 23452 does not establish the pinning effect. Prior lookup-key semantics remain valid and do not contradict the newly reported effect.

JUMP HORIZONTAL SPEED: PROVEN by user-reported live tests.
P+8E0 float; writer RVA845B8F, original F3 0F 11 8B E0 08 00 00.
ON validates and replaces the eight-byte writer with NOPs, then pins float20.0 every ~25ms.
OFF first stops/joins the writer, then restores exact instruction bytes and flushes instruction cache. No fixed field restore value.
10.0 and20.0 tested;20.0 stronger; no normal-ground speed effect. Concurrent Speed+Jump and standalone Jump reported working.

Implementation: dedicated two-toggle companion, persistent SHA-specific build profiles; unique class/object validation; no stale session addresses; no new scans on button clicks.
Old helper homology remains UNRESOLVED. Do not mix Saga/other-client offsets.

---
Archived research history (superseded where contradicted above):
# 4Unity Speed checkpoint — 2026-09-28

## Latest priority: old +1348 late-layout mapping

Supersedes the field-inventory next step below. See `speed_late_layout_mapping.md`.
Recovered CheckForFreeze MoveNext from the archived helper memory image:
old p2+1348 read -> local V_2 -> comparison with `_lastFreezeValue` ->
elapsed-time / freeze-alert counter / Discord alert and StopBot_Click paths.
The sampled value is not displayed in the alert, and no expected literal
character name/state text is present. ReadString's 32-byte, UTF-8, NUL-terminated
interpretation does not prove the game stores a genuine text field there.
Proxy method targets are qualified in the report.

Current TClient restarted (PID 27456); previous base/owner/P are stale until
re-resolved. VM_READ attempt returned ACCESS_DENIED (5), so no live late-region
scan occurred. Current counterpart, layout shift and predicted speed offset
remain UNRESOLVED. No writes or patches were made.

NEXT: CE read-only idle/walk raw late-region snapshots after re-resolving P.
Only after a verified anchor C may C-1348 be considered a shift and C-124 a
candidate speed offset; insertion between old 1224 and 1348 remains untested.

## Latest update: bounded integer-field inventory

Current focus is the user's requested 2/4-byte field inventory at +1000..+1800,
not further +1224 xref/caller tracing. See `speed_char_field_inventory.md` and
`../logs/speed_char_fields.csv`: 186 function spans, 108 operand references,
including one direct-call level; incomplete class coverage and unresolved
receiver bases are explicitly marked. No validated integer ground-speed
shortlist emerged. 1136/1138 + 12B8/12BC show 708-wrapped, angle-like arithmetic;
116C..1178 are float-operated fields, not integer-speed candidates.

User reports current +1224 did not yield SpeedHack in their test. This is not
proof that old/current fields are non-homologous, nor proof old p2 equals current
P. Both historical identity/homology claims remain UNRESOLVED.

**RESTORE P+1224=529 / FREEZE OFF: REQUESTED, NOT PERFORMED OR VERIFIED.** CE was
not exposed as a targetable window; the user must complete this in CE. No memory
edit was made by this inventory pass. Current base/heap values were not reverified.

Latest next step: classify remaining inventory rows by receiver and integer
semantics before proposing writes. Ground Speed remains NOT PROVEN. Older
sections below retain earlier evidence and experiments, not the latest priority.

Bu kayıt kullanıcı tarafından bildirilen CE runtime bulgularını ve aşağıda ayrı belirtilen yeni statik incelemeyi özetler. Bu tur bağımsız runtime testi yapılamadı. Canlı adresler yalnız bildirilen oturum için geçerlidir; restart sonrası base/owner/P yeniden çözülmelidir. Araştırma hedefi yalnız NORMAL GROUND WALK SPEED'dir.

## Target ve doğrulanmış canlı nesne

- TClient.exe base: `0x7FF6161C0000`
- SHA-256: `9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28`
- CTClientGame owner: `0x14DBE94E3A8`; `[owner]` RTTI: `.?AVCTClientGame@@`
- `[owner+0x2710] = P = 0x14DBE7267E0`
- `[P] = TClient.exe+CDBCC0` — CTClientChar vtable.
- Bu nesne zinciri CE runtime ile doğrulandı.

## Canlı hareket gözlemleri

- `P+7D4`: idle `0`, hareket sırasında `4` gözlendi.
- `P+8E0`: idle `0`, hareket sırasında yaklaşık `5.6249` gözlendi.
- `P+70/+74/+78` hareket sırasında değişiyor.
- `+845B8F: movss [rbx+8E0],xmm1` writer'ı canlıda doğrulandı; hit sırasında `RBX=P`. Bu, `+8E0` alanının Speed field olduğunu kanıtlamaz.

## Transform yolu

- `P+70` writer: `+A0BCA8: movups [rbx+rax+30],xmm10`; hit sırasında `RBX=P`, `RAX=0x40`.
- `+A0BCA8` generic transform/matrix yazım yoludur; `+A16BD0` 4×4 matrix multiply helper olarak çözüldü.
- Kaynak transform `P+80..BF`, sonuç `P+40..7F` alanına yazılıyor.
- `P+70/+74/+78` composed/world-transform sonucudur; doğrudan Speed field değildir.

## P+B0 writer'ları

| RVA | Mevcut bulgu |
| --- | --- |
| `+A0B5FF` | Genel transform/update; idle'da da çalışıyor. |
| `+7B017C`, `+844112` | Kamera kaynaklı; Speed adayı olarak elendi. |
| `+86F66E` | Shared-state yolu. |
| `+843EEB`, `+843F07` | Düşük frekanslı gözlendi. |
| `+79FCB3` | Yürürken yüksek frekanslı; displacement/Speed yolu olarak doğrulanmadı. |

## +79FCB3: yüksek hit sayısı displacement kanıtı değil

Canlı gözlenen yol:

```text
+79FCA4  addss xmm1,[rbx+B0]
+79FCAC  mov rax,[r15+2710]
+79FCB3  movss [rax+B0],xmm1
```

Hit sırasında `R15=owner`, `RAX=RBX=P`. Öncesindeki çağrılar:

```text
+79FB43  mov rcx,[r15+2710]
+79FB4A  call +845A00
+79FB53  mov rcx,[r15+2710]
+79FB5A  call +781CE0
```

`+781CE0`, ilişki kontrolü geçerse `P+1390` üzerinden linked object'i, aksi halde `P`'yi kaynak seçer; kaynak `+70/+74/+78` değerlerini caller buffer'a kopyalar. `+781DB0` seçilen kaynağın `+70`, `+781E10` ise `+78` değerini döndürür. `+863CC0` yalnız `lea rax,[rcx+40]; ret` içerir.

Kritik runtime testinde aynı frame/RBP için `+79FB5F` ve `+79FC9B` noktalarında `[RBP-10], [RBP-0C], [RBP-08] = (0,0,0)` bulundu. Normal yürüyüşte `+79FC60` üzerinden `+845A00` çıktısının branch/copy yolu gözlenmedi. Örneklenen yürütme zero-delta/no-op correction ile uyumludur; bu bulgu bütün olası yürütmeler hakkında sonuç vermez. `+79FCB3` Speed yolu olarak adlandırılmayacak ve yeni kanıt gelmedikçe önceliklendirilmeyecek.

## Ground walk güncellemesi — önceki hedefin yerine geçer

### Kullanıcının yeni CE bulguları

- Gerçek oyun XYZ ile birebir eşleşen alanlar: `P+B0/B4/B8`, adresler `0x14DBE726890/894/898`.
- `+A0B5FF: movups [rbx+B0],xmm3` hit'inde `RBX=P`, `XMM3=(X,Y,Z,1)`. Yerel XYZ commit doğrulandı; idle'da da çalışması ve generic transform olması nedeniyle speed hesabı olduğu sonucu çıkmaz.
- `+A0B080` girişinde `RCX=P`; bildirilen caller `+A0DA26`. `CTClientChar vtable+A8 -> +14220`, yalnız `ret`; output producer değil.
- `+8E0` idle 0, normal walk yaklaşık 5.6, duvara karşı ileri basıldığında da yaklaşık 5.6. Kullanıcının writer NOP + manuel scalar artırma deneyinde ground hızı değişmedi, forward jump horizontal hızı arttı. Bu tur bu değişiklikler tekrarlanmadı; mevcut canlı patch durumu okunamadı.
- `+846E20` reader jump anında bir kez çalıştı; `+846EC0/EC8 -> 8A8/8AC -> +845AFB/+845B03` jump yolu. `+845A00` içindeki 8A*/8B* component read breakpointleri normal düz yürüyüşte vurmadı. Jump/special movement araştırması yeniden açılmayacak; bu zincir ground speed diye adlandırılmayacak.
- Generic instruction hit'leri diğer moblardan gelebilir. Yerel sonuç için exact `P+B0/P+B8` watchpoint veya doğrulanmış receiver/base register `== current P` filtresi gerekir.

### Bu tur bağımsız statik bulgu

Disk SHA-256 tekrar eşleşti. `+A0B080` içindeki commit girdisi şu kadar geriye izlenebildi; ground-specific olduğuna dair yeni canlı kanıt yok:

```text
P -> +A0FE80 -> selected record S
Q = [S+20]
Q+34 = P+464; Q+48 = 0
+A0B12B -> +7FE400(Q, O=&[RSP+50])
+A0B13B -> virtual +A8 (bildirilen CTClientChar hedefi ret)
O+8/+C/+10 = [RSP+58/+5C/+60]
       ↓ +A0B228 / +A0B222
XMM15 = (üç bileşen, 1)
       ↓ transform composition
+A0B5FF -> P+B0..BF
```

`+7FE400`, `Q+18..20` pointer koleksiyonundaki kayıtların `+8/+C/+10` alanlarını çıktıya aktarır veya kayıt `+4` değerleri arasında doğrusal interpolasyon yapar (`+7FE606..+7FE677`). Boş koleksiyonda bu üçlü sıfırdır. Ayrı `+14/+18/+1C` ve `+20/+24/+28` üçlüleri de üretilir. Çıktının üç bileşeni transform girdisidir; hız vektörü/miktarı etiketi henüz verilmez.

`+A0B5FF` yolunda `+A0B51E` eski `P+B0..BF` satırını okur; `+A0B5BD..+A0B5FC` diğer eski matrix satırlarıyla ağırlıklı toplam üretir. Bu nedenle yalnız son XMM3 değerini görmek gerçek delta kanıtı değildir; aynı çağrıdaki önce/sonra farkı gerekir.

Önemli caller ayrımı: `+A0DA26`, `EDX=1` ile çağırır; `+A0B253/255` kontrolünden commit dalına geçilir. Aynı üst fonksiyonun `+A0DAB8` çağrısı `EDX=0` kullanır ve `+A0B812` dalına gider. `+A0DA26` çağrısı `P+464` birikiminin hesaplanan eşik değerine ulaşmasına bağlı döngüdedir; her frame çağrıldığı veya ground'a özel olduğu kanıtlanmadı. `P+464`, `+A0D857`'de zaman girdisi eklenerek güncellenir; bu da tek başına ground-speed scalar kanıtı değildir.

Kanıt dökümleri: `logs/speed_ground_A0B080.txt`, `speed_ground_7FE400.txt`, `speed_ground_A0D690.txt`, `speed_ground_A0FE80.txt`.

### Canlı erişim sınırı

PID 4024 için `OpenProcess(QUERY_LIMITED_INFORMATION|VM_READ)` tekrar `ERROR_ACCESS_DENIED (5)` döndürdü. Computer-use listelerinde CE çalışan uygulama olarak göründü ancak hedeflenebilir CE penceresi bulunmadı. Bu oturumun komut aracında yetki yükseltme kapalı; kullanıcının UAC izni alınmış olsa da yükseltilmiş işlem başlatılmadı. Base/heap/vtable bu tur tekrar okunamadı, watchpoint kurulmadı, oyun belleği değiştirilmedi. Idle/walk/wall karşılaştırması ve causal test yapılmadı.

### Tek sonraki deney

Güncel P kimliği ve normal, patchesiz test koşulu doğrulandıktan sonra, `+A0B130` noktasında `RBX==current P` ve `ESI==1` filtresiyle aynı çağrının `O=[RSP+50]` çıktısını (`O+8/+C/+10`), `P+464` ve eski `P+B0/B8` değerlerini kaydet; `+A0B5FF` öncesindeki XMM3 ile eski X/Z farkını eşleştir. Idle / düz ground walk / wall-blocked-forward örneklerini aynı gözlem protokolüyle karşılaştır. Amaç `+7FE400` çıktısının gerçek yerel koordinat farkına katkısını ayırmak; henüz scalar değiştirmek değil. Breakpoint altında geçen süreyi oyun zamanı veya hız ölçümü sayma.

Tarihsel `TClient.exe+762D60,2678,1224` / int `23452 (0x5B9C)` yalnız referanstır. Old `+1224` ile current `+1204` ayrı; 4Saga/GameSpeed/LegitSpeed offsetleri kullanılmaz. Ground speed mekanizması ve old helper bağlantısı açık kalır.

```text
LOCAL P = PROVEN
+8E0 WRITER = PROVEN
+79FCB3 SPEED RELEVANCE = NOT PROVEN / DEPRIORITIZED
GROUND PATH = PARTIAL
LOCAL XYZ WRITE = +A0B5FF; RBX=P (user CE verified)
GROUND-SPECIFIC UPSTREAM = UNRESOLVED; +7FE400 -> +A0B080 static candidate
CANDIDATE FIELD/VECTOR = +7FE400 output O+8/+C/+10; ground relevance UNRESOLVED
CAUSAL TEST = NOT PERFORMED; live access unavailable
OLD HELPER CONNECTION = UNRESOLVED
SPEED = NOT PROVEN
NEXT = local-filtered +A0B130 output -> +A0B5FF same-call X/Z delta correlation
```

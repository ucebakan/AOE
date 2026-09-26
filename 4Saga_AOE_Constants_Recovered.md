# 4Saga AOE Runtime Constant Recovery

Tarih: 2026-09-25  
Kapsam: yalnız `AoEHackBtn` AOE constant getter'ları, AOE object table girdileri ve `InviCheckTimer_Tick` içindeki AOE alan yazıları. `Program.Main` çağrılmadı; TClient'e attach/write/patch yapılmadı.

## DEPENDENCY RESOLUTION

İzole CLR4 dizini: `C:\Users\xaofx\Downloads\SagaAOEClr4Recovery`

- Host: .NET Framework CLR4, x64; `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
- `Newtonsoft.Json.dll` doğrudan yükleme: **PASS**
- Identity: `Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed`
- Location: `C:\Users\xaofx\Downloads\UnConfuserEx-main\UnConfuserEx-main\4Saga\Newtonsoft.Json.dll`
- Copied managed dependencies: `Memory.dll`, `Newtonsoft.Json.dll`, `AForge*.dll`, `NAudio*.dll`, `Tesseract.dll`
- AssemblyResolve, load öncesinde kaydedildi; bu çalıştırmada özel resolver üzerinden ek dependency talebi oluşmadı.

## CLR LOAD

- Copied helper: `C:\Users\xaofx\Downloads\SagaAOEClr4Recovery\4Saga.exe`
- `Assembly.LoadFrom`: **PASS**
- Identity: `4Saga, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null`
- `RuntimeHelpers.RunModuleConstructor(module.ModuleHandle)`: **PASS**
- Normal uygulama entry point'i çalıştırılmadı.

## THREE AOE AOBS

### PRIEST

- Key: `0xD7614662`
- MethodSpec: `0x2B000005`
- Generic MethodDef: `0x06000005`
- Closed return type: `System.String`
- Plaintext: `a9 01 00 00 00 00 00 00 ?? ?? ?? ?? ?? ?? 00 00 00 00 00 00 00 00 00 00 3F`
- Length: 74 characters; 25 byte tokens; 6 wildcard tokens
- Validation: printable, non-empty, valid Memory.dll AOB syntax

### MAGE

- Key: `0x9C195CCE`
- MethodSpec: `0x2B000005`
- Generic MethodDef: `0x06000005`
- Closed return type: `System.String`
- Plaintext: `42 01 00 00 00 00 00 00 ?? ?? ?? ?? ?? ?? 00 00 00 00 00 00 00 00 00 00 3F`
- Length: 74 characters; 25 byte tokens; 6 wildcard tokens
- Validation: printable, non-empty, valid Memory.dll AOB syntax

### ARCHER

- Key: `0x33E0B153`
- MethodSpec: `0x2B000002`
- Generic MethodDef: `0x06000007`
- Closed return type: `System.String`
- Plaintext: `0A 02 00 00 00 00 00 00 ?? ?? ?? ?? ?? ?? 00 00 00 00 00 00 00 00 ?? ?? 3F`
- Length: 74 characters; 25 byte tokens; 8 wildcard tokens
- Validation: printable, non-empty, valid Memory.dll AOB syntax

Alan eşlemesi decrypted `MoveNext` dataflow'undan gelir: üç `FirstOrDefault<Int64>` sonucu sırasıyla `aoe_priest` (`0x04000378`), `aoe_mage` (`0x04000379`) ve `aoe_archer` (`0x0400037A`) alanlarına kaydedilir.

## CONSTANT TABLE

- Field token: `0x04000A9A`
- Field: `System.Object[] 3209ED98::D99E4FA2`
- Runtime type: `System.Object[]`
- Length: `14176`

| Index | Runtime type | Değer / çağrı şekli |
|---:|---|---|
| 30 | `FD0BFE03` delegate | `String(Int64 ByRef, String)`; dinamik target token `0x00000000`; adresi `ToString("X")` biçimine dönüştüren proxy |
| 82 | `8388B42F` delegate | `Void(Object)`; dinamik target token `0x00000000` |
| 251 | `F3BC4BA4` delegate | `Boolean(Object,String,String,String,String,Object)`; dinamik target token `0x00000000`; `WriteMemory` proxy şekli |
| 365 | `119F5824` delegate | `Int32(Object,String,String)`; dinamik target token `0x00000000` |
| 443 | `AABDF419` delegate | `Void(AsyncVoidMethodBuilder ByRef)`; dinamik target token `0x00000000` |
| 590 | `0D3FFB34` delegate | `Object(Object,Int64,Int64,String,Boolean,Boolean,Boolean,String)`; dinamik target token `0x00000000`; `AoBScan` proxy şekli |
| 880 | `5481FB8C` delegate | `Void(AsyncVoidMethodBuilder ByRef,Object)`; dinamik target token `0x00000000` |

Delegate target'ları runtime proxy/dynamic method olduğundan `Method.MetadataToken` sıfırdır; isim uydurulmamıştır.

## AOE WRITE VALUES

`InviCheckTimer_Tick` token `0x060003C8`, runtime RVA `0x184248`. AOE ile ilgili altı write çağrısının tamamında:

- Address format: `X`
- Write type: `int`
- File argument: boş string
- Encoding argument: null

| Sınıf | `anchor+0x60` yolu | `anchor+0x8C` yolu |
|---|---:|---:|
| Priest | `1000` | `328` |
| Mage | `1000` | `324` |
| Archer | `1000` | `314` |

Önemli semantik düzeltme: IL'de aynı adrese ayrı ON ve OFF değerleri yazan restore çiftleri yoktur. `aoehigh=true` kontrol yolu üç sınıfta `anchor+0x60 = 1000` yazar; diğer yol `anchor+0x8C` alanlarına sırasıyla `328/324/314` yazar ve `aoehigh=false` durumunu korur. Bu nedenle `+0x60 OFF` ve `+0x8C ON` değerleri mevcut AOE IL tarafından **tanımlanmamıştır**. Bunları “original-byte restore” olarak yorumlamak doğru değildir.

Getter kanıtları:

- `WRITE_TYPE_A`: MethodDef `0x06000007`, key `0x1ED923A6` → `int`
- Mage `+0x8C` write type: MethodDef `0x06000008`, key `0x33EEE203` → `int`
- Priest `+0x60` write type: MethodDef `0x06000004`, key `0x526D10B6` → `int`
- Mage `+0x60` write type: MethodDef `0x06000005`, key `0x84923B6F` → `int`
- Archer `+0x8C`: MethodDef `0x06000006`, key `0x8375613C` → `314`
- Priest `+0x8C`: MethodDef `0x06000008`, key `0xF4D04865` → `328`
- Mage `+0x8C`: MethodDef `0x06000006`, key `0xAE4AD456` → `324`
- Ortak `+0x60`: MethodDef `0x06000008`, key `0xB0EDCB0D` → `1000`

## CURRENT TCLIENT MATCHES

Target: `C:\Program Files\4Saga Official\TClient.exe`  
SHA-256: `CD8FDA20BB99A6689DB99FCFE8B568DF027A0900927B797D8D5AB2FA3CC00EC7`

| Sınıf | Exact disk-image match | File offset | RVA | Robust AOB |
|---|---:|---|---|---|
| Priest | 0 | N/A | N/A | N/A — pattern opcode dizisi değildir |
| Mage | 0 | N/A | N/A | N/A — pattern opcode dizisi değildir |
| Archer | 0 | N/A | N/A | N/A — pattern opcode dizisi değildir |

Bu sonuç version drift göstergesi değildir. Patternlerin yapısı ve takip eden kullanım, bunların module `.text` talimat imzaları değil, TClient'in çalışma zamanında oluşturduğu dinamik veri nesnesi imzaları olduğunu gösterir:

- İlk 8 byte küçük bir kimlik değeridir (`0x1A9`, `0x142`, `0x20A`).
- `AoBScan` aralığı `0 .. Int64.MaxValue` olarak tüm process address space'tir.
- Bulunan adreslere `+0x60/+0x8C` eklenip `WriteMemory(...,"int",...)` uygulanır.
- Disk `TClient.exe` içinde üç exact match'in de sıfır olması bu modelle uyumludur.

CALL/JMP/RIP-relative wildcarding uygulanmadı; patternlerde bu tür native instruction operandları yoktur. Opcode varsayımıyla wildcard üretmek semantik olarak yanlış ve çoklu/yanlış eşleşmeye açıktır.

## NATIVE PATCH LOCATIONS

Priest/Mage/Archer için statik module RVA yoktur. `anchor+0x60` ve `anchor+0x8C` native instruction adresleri değil, runtime AOB ile bulunan dinamik nesnenin `int` alanlarıdır. Bu nedenle istenen `match-0x20 .. match+0xB0` native disassembly ve “original instruction bytes” kavramları bu implementation'a uygulanamaz.

| Sınıf | Anchor | `+0x60` | `+0x8C` |
|---|---|---|---|
| Priest | runtime heap/data match | `int` data field ← `1000` | `int` data field ← `328` |
| Mage | runtime heap/data match | `int` data field ← `1000` | `int` data field ← `324` |
| Archer | runtime heap/data match | `int` data field ← `1000` | `int` data field ← `314` |

No process attach/read istendiği ve uygulanmadığı için güncel oturumdaki dinamik absolute adresler bilinçli olarak toplanmadı.

## CONCLUSION

Arkadaşın 4Saga AOE implementasyonu native code branch/call patch'i değildir. `AoEHackBtn` tüm process address space'inde üç runtime veri imzasını arar ve Priest/Mage/Archer nesne adreslerini saklar. Periyodik handler, `aoehigh` durumuna göre bu nesnelerin `int` alanlarını değiştirir:

- High yolu: üç sınıfta `+0x60 = 1000`
- Diğer yol: Priest `+0x8C = 328`, Mage `+0x8C = 324`, Archer `+0x8C = 314`

Bu, TClient native talimatlarını tekrar çağıran veya instruction opcode'larını değiştiren bir sistem değil; üç class-specific runtime veri nesnesinin alanlarını değiştiren bir AOE parametre sistemidir.

## MODIFICATIONS

NONE — original `4Saga.exe`, current `TClient.exe`, processler ve mevcut 4Unity projesi değiştirilmedi. Yalnız izole CLR4 analiz kopyası/host/logları ve bu rapor oluşturuldu.

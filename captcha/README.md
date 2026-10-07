# Captcha / 4Unity Puzzle Test v1.1

[Tam Türkçe rapor](CAPTCHA_TAM_RAPOR.md): çalışma mantığı, tüm kaynak kodları, bellek profili, kullanım, derleme, test kayıtları ve bilinen sınırlar tek belgede.

| Klasör/dosya | İçerik |
|---|---|
| src/ | Derlenebilir C# WinForms projesi ve özgün açıklamalar |
| research/ | Üç statik PE/RTTI araştırma betiği; Python bağımlılık listesi |
| evidence/ | 5 Ekim 2026 tesliminin test JSON'ları, görüntüler ve manifest |
| references/ | Kullanıcının gönderdiği dört örnek kare |
| report-tools/ | Rapor ana metni ve yeniden oluşturma betiği |
| source-manifest.json | Arşiv dosyalarının boyut/SHA-256 envanteri |

Depo kökünden:

```powershell
dotnet build captcha/src/4UnityPuzzleTest.csproj -c Release
dotnet publish captcha/src/4UnityPuzzleTest.csproj -c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o captcha/releases/1.1.0
```

EXE yönetici UAC onayı ister (requireAdministrator / uiAccess=false). Derleme için .NET 9 SDK, çalışırken uygun Windows OCR dili gerekir.

Raporu arşivden yeniden üretmek: `python captcha/report-tools/export_captcha_report.py --refresh`. Betik standart Python kütüphanesini kullanır. Statik oyun araştırma betikleri ise pefile/capstone ister ve sabit oyun dosya yolları içerir.

Dosya hash'leri ve kod eklerini kontrol etmek: `python captcha/report-tools/verify_captcha_archive.py captcha`. Bu kontrol oyun sürecine erişmez.

Yerel test penceresinde otomatik geliş → dört doğru seçim → kapanış doğrulandı. Gerçek oyun tıklama kabulü ve canlı bellek geçişleri doğrulanmadı; mevcut bellek tanılaması Win32 5 erişim reddi aldı. Farm bağlantısı henüz eklenmedi. Bu arşiv 7 Ekim 2026'da hazırlanmıştır; EXE ikilisi tekrar eklenmemiş, teslim kimliği evidence/build.json içinde korunmuştur.

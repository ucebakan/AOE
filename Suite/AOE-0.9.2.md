# 4UnityTools 0.9.2 · AOE Manager sınıf desteği

AOE Manager normal AOE çağrısından yeteneği otomatik tanır:

| Sınıf | Yetenek | Başlangıç / periyodik kayıt |
|---|---|---|
| Archer | Rain of Arrows | 321 / 322 |
| Mage | Ice Rain | 424 / 425 |
| Priest | Shadow Thunderstorm | 521 / 522 |

Bu adlar, çağırma descriptor'ları ve periyodik → alan hasarı bağlantıları güncel
FileMerger.unity arşivinde doğrulandı. Archer ve Mage için gerçek oyun casti
bu sürüm hazırlanırken henüz kaydedilmedi; destek, canlı doğrulama kapısıyla
sunulur. Testlerdeki başarı gerçek oyunda/server'da sonuç kanıtı değildir.

Attach sonrası **Initial Nx kapalıyken normal bir AOE** kullan. Manager doğru
yetenek adını göstermeli; başlangıç dönüşü ve aynı thread/owner üzerindeki
dokuz periyodik çağrı/dönüş tamamlanınca **Live Validation: PASSED** olur.
Sonra Initial Nx etkinleştirilebilir. Hide AOE Visual, aynı aileye ait TSkill
kaydını aynı thread'de reader → prep → call sırasıyla yakalar; özgün görsel
değerini dinamik okur. Initial Nx ile birlikteyken son replay dönüşüne kadar
özgün alan korunur; son dönüşte Hide tekrar uygulanır.

Başka desteklenen başlangıç yeteneği görülürse önceki aileye ait canlı kanıt
silinir, bekleyen replay durdurulur ve önceki sahip olunan görsel geri alınır.
Yeni aile için normal doğrulama ve Hide'ın yeniden etkinleştirilmesi gerekir.
Core/reader donanım breakpoint'leri bu geçişte yeniden kurulmaz.

Oyun patch'i kod adreslerini değiştirirse mevcut SHA/AOB/semantik recovery
devam eder. Kaynak yetenek ID'leri de değişirse bilinmeyen kayıtlar kabul
edilmez; arşiv kataloğu ve aile sözleşmesinin ayrıca güncellenmesi gerekir.
Uygulama açılışı/otomatik tarama işlev arm etmez veya oyun belleğine yazmaz.

Dağıtım UAC manifesti: `requireAdministrator`, `uiAccess=false`.
Yerel kaynak/fixture, arayüz/DPI ve patch recovery sonuçları sürüm klasöründe
bulunur. Eski açık 4UnityTools süreçlerini kapatıp kök EXE'yi yeniden aç.

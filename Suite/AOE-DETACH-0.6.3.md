# 0.6.3 — Hide AOE Visual + Initial AOE

5 Ekim 2026, 02:53 oturum logunda görsel reader başarıyla TSkill'i buldu.
Hemen arkasından reader producer ile değiştirilirken tüm Initial breakpoint'ler
Restore/Arm işleminden geçirildi. Bekleyen core single-step'in DR6 cause biti
silinince `Initial Nx #DB did not match owned DR6/DR7/RIP state` koruması
debugger oturumunu kapattı.

0.6.3 doğrulanmış görsel reader'ı attach sırasında dördüncü slotta kurar; üç
Initial core noktası ve reader oturum boyunca sabit tutulur. Hide capture
aç/kapat ve Nx reset yalnız model durumunu değiştirir; DR6/DR7'yi yeniden
yazmaz. Reset mevcut sahiplik kayıtlarını ve canlı code fingerprint'lerini
doğrular. Her gerçek debug olayında kesin DR6/DR7/RIP kontrolü korunur.
Yabancı olayların iletimi ve debug-register geri alma yolu korunur.
Producer sayımı reader slotu tutulurken kullanılamaz; Initial tekrar sayacı
ve algoritması değişmez. Gizleme yalnız kullanıcı açıp TSkill korelasyonu
doğrulandıktan sonra yapılır; attach görsel alanına yazmaz.

Yerel trace_fixture sürecinde üç thread'in eşzamanlı 60 çağrısı sırasında görsel
capture aç/kapat/yeniden açma altı tur denenir. Tüm olayların korunması, debugger'ın
Attached kalması, yeni thread kapsamı ve özgün register geri alma değerleri
kontrol edilir. Görsel/Initial model testleri ve önceki debugger regresyonları
da çalışır. TClient'e test amacıyla bağlanılmaz ve oyun belleğine yazılmaz.

4UnityTools'u normal şekilde kapatıp 0.6.3'ü açın. UAC manifesti
`requireAdministrator`, `uiAccess=false` olarak korunur. Oyun içindeki iki işlevin
birlikte kullanımı kullanıcı tarafından yeniden doğrulanmalıdır.

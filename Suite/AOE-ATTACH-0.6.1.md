# 0.6.1 — Multikill açıkken AOE attach

5 Ekim 2026 loglarında AOE profili yeni oyun SHA'sı için başarıyla bulunmuştu.
Attach, `TClient.exe+0x191B45` penceresindeki `0F86 → 0F84` değişikliğine
takılıyordu. Bu, aynı oturumdaki Multikill'in sahip olduğu branch değişikliğiydi.

Yeni canlı doğrulama yalnız semantik taramanın bulduğu type-2 radius JBE'nin
Multikill JE biçimini kabul eder. PID, oluşturulma zamanı, modül tabanı, SHA,
RVA, original/patched byte'lar, açık sahiplik kilidi, kayıt ve tam canlı
Multikill kod penceresi doğrulanır. Kontrol salt okunurdur.

Kanıt uygulama içindeki aktif Multikill sahibinden alınır. Diskteki kayıt tek
başına yeterli değildir. Multikill kapanırken, geri alınırken veya hata
aldığında izin kaldırılır. Yabancı JE, değişen branch hedefi, çevre kod veya
başka oturum kabul edilmez. AOE'nin aynı oturumdaki cast doğrulaması korunur.

Bu düzeltme debugger attach denetimini değiştirmez; UAC gereksinimi korunur.
Testlerde oyun belleğine yazılmaz, oyun debugger'a bağlanmaz veya AOE arm
edilmez. Native testler kendi debugger fixture süreçlerini kullanır.

Sürüm: `Suite/releases/0.6.1/4UnityTools.exe`.

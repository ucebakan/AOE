# 0.6.2 — Hide AOE Visual

Hide AOE Visual için kurulan TSkill reader breakpoint'i, Initial Nx tamamlanıp
yeniden hazırlanırken producer breakpoint'iyle değiştiriliyordu. Hide kutusu
seçili ve runtime capture bayrağı açık kalsa da gerçek reader kapsamı kayboluyordu.
5 Ekim oturum logunda reader kurulumu ve ardından Nx reset görülüyor; aralarında
bir `visual_reader_hit` veya `visual_tskill_resolved` yok.

0.6.2 bekleyen görsel yakalama kapsamını Nx reset sırasında korur. Görsel gizleme
açıkken prep/call/return kapsamı tamamlanan Nx turundan sonra da korunur; böylece
sonraki normal ve periyodik AOE çağrıları gizleme durumunu güvenli biçimde sürdürür.
Initial Nx sayacı ve tekrar algoritması değiştirilmedi.

Gizleme isteği UI timer'ında doğrudan sıfır yazmak yerine debugger worker'a gider.
Yeni çözülen çağrı hâlâ çalışıyorsa veya Nx tekrarları sürüyorsa özgün görsel alanı
son return'e kadar tutulur. Son return'de sıfır yeniden uygulanır. Aç/kapat
geçişleri durdurulmuş thread'lerde ve aynı PID/oluşturulma zamanı üzerinden,
yalnız yakalanan özgün değer ile sahip olduğumuz sıfır arasında yapılır.
Geri okuma başarısızlığında geri alma sorumluluğu korunur. Kapanış/debugger hata
temizliği de sahip olunan görsel değerini geri yükler. Başka değerler ezilmez.

Kullanım: eski 4UnityTools'u normal şekilde kapatıp 0.6.2'yi açın. Attach sonrası
Hide AOE Visual'i seçin. TSkill henüz çözülmediyse bir AOE kullanımı gerekir;
çözülmeden yazım yapılmaz. Zaten oluşmuş bir efektin silinmesi vaat edilmez.

Dağıtılan EXE `requireAdministrator`, `uiAccess=false` manifesti içerir.
Testlerde TClient'e bağlanılmaz ve oyun belleğine yazılmaz. Native testler
yerel fixture süreçlerini ve sahte görsel alanlarını kullanır. Oyun içinde
görsel sonucu kullanıcı yeniden başlatıp deneyerek doğrular.

# 4UnityTools 0.9.3 · Rain of Arrows başlangıç yolu

Archer Rain of Arrows'un başlangıcı, Priest için kullanılan lifecycle çağrı
noktasından geçmiyordu. Ayrıca periyodik çağrılar caster yerine onun oluşturduğu
AOE nesnesine aitti. Bu yüzden 0.9.2 Initial Nx doğrulamada bekliyordu.

Bu sürüm ortak worker girişini ve iki doğrulanmış dönüş noktasını takip eder.
Başlangıç hazırlığı, o oturumda kullanılan normal skill'in çağrı/dönüş kanıtından
seçilir. Archer hazırlığı AOB, tekil semantik eşleşme, unwind sınırı, ortak worker
hedefi, stack/frame argümanları ve canlı özgün byte kontrolüyle bulunur. Patch
sonrasında aynı tarif yeniden uygulanır; eksik/çoklu eşleşmede eski adres
kullanılmaz.

Periyodik nesnenin creator ID'si, oyunun worker'ında kullanılan sanal getter
üzerinden doğrulanır. Vtable slotu ve getter kodu exact SHA dosyasından türetilir;
canlı method pointer/kod ve local actor ID eşleşmesi gerekir. Başka oyuncuların
çağrıları sınıfı değiştiremez, Hide alanını çözemez veya Initial Nx'i doğrulayamaz.

Worker girişi, iki dönüş ve görsel reader için dört donanım takip noktası attach
sırasında kurulur. Hide/Initial Nx açma, kapama ve tekrarları bunları değiştirmez.
Replay öncesinde ve her dönüşte RSP ile hazırlıkta kullanılan nonvolatile
register'lar, Archer'ın frame register'ı dahil, doğrulanır. N çağrı için en çok
N-1 yönlendirme yapılır.

AOE Manager'daki uzun beyaz tanı metinleri kaldırıldı. Bağlantı, Initial Nx ve
işlem engelinin kısa açıklaması gösterilir; işlev düğmeleri korunur.

Attach sonrası Initial Nx kapalıyken bir normal Rain of Arrows kullanıp yaklaşık
12 saniye bekle. Doğrulama tamamlanınca Initial Nx'i aç ve yeni bir cast yap.

2026-10-07 read-only Archer kaydı: 1 başlangıç + 9 periyodik çağrı ve dönüş,
yerel caster → AOE nesnesi creator ilişkisi ve temiz debugger detach doğrulandı.
Bu kayıt replay veya server'ın ek hasar kabulü kanıtı değildir. Mage Ice Rain
gerçek oyun testi halen ayrıca gereklidir; Priest/Mage aile sözleşmeleri korunur.

2026-10-07: Kullanıcı son 0.9.3 EXE'nin çalıştığını doğruladı. Bu oyun içi
geri bildirimdir; ayrıntılı replay sayacı veya server hasarı ölçümü kaydedilmedi.

Dağıtım manifesti: `requireAdministrator`, `uiAccess=false`.

# MobTP: ilk geri okuma farkında kalan mobların işlenmemesi — 0.7.3

Canlı kullanım kayıtlarında Home range 100 doğru uygulanıyor: 106 registry üyesinden
103 mob uygun. Ancak `READBACK_MISMATCH_BATCH_STOP`, ilk farklı geri okumada bütün
batch'i durduruyor. Örneğin `tp-20261005-014836-425-0b3950cec8c54411a32177225a895b1f.jsonl`
dosyasında 10 mobdan sonra kalan 92 mob hiç işlenmiyor. Durduran mobun X/Z değerleri
hedefle aynı; B kanalındaki Y 101.872734 yerine 101.925255. Bu küçük yükseklik
değişimi, yakın mobların bile sıraları gelmeden batch'in kesilmesini açıklıyor.
Son 40 dosyadaki 39 durdurmanın 35'inde XZ farkı 0.01 toleransı içinde ve yalnız
Y farklı; kalan dört durdurmada konumun XZ bileşeni de değişmiş.

Yeni davranış:

- Tam XYZ eşleşmesi, yalnız XZ eşleşip Y değişmesi ve konumun geri okumada değişmesi
  ayrı sonuçlardır. Y değişimi tam XYZ başarısı olarak gösterilmez.
- Geçerli kimlik/oturum ve tamamlanmış iki yazımdan sonra bir mobun konumu değişirse
  sonuç kaydedilir; sonraki bağımsız mob taze kontrollerden geçirilir. Otomatik tekrar yazımı yoktur.
- Eksik/kısmi yazım, okunamayan veya geçersiz koordinat, kimlik/Home/oturum/odak
  değişimi ve iptal batch'i durdurur. Geri okumalar sonrasında da kimlik kontrol edilir.
- Sonuç ve JSON kaydı XYZ eşleşen, Y değişen, konumu değişen, atlanan ve hiç
  işlenmeyen mob sayısını ayrı gösterir. Home range filtresi korunur.

30 sentetik test, kayıtlı küçük Y farkını ve ilk mob değişirken sonraki mobların
işlenmesini yeniden üretir; kısmi yazım, geçersiz okuma ve geri okuma sırasında
kimlik kaybının durdurması ayrıca kontrol edilir. Canlı oyuna test yazımı yapılmaz.

Geri okuma, sunucunun taşıma işlemini kabul ettiğini veya konumun kalıcı kaldığını
kanıtlamaz. EXE yönetici UAC manifestini korur.

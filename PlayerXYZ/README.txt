PlayerXYZ 1.1 — profilli koordinat resolver

PlayerXYZ.exe yönetici izni ister. EXE ve profiles klasörünü beraber tutun.
Aynı SHA: profil + sabit nokta doğrulaması; AOB tekrar taranmaz.
Yeni SHA / eski-bozuk profil: owner AOB, iki ayrı XYZ yazım AOB'si ve RTTI ile yeniden çözüm.
Offsetler imza içindeki displacement'lardan çıkarılır. XYZ'nin +4 aralıklı olması,
iki grubun çakışmaması, CTClientChar vtable metot bağlantıları ve canlı kod eşleşmesi doğrulanır.
Tekil çözüm yoksa yazım açılmaz. Yeni session'da heap owner/P yeniden bulunur.
Butona basmak AOB/heap taraması yapmaz; owner->P değişimleri hafif okumalarla kontrol edilir.

Canlı ekranda adres/offsetler yüklenen profile göre gösterilir.
X -> birinci alanlar, Y(yükseklik/H) -> ikinci alanlar, Z(oyun yatay Y) -> üçüncü alanlar.
Koordinatları Yaz tek seferlik yazımdır; freeze veya kod patch yapmaz.

Current build 70/74/78 ve B0/B4/B8 olarak yeniden çözüldü.
8 resolver kontrolü ve 4 veri eşleme kontrolü geçti. Yeni sürümle canlı teleport denenmedi.
Gelecekteki tüm patch'ler garanti edilmez: compiler/layout/RTTI değişimi çözümü durdurabilir.

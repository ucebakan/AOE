# Kullanıcının kalıcı uygulama tercihleri

- 4UnityTools geliştirmelerinde `Suite/AGENTS.md` ürün gereksinimlerini uygula. Butonlar ana görünümü gerçek işlev düğmeleridir; genel bakış kaldırılmıştır. Araç bağlantıları, iş mantığı ve UI yerleşimi ayrı modüllerde tutulur.
- Yerleşimler dinamik, okunabilir ve DPI uyumlu olmalı. Yazı/kontrol çakışması olmamalı; orta boy pencere, tam ekran ve ölçek değişimleri test edilmeli.
- Kullanıcı değişikliklerin topluca tamamlanmasını, derleme ve uygulama kontrollerinin en sonda tek turda yapılmasını tercih ediyor. Her küçük düzenlemeden sonra EXE'yi açıp tüm kontrolleri tekrarlama; son kontrolde somut bir hata çıkarsa yalnız gerekli düzeltme ve ilgili doğrulamayı yap.
- Dağıtılan birleşik EXE her sürümde Windows UAC yönetici onayı gerektirir. `requireAdministrator` manifestini ve derleme doğrulamalarını koru.
- Kullanıcı "git", "gitle", "gitleyelim" veya benzeri ifadeyle Git'e kaydetmeyi istediğinde ilgili değişiklikleri commit edip yapılandırılmış uzak depoya push et. Ayrıca push onayı isteme. Yalnız yerel kayıt/push yapmama yönündeki açık istek önceliklidir. Bu tercih force-push yetkisi vermez.

# BrightTray

[![Release](https://img.shields.io/github/v/release/recepqandemir/BrightTray)](https://github.com/recepqandemir/BrightTray/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-orange.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue.svg)](#indirme)

Twinkle Tray benzeri, sistem tepsisinden çalışan bir Windows parlaklık denetleyicisi.
C# / WPF (.NET 8) ile yazıldı.

## Özellikler

- **Harici monitörler:** DDC/CI (dxva2) ile her ekran için ayrı parlaklık slider'ı
- **Dahili ekran:** Dizüstü panelleri için WMI desteği
- **Genel kısayollar:** Varsayılan `Ctrl+Alt+Up` / `Ctrl+Alt+Down` (tüm ekranlar, adım ayarlanabilir)
- **Zamanlama:** Gün içinde saate göre otomatik parlaklık (örn. 07:00 → %80, 23:00 → %15). İstersen her ekran
  için ayrı liste girilebilir; listesi boş olan ekran “Tüm ekranlar” listesini kullanır
- **Özel ekran adları:** Ayarlar → “Ekran adları” bölümünden her ekrana kendi adını ver
  (örn. “Ofis”, “Salon TV”); panelde bu ad görünür. Boş bırakırsan varsayılan ad kullanılır
- **Parlaklığı hatırlama:** Her ekranın son ayarlanan parlaklığı kaydedilir. Uyku, kilit açma,
  ekran takma/çıkarma gibi yenilemelerden sonra otomatik geri yüklenir (kapatılabilir)
- Tepsi simgesine sol tık: parlaklık paneli, sağ tık: Ayarlar / Ekranları yenile / Çıkış
- Ekran takıldığında/çıkarıldığında ve uykudan dönüşte liste otomatik yenilenir
- İsteğe bağlı: Windows ile birlikte başlat
- **Dil desteği:** Türkçe ve İngilizce. Varsayılan olarak sistem dili kullanılır
  (desteklenmeyen bir dilde İngilizce); Ayarlar'dan elle değiştirilebilir ve anında uygulanır

## Görünüm

Koyu tema, turuncu vurgu. Tüm renkler ve kontrol stilleri `UI/Theme.xaml` içindedir; vurgu rengini
değiştirmek için `AccentBrush`, `AccentHoverBrush`, `AccentPressedBrush` ve `AccentSoftBrush` değerlerini
güncellemek yeterlidir (tepsi simgesinin rengi `App.xaml.cs` içindeki `CreateTrayIcon` yönteminde).

## İndirme

Hazır sürümü [Releases](https://github.com/recepqandemir/BrightTray/releases) sayfasından indirebilirsin:

1. `BrightTray.exe` dosyasını indir.
2. İlk çalıştırma için [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64) kurulu olmalıdır.
3. `BrightTray.exe` dosyasını çalıştır; simge sistem tepsisinde görünür.

## Derleme ve çalıştırma (Windows)

Gereksinim: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
cd BrightTray
dotnet run
```

Tek dosya olarak yayınlamak için:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Çıktı `bin\Release\net8.0-windows\win-x64\publish\` altında oluşur.

## Sürümleme

[Semantic Versioning](https://semver.org/lang/tr/) kullanılır: `MAJOR.MINOR.PATCH`

- **PATCH** (0.3.0 → 0.3.1): yalnızca hata düzeltmesi
- **MINOR** (0.3.0 → 0.4.0): geriye uyumlu yeni özellik
- **MAJOR** (0.x → 1.0.0 ve sonrası): ayar dosyasını veya davranışı bozan değişiklik

Sürüm numarasının tek kaynağı `BrightTray.csproj` içindeki `<Version>` alanıdır. Uygulamadaki sürüm metni
(tepsi menüsünün başlığı ve Ayarlar penceresinin sol altı), dosya özellikleri ve yayın çıktısı buradan gelir.
Değişiklikler `CHANGELOG.md` dosyasında tutulur.

Yeni sürüm çıkarmak için:

1. `CHANGELOG.md` içindeki “Yayınlanmamış” başlığının altındaki notları yeni sürüm başlığına taşıyın (tarihiyle).
2. `BrightTray.csproj` içindeki `<Version>` değerini güncelleyin.
3. Git kullanıyorsanız etiketleyin: `git commit -am "Release 0.4.0"` ardından `git tag v0.4.0`.
4. Yayınlayın (sürüm csproj'dan gelir; gerekirse `-p:Version=0.4.0` ile geçici olarak ezilebilir):

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Çıkan `BrightTray.exe` için sürüm, dosyaya sağ tık → Özellikler → Ayrıntılar'da da görünür.

## Ayarlar

Ayarlar penceresinden değiştirilebilir; dosya olarak şurada tutulur:
`%APPDATA%\BrightTray\settings.json`

Kısayol ifadeleri `Ctrl`, `Alt`, `Shift`, `Win` değiştiricilerinden en az birini ve bir tuşu içermelidir
(örn. `Ctrl+Shift+F11`, `Alt+Up`).

## Yeni dil ekleme

1. `Services/Loc.cs` içindeki `Tables` sözlüğüne, `["en"]` bloğunu kopyalayarak yeni bir dil bloğu ekleyin
   (örn. `["de"] = new() { ... }`). Çevrilmeyen anahtarlar otomatik olarak İngilizceye düşer.
2. Aynı dosyada `Languages` listesine dilin kodunu ve kendi dilindeki adını ekleyin
   (örn. `("de", "Deutsch")`).

Yeni dil, Ayarlar'daki dil listesinde görünür ve sistem dili o dilse otomatik seçilir.
Ayar dosyasında dil `Language` alanında tutulur (`"auto"`, `"tr"`, `"en"`).

## Parlaklık neden sıfırlanıyordu, şimdi ne yapılıyor?

Windows ekran değişikliği bildirdiğinde (uyku, kilit açma, çözünürlük/HDR değişimi, kablo takma)
bazı monitörler kendi parlaklığını varsayılana döndürür ve uygulama da ekran bağlantılarını
baştan kurduğu için eski değeri bilmiyordu. Şimdi:

1. Her ekran, Windows'un verdiği kararlı bir kimlikle tanınır ve son parlaklığı `settings.json`
   içindeki `SavedBrightness` alanına kaydedilir.
2. Yenilemeden sonra kayıtlı değer ekrana yeniden yazılır; DDC/CI hazır değilse birkaç kez denenir.
3. 4 ve 10 saniye sonra donanım okunur; monitör değeri sonradan sıfırlamışsa tekrar yazılır.
4. Uykudan sonra bir ekran geçici olarak yanıt vermezse listeden hemen silinmez, birkaç kez yeniden aranır.
5. Ekran kümesi değişmediyse mevcut nesneler korunur; açık panel kapanmaz ve slider'lar bozulmaz.

Monitörün kendi tuşlarıyla yaptığın parlaklık değişikliği bir sonraki yenilemede ezilmesin istersen
Ayarlar'dan “son parlaklığı geri yükle” seçeneğini kapat.

## Sorun giderme

- **Simgeye tıklayınca panel görünmüyor:** Uygulamayı yönetici olarak çalıştırmak gerekmez ve önerilmez
  (Windows ile otomatik başlatma da yönetici olarak çalışmaz). Panel, simgenin “gizli simgeler” kutusunda
  olması gibi durumlarda odak yarışı nedeniyle kapanabiliyordu; 0.5.4 ile giderildi, 0.5.5 ile panel tıklanan
  ekranda piksel hassasiyetiyle konumlanır. Sürerse simgeye tıkladıktan sonra tepsi menüsünden
  **“Tanılama günlüğünü aç”** deyin; `%APPDATA%\BrightTray\diagnostics.txt` yönetici durumunu, ekranları ve
  panelin açılış olaylarını içerir. Ayrıca `%APPDATA%\BrightTray\error.log` dosyasına bakın.

- **Harici ekran listede yok:** Monitörün kendi ekran menüsünde **DDC/CI** seçeneğini açın,
  ardından tepsi menüsünden “Ekranları yenile”ye basın. Yine olmuyorsa tepsi menüsünden
  **“Tanılama günlüğünü aç”** deyin: günlük her ekran için standart API'nin, yetenek bayraklarının ve ham
  DDC/CI (VCP 0x10) komutunun sonucunu hata koduyla yazar. Aynı dosya `%APPDATA%\BrightTray\monitors.log`
  altındadır.
- Dizüstü bilgisayarın kendi paneli DDC/CI desteklemez (günlükte hata `0x1F` görünür); bu normaldir.
  Panel WMI ile yönetilir ve günlükte “IGNORED (built-in panel)” olarak işaretlenir.
- Fn tuşları veya monitörün kendi düğmeleriyle parlaklığı değiştirirsen, panel açıldığında gerçek değer
  donanımdan okunup gösterilir.
- Aygıt Yöneticisi'nde ekranı “Generic Monitor” olarak görmek normaldir; parlaklık kontrolü sürücüye değil
  monitörün DDC/CI desteğine bağlıdır.
- Gigabyte gibi bazı monitörlerde DDC/CI menüsü (OSD → Sistem/Ayarlar) kapalı gelebilir; HDMI/DP yerine
  USB-C veya dock üzerinden bağlıysa doğrudan DP/HDMI kablosuyla deneyin.
- Bazı dock'lar, USB-C/DisplayLink adaptörleri ve KVM anahtarları DDC/CI komutlarını iletmez.
- Bazı monitörler parlaklık komutunu hiç desteklemez; bu durumda ekran listelenmez.
- Kısayol kaydedilemiyorsa başka bir uygulama aynı tuş kombinasyonunu kullanıyor olabilir.

## Proje yapısı

| Yol | Görev |
| --- | --- |
| `App.xaml(.cs)` | Tepsi simgesi, servislerin bağlanması, ayarların uygulanması |
| `Services/MonitorService.cs` | Ekranları bulur, toplu parlaklık işlemleri |
| `Services/DdcMonitor.cs` | Harici ekran (DDC/CI) |
| `Services/WmiMonitor.cs` | Dahili dizüstü ekranı (WMI) |
| `AppInfo.cs` | Derlemeye gömülü sürüm numarasını okur |
| `CHANGELOG.md` | Sürüm geçmişi |
| `Services/Loc.cs` | Dil tabloları (tr, en) ve metin arama |
| `UI/LocExtension.cs` | XAML'da `{ui:Loc Anahtar}` kullanımı |
| `Services/HotkeyService.cs` | Genel klavye kısayolları |
| `Services/SchedulerService.cs` | Saate göre otomatik parlaklık |
| `UI/FlyoutWindow.xaml` | Tepsi paneli (monitör başına slider) |
| `UI/SettingsWindow.xaml` | Kısayol, zamanlama ve başlangıç ayarları |

## Lisans

[MIT](LICENSE) © 2026 recepqandemir

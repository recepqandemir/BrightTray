# Değişiklik günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/), sürümleme [Semantic Versioning](https://semver.org/lang/tr/) esaslıdır.

## [Yayınlanmamış]

## [0.6.3] - 2026-11-24

### Değişti
- Bellek kullanımı azaltıldı. Uygulama boştayken (panel kapandıktan, ekran taraması veya ayarlar bittikten ~6 sn sonra) çöp toplama yapılır ve çalışma kümesi Windows'a iade edilir.
- WPF çizimi yazılıma alındı (`RenderMode.SoftwareOnly`): GPU/D3D sürücü bileşenleri yüklenmez.
- Çalışma zamanı ayarları: arka plan GC kapalı, dinamik PGO kapalı, `GCConserveMemory` açık.
- Yayınlarken (`-r win-x64`) ReadyToRun etkin: JIT için ayrılan bellek ve açılış süresi azalır.

## [0.6.2] - 2026-11-17

### Eklendi
- Uygulama (EXE) simgesi: turuncu güneş (`app.ico`, 16–256 px). `BrightTray.csproj` içinde `ApplicationIcon` olarak tanımlı.

## [0.6.1] - 2026-11-17

### Düzeltildi
- Ayarlar'daki açılır listelerde (Dil, Zamanlama → Ekran) seçili öğe “LangItem { Code = auto, Name = ... }” gibi ham nesne metniyle görünüyordu; artık yalnızca ad gösterilir.

## [0.6.0] - 2026-11-16

### Eklendi
- Ekran bazlı zamanlama: Ayarlar → Zamanlama bölümünde “Ekran” listesinden bir ekran seçip ona özel saat/parlaklık listesi girilebilir (örn. 1. ekran 09:00 → %50, 19:00 → %23; 2. ekran 08:00 → %60, 19:30 → %30). Kendi listesi boş olan ekran “Tüm ekranlar (varsayılan)” listesini kullanır. Mevcut ayarlar olduğu gibi çalışmaya devam eder.

## [0.5.6] - 2026-11-11

### Düzeltildi
- Panel açılıyor ama 2 piksel genişliğe çöküyordu; bu yüzden slider'lar görünmüyordu (tanılama günlüğüyle bulundu). Panel genişliği (348) artık hem XAML'de hem kodda sabitlenir (Min/MaxWidth).

## [0.5.5] - 2026-11-11

### Düzeltildi
- Panel artık tıklanan ekranın çalışma alanına piksel cinsinden (SetWindowPos) yerleştirilir. Farklı ölçekli iki ekranda (örn. dizüstü 3200x2000 + harici 2560x1440) panelin başka ekrana ya da ekran dışına düşmesi önlenir.

### Değişti
- “Tanılama günlüğünü aç” artık `diagnostics.txt` üretir: yönetici olup olunmadığı, ekranlar, panelin açılış olayları (konum, slider sayısı, odak) ve ekran tarama raporu tek dosyada.

## [0.5.4] - 2026-11-11

### Düzeltildi
- Simgeye tıklayınca panelin görünmemesi (yalnızca normal kullanıcı olarak çalışırken; “Yönetici olarak çalıştır” ile görünüyordu). Özellikle “gizli simgeler” kutusundan tıklanınca Windows odağı geri alıyor ve panel daha açılırken “odağı kaybetti” sayılıp kapanıyordu. Açılıştan sonraki ilk 700 ms içindeki odak kaybı artık yok sayılır, panel öne alınır ve odak ona verilir.
- Panelde slider'ın görünmemesi (olası ikinci neden): 0.5.0'da slider'a eklenen tutamaç gölgesi, uygulama temasından fırça başvurusu ve kalınlık değişikliği geri alındı. Slider, eskiden çalışan sade şablona turuncu sabit renkle döndü.

### Değişti
- Beklenmeyen bir hata oluştuğunda (örn. panel çizilirken) artık sessiz kalınmaz; oturum başına bir kez hata mesajı gösterilir ve ayrıntı `error.log` dosyasına yazılır.

## [0.5.3] - 2026-11-11

### Düzeltildi
- Dizüstü bilgisayarın kendi paneli (DDC/CI desteklemez, WMI ile yönetilir) DDC taramasında yanlışlıkla “okunamayan ekran” sayılıyordu. Bu yüzden panelde sahte kırmızı uyarı çıkıyor ve açılışta tarama gereksiz yere 4 kez tekrarlanıyordu. Dahili paneller WMI'dan (`VideoOutputTechnology`) tanınır ve DDC taramasında yok sayılır.
- Panel, gerçek parlaklık yerine uygulamanın son yazdığı değeri gösteriyordu; Fn tuşları, monitörün kendi düğmeleri veya Windows ayarlarıyla yapılan değişiklikler görünmüyordu. Panel açılınca gerçek değer donanımdan okunup slider'lara yansıtılır.

### Değişti
- Tanılama günlüğüne “Built-in display keys (WMI)” satırı ve “IGNORED (built-in panel)” durumu eklendi.

## [0.5.2] - 2026-11-11

### Düzeltildi
- Panel, harici ekranların DDC/CI taraması bitene kadar bilgi gösteremiyordu. Dahili ekran artık önce ve hemen gösterilir; harici ekranlar bulundukça panel kapanmadan yenilenir.
- DDC/CI'ya hiç yanıt vermeyen bir ekran, her ekran olayında taramayı birkaç kez tekrarlatıp paneli yavaşlatıyordu. Yeniden deneme artık yalnızca daha önce çalışan bir ekran kaybolduğunda ve açılıştaki ilk taramada yapılır; deneme sayısı da azaltıldı.

### Eklendi
- Panel açılırken bir hata olursa sessiz kalmak yerine mesaj gösterilir ve `error.log` dosyasına yazılır.

## [0.5.1] - 2026-11-11

### Düzeltildi
- Bir ekran DDC/CI'ya yanıt vermediğinde (atlandığında) ilk ekran listesi, yeniden denemeler bitene kadar (~13 sn) yayınlanmıyor ve panel boş görünüyordu. Liste boşken bulunan ekranlar artık hemen gösterilir.
- Panel açıkken tarama bitince pencerenin kapanması; artık yerinde güncellenir.

### Eklendi
- Panelde tarama sürerken “Ekranlar taranıyor…” durumu.
- Beklenmeyen hatalar `%APPDATA%\BrightTray\error.log` dosyasına yazılır.

## [0.5.0] - 2026-11-11

### Değişti
- Yeni görünüm: koyu zemin üzerinde turuncu vurgu (önceki sarının yerine). Panel, tepsi simgesi ve Ayarlar penceresi aynı temayı kullanır.
- Düğmeler, metin kutuları, onay kutuları, açılır liste, tablo ve kaydırma çubukları yuvarlak köşeli modern stillerle yenilendi (`UI/Theme.xaml`). Vurgu rengini değiştirmek için `Accent*` fırçalarını düzenlemek yeterlidir.
- Ayarlar penceresi kartlara bölündü; sürüm ve Kaydet/İptal düğmeleri alt çubukta her zaman görünür, küçük ekranlarda içerik kaydırılır.
- Ayarlar penceresinin başlık çubuğu koyu temaya uyar (Windows 10 20H1 ve sonrası).
- Panelde slider çizgisi kalınlaştı, tutamaç büyüdü.

## [0.4.0] - 2026-11-04

### Eklendi
- Yedek DDC/CI yolu: standart parlaklık API'sine yanıt vermeyen monitörler için ham VCP 0x10 komutu denenir.
- Tanılama günlüğü: tepsi menüsünde “Tanılama günlüğünü aç”. Her ekranın neden eklendiğini ya da atlandığını (hata kodlarıyla) gösterir; dosya `%APPDATA%\BrightTray\monitors.log` altında tutulur.
- Parlaklığı okunamayan ekran varsa panelde uyarı gösterilir (artık sessizce listeden düşmez).

### Değişti
- Bir ekran atlandığında (örn. Windows açılışında DDC/CI henüz hazır değilken) tarama kabul edilmeden önce birkaç kez yeniden denenir.

## [0.3.1] - 2026-11-01

### Düzeltildi
- Proje klasörünün içinde eski bir kopya (örn. `_BrightTray_v1.0`) bulunduğunda derlemenin “already contains a definition” hatalarıyla başarısız olması. `_` ile başlayan klasörler artık derlemeye dahil edilmez.

## [0.3.0] - 2026-11-01

### Eklendi
- Her ekrana kendi adını verme (Ayarlar → Ekran adları).
- Son parlaklığı ekran başına kaydetme ve ekranlar yenilendiğinde geri yükleme (kapatılabilir).
- Sürüm numarası: tepsi menüsünde ve Ayarlar penceresinde gösterilir.
- Ekran kilidi açılınca ve uzak oturumdan dönünce ekran listesi yenilenir.

### Düzeltildi
- Uyku, kilit açma veya ekran değişikliğinden sonra parlaklığın varsayılana dönmesi.
- Ekranlar yenilendiğinde açık panelin slider'larının çalışmaz hale gelmesi.
- Uykudan sonra geçici olarak yanıt vermeyen ekranın listeden silinmesi (artık birkaç kez yeniden aranır).

### Değişti
- Ekranlar Windows'un verdiği kararlı kimlikle tanınır (sıra numarasına bağlı değil).

## [0.2.0] - 2026-10-31

### Eklendi
- Dil desteği: Türkçe ve İngilizce, sistem diline göre otomatik seçim ve Ayarlar'dan elle değiştirme.

## [0.1.0] - 2026-10-31

### Eklendi
- İlk sürüm: sistem tepsisi paneli, harici ekranlar için DDC/CI ve dahili ekran için WMI parlaklık kontrolü.
- Genel klavye kısayolları ve saate göre zamanlama.
- Windows ile birlikte başlatma seçeneği.

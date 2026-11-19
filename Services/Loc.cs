using System.Collections.Generic;
using System.Globalization;

namespace BrightTray.Services;

/// <summary>
/// Basit yerelleştirme. Yeni dil eklemek için aşağıya bir sözlük ekleyip
/// <see cref="Languages"/> listesine kodunu ve kendi adını yazmak yeterli.
/// Eksik anahtarlar İngilizceye, o da yoksa anahtarın kendisine düşer.
/// </summary>
public static class Loc
{
    private const string Fallback = "en";

    /// <summary>Ayarlar penceresinde gösterilen diller (kod, kendi dilindeki ad).</summary>
    public static IReadOnlyList<(string Code, string Name)> Languages { get; } = new[]
    {
        ("tr", "Türkçe"),
        ("en", "English"),
    };

    public static string Code { get; private set; } = Fallback;

    /// <summary>"auto" sistem dilini seçer; desteklenmeyen bir sistem dilinde İngilizce kullanılır.</summary>
    public static void Set(string? setting)
    {
        if (!string.IsNullOrWhiteSpace(setting) && Tables.ContainsKey(setting))
        {
            Code = setting;
            return;
        }

        string system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Code = Tables.ContainsKey(system) ? system : Fallback;
    }

    public static string T(string key)
    {
        if (Tables[Code].TryGetValue(key, out var value)) return value;
        if (Tables[Fallback].TryGetValue(key, out var fallback)) return fallback;
        return key;
    }

    public static string T(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["tr"] = new()
        {
            // Panel
            ["Brightness"] = "Parlaklık",
            ["NoMonitors"] = "Parlaklığı kontrol edilebilen ekran bulunamadı. Monitörün kendi menüsünde DDC/CI seçeneğinin açık olduğundan emin olun, sonra tepsi simgesine sağ tıklayıp “Ekranları yenile” seçin.",

            // Tepsi
            ["MenuSettings"] = "Ayarlar…",
            ["MenuRefresh"] = "Ekranları yenile",
            ["MenuExit"] = "Çıkış",
            ["Scanning"] = "Ekranlar taranıyor…",
            ["PanelError"] = "Parlaklık paneli açılamadı. Ayrıntı error.log dosyasına yazıldı.",
            ["UnexpectedError"] = "Beklenmeyen bir hata oluştu. Ayrıntı error.log dosyasına yazıldı.",
            ["MenuDiagnostics"] = "Tanılama günlüğünü aç",
            ["SkippedHint"] = "{0} ekranın parlaklığı okunamadı (DDC/CI yanıt vermiyor). Ayrıntı için tepsi menüsünden “Tanılama günlüğünü aç” seçeneğine bakın.",
            ["TrayNoMonitor"] = "BrightTray · ekran bulunamadı",
            ["TrayBrightness"] = "BrightTray · parlaklık {0}%",
            ["HotkeyFailed"] = "“{0}” kısayolu kaydedilemedi. Başka bir uygulama kullanıyor olabilir; Ayarlar'dan değiştirin.",

            // Ekran adları
            ["InternalDisplay"] = "Dahili ekran",
            ["ExternalDisplay"] = "Harici ekran",
            ["MonitorN"] = "Monitör {0}",

            // Ayarlar penceresi
            ["SettingsTitle"] = "BrightTray Ayarları",
            ["Version"] = "Sürüm {0}",
            ["Language"] = "Dil",
            ["LangAuto"] = "Otomatik (sistem dili)",
            ["General"] = "Genel",
            ["DisplayNames"] = "Ekran adları",
            ["DisplayNamesHint"] = "Ekranlara kendi adınızı verin (örn. “Ofis”, “Salon TV”). Boş bırakırsanız varsayılan ad kullanılır.",
            ["RestoreBrightness"] = "Ekranlar yenilendiğinde veya bilgisayar uykudan döndüğünde son parlaklığı geri yükle",
            ["Hotkeys"] = "Klavye kısayolları",
            ["HotkeyUp"] = "Parlaklığı artır",
            ["HotkeyDown"] = "Parlaklığı azalt",
            ["Step"] = "Adım (%)",
            ["Schedule"] = "Zamanlama",
            ["ScheduleEnable"] = "Zamanlamayı etkinleştir",
            ["ApplyOnStartup"] = "Açılışta en son geçerli değeri hemen uygula",
            ["ColTime"] = "Saat (SS:DD)",
            ["ColBrightness"] = "Parlaklık (0-100)",
            ["ScheduleTarget"] = "Ekran",
            ["AllDisplays"] = "Tüm ekranlar (varsayılan)",
            ["ScheduleTargetHint"] = "Bir ekran için kendi listesini doldurursan o ekran yalnızca onu kullanır. Boş bırakırsan “Tüm ekranlar” listesi geçerli olur.",
            ["ScheduleHint"] = "Satır eklemek için tablonun boş son satırına yazın; silmek için satırı seçip Delete tuşuna basın.",
            ["RunAtStartup"] = "Windows ile birlikte başlat",
            ["Save"] = "Kaydet",
            ["Cancel"] = "İptal",

            // Doğrulama uyarıları
            ["WarnStep"] = "Adım 1 ile 50 arasında bir sayı olmalı.",
            ["WarnUp"] = "“Artır” kısayolu geçersiz. Örnek: Ctrl+Alt+Up",
            ["WarnDown"] = "“Azalt” kısayolu geçersiz. Örnek: Ctrl+Alt+Down",
            ["WarnTime"] = "“{0}” geçerli bir saat değil. SS:DD biçimini kullanın (örn. 07:30).",
            ["WarnBrightness"] = "Parlaklık 0 ile 100 arasında olmalı.",
        },

        ["en"] = new()
        {
            // Flyout
            ["Brightness"] = "Brightness",
            ["NoMonitors"] = "No controllable display was found. Make sure DDC/CI is enabled in the monitor's own menu, then right-click the tray icon and choose “Refresh displays”.",

            // Tray
            ["MenuSettings"] = "Settings…",
            ["MenuRefresh"] = "Refresh displays",
            ["MenuExit"] = "Exit",
            ["Scanning"] = "Scanning displays…",
            ["PanelError"] = "The brightness panel could not be opened. Details were written to error.log.",
            ["UnexpectedError"] = "An unexpected error occurred. Details were written to error.log.",
            ["MenuDiagnostics"] = "Open diagnostics log",
            ["SkippedHint"] = "Could not read brightness from {0} display(s) (no DDC/CI response). See “Open diagnostics log” in the tray menu for details.",
            ["TrayNoMonitor"] = "BrightTray · no display found",
            ["TrayBrightness"] = "BrightTray · brightness {0}%",
            ["HotkeyFailed"] = "Could not register the “{0}” hotkey. Another app may be using it; change it in Settings.",

            // Display names
            ["InternalDisplay"] = "Built-in display",
            ["ExternalDisplay"] = "External display",
            ["MonitorN"] = "Monitor {0}",

            // Settings window
            ["SettingsTitle"] = "BrightTray Settings",
            ["Version"] = "Version {0}",
            ["Language"] = "Language",
            ["LangAuto"] = "Automatic (system language)",
            ["General"] = "General",
            ["DisplayNames"] = "Display names",
            ["DisplayNamesHint"] = "Give your displays your own names (e.g. “Office”, “Living room TV”). Leave empty to use the default name.",
            ["RestoreBrightness"] = "Restore the last brightness when displays refresh or the PC wakes up",
            ["Hotkeys"] = "Keyboard shortcuts",
            ["HotkeyUp"] = "Increase brightness",
            ["HotkeyDown"] = "Decrease brightness",
            ["Step"] = "Step (%)",
            ["Schedule"] = "Schedule",
            ["ScheduleEnable"] = "Enable schedule",
            ["ApplyOnStartup"] = "Apply the current scheduled value on startup",
            ["ColTime"] = "Time (HH:MM)",
            ["ColBrightness"] = "Brightness (0-100)",
            ["ScheduleTarget"] = "Display",
            ["AllDisplays"] = "All displays (default)",
            ["ScheduleTargetHint"] = "If you fill in a list for a display, that display uses only it. If you leave it empty, the “All displays” list applies.",
            ["ScheduleHint"] = "To add a row, type in the empty last row of the table; to delete one, select it and press Delete.",
            ["RunAtStartup"] = "Start with Windows",
            ["Save"] = "Save",
            ["Cancel"] = "Cancel",

            // Validation warnings
            ["WarnStep"] = "Step must be a number between 1 and 50.",
            ["WarnUp"] = "The “increase” shortcut is invalid. Example: Ctrl+Alt+Up",
            ["WarnDown"] = "The “decrease” shortcut is invalid. Example: Ctrl+Alt+Down",
            ["WarnTime"] = "“{0}” is not a valid time. Use HH:MM (e.g. 07:30).",
            ["WarnBrightness"] = "Brightness must be between 0 and 100.",
        },
    };
}

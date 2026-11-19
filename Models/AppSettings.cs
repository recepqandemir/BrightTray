using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace BrightTray.Models;

/// <summary>Zamanlamadaki tek bir satır: belirli saatte uygulanacak parlaklık.</summary>
public sealed class ScheduleEntry
{
    /// <summary>"HH:mm" biçiminde saat.</summary>
    public string Time { get; set; } = "08:00";

    /// <summary>0-100 arası parlaklık yüzdesi.</summary>
    public int Brightness { get; set; } = 70;

    public bool TryGetTime(out TimeSpan time) =>
        TimeSpan.TryParseExact(Time?.Trim(), "h\\:mm", CultureInfo.InvariantCulture, out time)
        && time >= TimeSpan.Zero && time < TimeSpan.FromHours(24);
}

public sealed class AppSettings
{
    /// <summary>Klavye kısayolu başına parlaklık adımı (yüzde).</summary>
    public int HotkeyStep { get; set; } = 10;

    public string HotkeyUp { get; set; } = "Ctrl+Alt+Up";
    public string HotkeyDown { get; set; } = "Ctrl+Alt+Down";

    public bool ScheduleEnabled { get; set; } = false;

    /// <summary>Uygulama açılırken zamanlamadaki en son geçmiş değeri hemen uygula.</summary>
    public bool ApplyScheduleOnStartup { get; set; } = true;

    public bool RunAtStartup { get; set; } = false;

    /// <summary>
    /// Ekranlar yenilendiğinde (uyku, kilit açma, çözünürlük değişimi...) monitör kendi değerine
    /// dönmüş olabilir; açıksa en son ayarlanan parlaklık geri yüklenir.
    /// </summary>
    public bool RestoreBrightness { get; set; } = true;

    /// <summary>Ekran kimliği → en son ayarlanan parlaklık (0-100). Program tarafından yönetilir.</summary>
    public Dictionary<string, int> SavedBrightness { get; set; } = new();

    /// <summary>Ekran kimliği → kullanıcının verdiği ad.</summary>
    public Dictionary<string, string> CustomNames { get; set; } = new();

    /// <summary>"auto" (sistem dili), "tr", "en"...</summary>
    public string Language { get; set; } = "auto";

    public List<ScheduleEntry> Schedule { get; set; } = new()
    {
        new ScheduleEntry { Time = "07:00", Brightness = 80 },
        new ScheduleEntry { Time = "20:00", Brightness = 40 },
        new ScheduleEntry { Time = "23:00", Brightness = 15 },
    };

    /// <summary>
    /// Ekran kimliği → o ekrana özel zamanlama. Listesi olmayan (veya boş) ekranlar yukarıdaki genel
    /// <see cref="Schedule"/> listesini kullanır.
    /// </summary>
    public Dictionary<string, List<ScheduleEntry>> MonitorSchedules { get; set; } = new();

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this)) ?? new AppSettings();
}

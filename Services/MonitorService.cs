using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BrightTray.Models;
using BrightTray.Native;

namespace BrightTray.Services;

/// <summary>Bağlı ekranları bulur, parlaklığı hatırlar ve ekranlar yenilendiğinde geri yükler.</summary>
public sealed class MonitorService : IDisposable
{
    private sealed record ScanResult(List<MonitorDevice> Devices, int Skipped, string Report);

    private List<MonitorDevice> _monitors = new();
    private bool _disposed;
    private bool _scannedOnce;

    /// <summary>Kayıtlı parlaklıklar ve özel adlar burada tutulur. App, ayarlar değiştikçe günceller.</summary>
    public AppSettings Settings { get; set; } = new();

    public IReadOnlyList<MonitorDevice> Monitors => _monitors;

    /// <summary>Windows'ta görünen ama DDC/CI ile parlaklığı okunamadığı için listeye alınmayan ekran sayısı.</summary>
    public int SkippedCount { get; private set; }

    /// <summary>Şu anda tarama (veya yeniden deneme) sürüyor mu? Panel "taranıyor" göstermek için kullanır.</summary>
    public bool IsScanning { get; private set; }

    /// <summary>Son taramanın ayrıntılı raporu (hangi ekran neden eklendi ya da atlandı).</summary>
    public string LastReport { get; private set; } = "";

    /// <summary>Rapor bu dosyaya da yazılır (ayarlanmazsa yazılmaz).</summary>
    public string? ReportPath { get; set; }

    /// <summary>Parlaklık program tarafından (kısayol / zamanlama) değiştirildiğinde tetiklenir.</summary>
    public event Action? BrightnessChanged;

    /// <summary>
    /// Ekran listesi yenilendiğinde tetiklenir. Parametre: ekran kümesi değiştiyse true
    /// (aynıysa mevcut nesneler korunmuştur ve açık panel olduğu gibi kullanılabilir).
    /// </summary>
    public event Action<bool>? MonitorsRefreshed;

    /// <summary>Kaydedilmesi gereken bir ayar (SavedBrightness) değiştiğinde tetiklenir.</summary>
    public event Action? StateChanged;

    public int AveragePercent =>
        _monitors.Count == 0 ? 0 : (int)Math.Round(_monitors.Average(m => m.Percent));

    /// <summary>DDC/CI sorguları yavaş olabilir; arayüzü kilitlememek için arka planda çalışır.</summary>
    public async Task RefreshAsync()
    {
        IsScanning = true;
        bool firstRun = !_scannedOnce;

        try
        {
            // Liste boşsa önce yalnızca dahili ekranı (WMI, hızlı) göster. Harici ekranların DDC/CI taraması
            // yanıt vermeyen bir monitör yüzünden saniyelerce sürebilir; panel o sürede boş kalmasın.
            if (_monitors.Count == 0)
            {
                var quick = await Task.Run(() => WmiMonitor.TryCreate());
                if (_disposed)
                    return;

                if (quick != null)
                    Publish(new ScanResult(new List<MonitorDevice> { quick }, 0, ""), 0);
            }

            for (int attempt = 0; ; attempt++)
            {
                var scan = await Task.Run(Enumerate);

                if (_disposed)
                {
                    foreach (var m in scan.Devices)
                        m.Dispose();
                    return;
                }

                // Yeniden deneme yalnızca anlamlı olduğunda: daha önce çalışan bir ekran eksik göründüğünde (uyku
                // sonrası DDC/CI birkaç saniye geç uyanabilir) ya da Windows açılışındaki ilk taramada.
                // Hiç çalışmamış bir ekran için her ekran olayında tekrar tekrar denemek paneli yavaşlatır;
                // o durumda tepsi menüsündeki "Ekranları yenile" kullanılır.
                bool missing = scan.Devices.Count < _monitors.Count;
                bool needRetry = missing || (firstRun && scan.Skipped > 0);
                bool last = !needRetry || attempt >= 2;
                IsScanning = !last;

                // Liste hâlâ boşsa (uygulama yeni açıldı) bulunanları beklemeden göster. Aksi halde, bir ekran geçici
                // olarak yanıt vermiyor olabileceği için mevcut liste tarama bitene kadar korunur.
                if (last || _monitors.Count == 0)
                {
                    Publish(scan, attempt + 1);
                }
                else
                {
                    foreach (var m in scan.Devices)
                        m.Dispose();
                }

                if (last)
                    return;

                await Task.Delay(2000);
                if (_disposed)
                    return;
            }
        }
        finally
        {
            IsScanning = false;
            _scannedOnce = true;
        }
    }

    /// <summary>Tarama sonucunu yayınlar: aynı ekranların nesnelerini korur, yenileri ekler, kaybolanları kapatır.</summary>
    private void Publish(ScanResult scan, int attempts)
    {
        var fresh = scan.Devices;
        SkippedCount = scan.Skipped;

        // Ön yayın (yalnızca dahili ekran) raporsuzdur; asıl tarama sonucu raporu yazar.
        if (scan.Report.Length > 0)
        {
            LastReport = scan.Report + $"Scan attempts: {attempts}{Environment.NewLine}";
            WriteReport();
        }

        var oldIds = _monitors.Select(m => m.Id).ToList();
        var leftovers = new List<MonitorDevice>(_monitors);
        var merged = new List<MonitorDevice>();

        foreach (var f in fresh)
        {
            // Aynı ekran zaten varsa nesneyi koru, yalnızca taze bağlantıyı ona devret.
            var existing = leftovers.FirstOrDefault(m => m.Id == f.Id);
            if (existing != null && existing.Adopt(f))
            {
                leftovers.Remove(existing);
                existing.DefaultName = f.DefaultName;
                f.Dispose();
                merged.Add(existing);
            }
            else
            {
                f.PercentRequested += OnPercentRequested;
                merged.Add(f);
            }
        }

        foreach (var gone in leftovers)
            gone.Dispose();

        _monitors = merged;

        bool changed = !oldIds.SequenceEqual(merged.Select(m => m.Id));

        ApplyNames();
        RestoreBrightness();

        MonitorsRefreshed?.Invoke(changed);

        _ = VerifyAsync(merged);
    }

    /// <summary>
    /// Gerçek parlaklığı donanımdan okuyup sapma varsa (Fn tuşları, monitörün kendi düğmeleri, Windows ayarları)
    /// uygulamanın bildiği değeri günceller; cihaza yazmaz. Bir değer değiştiyse true döner.
    /// </summary>
    public async Task<bool> SyncFromHardwareAsync()
    {
        var devices = _monitors;
        bool changed = false;

        foreach (var m in devices)
        {
            int? actual = await Task.Run(() => m.ReadActualPercent());

            if (_disposed || !ReferenceEquals(devices, _monitors))
                return changed;

            // 2 puandan küçük farklar ham değerin yuvarlanmasından kaynaklanır; yok say.
            if (actual is int a && Math.Abs(a - m.Percent) > 2 && m.TryAdoptHardwarePercent(a))
            {
                Settings.SavedBrightness[m.Id] = m.Percent;
                changed = true;
            }
        }

        if (changed)
            StateChanged?.Invoke();

        return changed;
    }

    /// <summary>Ayarlardaki özel adları ekranlara uygular.</summary>
    public void ApplyNames()
    {
        foreach (var m in _monitors)
            m.CustomName = Settings.CustomNames.TryGetValue(m.Id, out var name) ? name : null;
    }

    public void SetAll(int percent)
    {
        foreach (var m in _monitors)
            m.RequestPercent(percent);
        BrightnessChanged?.Invoke();
    }

    /// <summary>Tek bir ekranın parlaklığını kimliğiyle ayarlar (ekran bağlı değilse yok sayılır).</summary>
    public void SetOne(string id, int percent)
    {
        foreach (var m in _monitors)
        {
            if (m.Id == id)
            {
                m.RequestPercent(percent);
                BrightnessChanged?.Invoke();
                return;
            }
        }
    }

    public void AdjustAll(int delta)
    {
        foreach (var m in _monitors)
            m.RequestPercent(m.Percent + delta);
        BrightnessChanged?.Invoke();
    }

    private void OnPercentRequested(MonitorDevice device)
    {
        Settings.SavedBrightness[device.Id] = device.Percent;
        StateChanged?.Invoke();
    }

    private void WriteReport()
    {
        if (string.IsNullOrEmpty(ReportPath))
            return;

        try
        {
            File.WriteAllText(ReportPath, LastReport, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch
        {
            // Rapor yazılamazsa uygulama etkilenmesin.
        }
    }

    /// <summary>
    /// Her yenilemeden sonra kayıtlı değeri ekrana yeniden yazar (monitör varsayılana dönmüş olabilir).
    /// Kaydı olmayan ekran için donanımdan okunan değer ilk kayıt olur.
    /// </summary>
    private void RestoreBrightness()
    {
        bool dirty = false;

        foreach (var m in _monitors)
        {
            if (Settings.SavedBrightness.TryGetValue(m.Id, out int saved))
            {
                if (Settings.RestoreBrightness)
                    m.RequestPercent(saved, notify: false);
            }
            else
            {
                Settings.SavedBrightness[m.Id] = m.Percent;
                dirty = true;
            }
        }

        if (dirty)
            StateChanged?.Invoke();
    }

    /// <summary>
    /// Bazı monitörler uykudan sonra, DDC/CI komutunu kabul ettikten birkaç saniye sonra bile varsayılana döner.
    /// Bu yüzden kısa süre sonra donanımı okuyup hedeften sapmışsa değeri yeniden yazar.
    /// </summary>
    private async Task VerifyAsync(List<MonitorDevice> devices)
    {
        if (!Settings.RestoreBrightness)
            return;

        foreach (int delayMs in new[] { 4000, 10000 })
        {
            await Task.Delay(delayMs);

            // Bu arada yeni bir yenileme olduysa o kendi doğrulamasını yapar.
            if (_disposed || !ReferenceEquals(devices, _monitors))
                return;

            foreach (var m in devices)
            {
                int target = m.Percent;
                int? actual = await Task.Run(() => m.ReadPercent());

                if (actual is int a && Math.Abs(a - target) > 3 && ReferenceEquals(devices, _monitors))
                    m.RequestPercent(target, notify: false);
            }
        }
    }

    // ------------------------------------------------------------------ tarama

    private static string Err() => $"error 0x{Marshal.GetLastWin32Error():X}";

    private static ScanResult Enumerate()
    {
        var list = new List<MonitorDevice>();
        var log = new StringBuilder();
        int skipped = 0;
        int index = 0;
        int displayNo = 0;

        log.AppendLine($"BrightTray {AppInfo.Version} · {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {Environment.OSVersion}");
        log.AppendLine();

        var internalPanel = WmiMonitor.TryCreate();
        if (internalPanel != null)
        {
            list.Add(internalPanel);
            log.AppendLine($"Internal panel (WMI): found, {internalPanel.Percent}%");
        }
        else
        {
            log.AppendLine("Internal panel (WMI): none");
        }

        // Dizüstü bilgisayarın kendi paneli DDC/CI desteklemez; DDC taramasında başarısız sayılmasın.
        var internalKeys = WmiMonitor.GetInternalDisplayKeys();
        log.AppendLine($"Built-in display keys (WMI): {(internalKeys.Count == 0 ? "none" : string.Join(", ", internalKeys))}");
        log.AppendLine();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                displayNo++;
                var (deviceName, interfaceIds) = GetAdapterInfo(hMonitor);

                log.AppendLine(
                    $"Display #{displayNo}: {deviceName}  {rect.Right - rect.Left}x{rect.Bottom - rect.Top} at {rect.Left},{rect.Top}");

                if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count))
                {
                    log.AppendLine($"  GetNumberOfPhysicalMonitors failed ({Err()})");
                    skipped++;
                    log.AppendLine();
                    return true;
                }

                log.AppendLine($"  physical monitors: {count}");
                if (count == 0)
                {
                    log.AppendLine();
                    return true;
                }

                var physical = new NativeMethods.PHYSICAL_MONITOR[count];
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
                {
                    log.AppendLine($"  GetPhysicalMonitorsFromHMONITOR failed ({Err()})");
                    skipped++;
                    log.AppendLine();
                    return true;
                }

                for (int j = 0; j < physical.Length; j++)
                {
                    var pm = physical[j];
                    index++;

                    // Kararlı kimlik: Windows'un monitör arayüz yolu. Bulunamazsa ad + sıra numarası.
                    string id = j < interfaceIds.Count && interfaceIds[j].Length > 0
                        ? "ddc:" + interfaceIds[j]
                        : $"ddc:{pm.szPhysicalMonitorDescription}:{index}";

                    log.AppendLine($"  [{j}] \"{pm.szPhysicalMonitorDescription}\"");
                    log.AppendLine($"      id: {id}");

                    // Dahili panel: DDC/CI denenmez, başarısız sayılmaz ve panelde uyarı çıkarmaz (WMI ile yönetilir).
                    string? key = KeyFromId(id);
                    if (key != null && internalKeys.Contains(key))
                    {
                        NativeMethods.DestroyPhysicalMonitors(1, new[] { pm });
                        log.AppendLine("      => IGNORED (built-in panel, controlled via WMI)");
                        continue;
                    }

                    if (Probe(pm.hPhysicalMonitor, log, out bool useVcp, out uint min, out uint cur, out uint max))
                    {
                        list.Add(new DdcMonitor(pm, id, index, min, cur, max, useVcp));
                        log.AppendLine($"      => ADDED ({(useVcp ? "raw VCP 0x10" : "brightness API")})");
                    }
                    else
                    {
                        skipped++;
                        NativeMethods.DestroyPhysicalMonitors(1, new[] { pm });
                        log.AppendLine("      => SKIPPED (no DDC/CI brightness response)");
                    }
                }

                log.AppendLine();
                return true;
            }, IntPtr.Zero);

        log.AppendLine($"Result: {list.Count} controllable (including internal), {skipped} skipped");

        return new ScanResult(list, skipped, log.ToString());
    }

    /// <summary>
    /// "ddc:\\?\display#cso1626#4&amp;1009&amp;2&amp;uid8388688#{guid}" -> "cso1626\4&amp;1009&amp;2&amp;uid8388688".
    /// WMI'daki InstanceName ile karşılaştırılabilen anahtar; biçim tanınmazsa null.
    /// </summary>
    private static string? KeyFromId(string id)
    {
        const string marker = @"\display#";

        int i = id.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            return null;

        string rest = id.Substring(i + marker.Length);

        int end = rest.IndexOf("#{", StringComparison.Ordinal);
        if (end >= 0)
            rest = rest.Substring(0, end);

        return rest.Replace('#', '\\').ToLowerInvariant();
    }

    /// <summary>
    /// Önce standart parlaklık API'sini, olmazsa ham DDC/CI (VCP 0x10) komutunu dener.
    /// Gigabyte gibi bazı monitörler standart API'ye yanıt vermese de ham komuta yanıt verir.
    /// </summary>
    private static bool Probe(IntPtr handle, StringBuilder log,
        out bool useVcp, out uint min, out uint cur, out uint max)
    {
        useVcp = false;
        min = 0;
        cur = 0;
        max = 0;

        if (NativeMethods.GetMonitorCapabilities(handle, out uint caps, out _))
            log.AppendLine($"      capabilities: 0x{caps:X} (brightness flag: {((caps & 0x10) != 0 ? "yes" : "no")})");
        else
            log.AppendLine($"      capabilities: failed ({Err()})");

        for (int attempt = 1; attempt <= 1; attempt++)
        {
            if (NativeMethods.GetMonitorBrightness(handle, out min, out cur, out max) && max > min)
            {
                log.AppendLine($"      brightness API: ok (min {min}, current {cur}, max {max})");
                return true;
            }

            log.AppendLine($"      brightness API: failed, try {attempt} ({Err()})");
        }

        min = 0;

        for (int attempt = 1; attempt <= 1; attempt++)
        {
            if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(
                    handle, NativeMethods.VCP_BRIGHTNESS, IntPtr.Zero, out cur, out max) && max > 0)
            {
                useVcp = true;
                log.AppendLine($"      VCP 0x10: ok (current {cur}, max {max})");
                return true;
            }

            log.AppendLine($"      VCP 0x10: failed, try {attempt} ({Err()})");
        }

        return false;
    }

    /// <summary>Bir HMONITOR'ın bağlı olduğu ekran adı (\\.\DISPLAYn) ve etkin monitörlerinin kararlı arayüz yolları.</summary>
    private static (string DeviceName, List<string> InterfaceIds) GetAdapterInfo(IntPtr hMonitor)
    {
        var ids = new List<string>();

        var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            return ("?", ids);

        for (uint i = 0; i < 16; i++)
        {
            var dd = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
            if (!NativeMethods.EnumDisplayDevices(info.szDevice, i, ref dd, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME))
                break;

            if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_ACTIVE) == 0)
                continue;

            ids.Add((dd.DeviceID ?? "").ToLowerInvariant());
        }

        return (info.szDevice, ids);
    }

    public void Dispose()
    {
        _disposed = true;

        foreach (var m in _monitors)
            m.Dispose();
        _monitors = new List<MonitorDevice>();
    }
}

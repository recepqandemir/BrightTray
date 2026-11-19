using System;
using System.Collections.Generic;
using System.Management;

namespace BrightTray.Services;

/// <summary>Dizüstü bilgisayarın dahili ekranı (WMI: WmiMonitorBrightness).</summary>
internal sealed class WmiMonitor : MonitorDevice
{
    private WmiMonitor(int percent)
    {
        Id = "wmi:internal";
        DefaultName = Loc.T("InternalDisplay");
        IsInternal = true;
        Percent = Math.Clamp(percent, 0, 100);
    }

    /// <summary>Dahili ekran yoksa (masaüstü PC) null döner.</summary>
    public static WmiMonitor? TryCreate()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE");

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                using (mo)
                {
                    return new WmiMonitor(Convert.ToInt32(mo["CurrentBrightness"]));
                }
            }
        }
        catch
        {
            // WMI erişilemiyor veya dahili ekran yok.
        }

        return null;
    }

    protected override void Apply(int percent)
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE");

        foreach (ManagementObject mo in searcher.Get())
        {
            using (mo)
            {
                // Parametreler: Timeout (uint32), Brightness (uint8)
                mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)Math.Clamp(percent, 0, 100) });
            }
        }
    }

    /// <summary>Panel açıldığında gerçek parlaklığı okur (Fn tuşları vb. ile değişmiş olabilir).</summary>
    internal override int? ReadActualPercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE");

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                using (mo)
                {
                    return Math.Clamp(Convert.ToInt32(mo["CurrentBrightness"]), 0, 100);
                }
            }
        }
        catch
        {
            // Okunamazsa mevcut değer kalır.
        }

        return null;
    }

    /// <summary>
    /// Dizüstü bilgisayarın kendi panellerinin anahtarları ("csot1626\4&amp;...&amp;uid..." biçiminde, küçük harf).
    /// Bu paneller DDC/CI desteklemez; WMI ile yönetilirler. DDC taramasında başarısız sayılmamaları için kullanılır.
    /// </summary>
    public static HashSet<string> GetInternalDisplayKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT InstanceName, VideoOutputTechnology FROM WmiMonitorConnectionParams");

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                using (mo)
                {
                    long tech = Convert.ToInt64(mo["VideoOutputTechnology"]);
                    if (tech < 0)
                        tech += 1L << 32;

                    // 0x80000000: dahili, 11: gömülü DisplayPort, 13: gömülü UDI
                    if (tech != 0x80000000L && tech != 11 && tech != 13)
                        continue;

                    string name = Convert.ToString(mo["InstanceName"]) ?? "";

                    // "DISPLAY\CSO1626\4&10094378&2&UID8388688_0" -> "cso1626\4&10094378&2&uid8388688"
                    if (name.StartsWith(@"DISPLAY\", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(@"DISPLAY\".Length);

                    int underscore = name.LastIndexOf('_');
                    if (underscore > 0)
                        name = name.Substring(0, underscore);

                    keys.Add(name.ToLowerInvariant());
                }
            }
        }
        catch
        {
            // WMI okunamazsa boş küme döner; davranış eskisi gibi kalır.
        }

        return keys;
    }

    // WMI'da tutulacak bir tutamaç yok; mevcut nesne aynen kullanılabilir.
    internal override bool Adopt(MonitorDevice fresh) => fresh is WmiMonitor;

    public override void Dispose()
    {
    }
}

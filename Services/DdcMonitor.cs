using System;
using System.Threading;
using BrightTray.Native;

namespace BrightTray.Services;

/// <summary>DDC/CI (dxva2 Monitor Configuration API) üzerinden kontrol edilen harici ekran.</summary>
internal sealed class DdcMonitor : MonitorDevice
{
    private readonly object _io = new();
    private NativeMethods.PHYSICAL_MONITOR _handle;
    private uint _min;
    private uint _max;
    private bool _useVcp;
    private bool _disposed;

    /// <param name="useVcp">true ise parlaklık, yüksek seviyeli API yerine ham VCP 0x10 komutuyla okunur/yazılır.</param>
    public DdcMonitor(NativeMethods.PHYSICAL_MONITOR handle, string id, int index, uint min, uint current, uint max, bool useVcp)
    {
        _handle = handle;
        _min = min;
        _max = max;
        _useVcp = useVcp;

        var description = string.IsNullOrWhiteSpace(handle.szPhysicalMonitorDescription)
            ? Loc.T("ExternalDisplay")
            : handle.szPhysicalMonitorDescription;

        Id = id;
        DefaultName = $"{Loc.T("MonitorN", index)} · {description}";
        IsInternal = false;
        Percent = ToPercent(current, min, max);
    }

    private static int ToPercent(uint current, uint min, uint max) =>
        Math.Clamp((int)Math.Round(((double)current - min) / ((double)max - min) * 100.0), 0, 100);

    protected override void Apply(int percent)
    {
        lock (_io)
        {
            if (_disposed) return;

            uint raw = _min + (uint)Math.Round((_max - _min) * Math.Clamp(percent, 0, 100) / 100.0);

            // DDC/CI, özellikle uyku/kilit sonrası ilk saniyelerde başarısız olabilir; birkaç kez dene.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool ok = _useVcp
                    ? NativeMethods.SetVCPFeature(_handle.hPhysicalMonitor, NativeMethods.VCP_BRIGHTNESS, raw)
                    : NativeMethods.SetMonitorBrightness(_handle.hPhysicalMonitor, raw);

                if (ok)
                    return;

                Thread.Sleep(250);
            }
        }
    }

    internal override int? ReadPercent()
    {
        lock (_io)
        {
            if (_disposed) return null;

            if (_useVcp)
            {
                if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(
                        _handle.hPhysicalMonitor, NativeMethods.VCP_BRIGHTNESS, IntPtr.Zero, out uint vcpCur, out uint vcpMax)
                    && vcpMax > 0)
                {
                    return ToPercent(vcpCur, 0, vcpMax);
                }

                return null;
            }

            if (NativeMethods.GetMonitorBrightness(_handle.hPhysicalMonitor, out uint min, out uint cur, out uint max)
                && max > min)
            {
                return ToPercent(cur, min, max);
            }

            return null;
        }
    }

    internal override bool Adopt(MonitorDevice fresh)
    {
        if (fresh is not DdcMonitor other)
            return false;

        // Kilit sırası: önce diğerini serbest bırak, sonra kendi kilidimizi al (iç içe kilit yok).
        var (newHandle, newMin, newMax, newVcp) = other.Release();

        lock (_io)
        {
            if (_disposed)
            {
                NativeMethods.DestroyPhysicalMonitors(1, new[] { newHandle });
                return true;
            }

            var oldHandle = _handle;
            _handle = newHandle;
            _min = newMin;
            _max = newMax;
            _useVcp = newVcp;
            NativeMethods.DestroyPhysicalMonitors(1, new[] { oldHandle });
        }

        return true;
    }

    /// <summary>Tutamacı bu nesneden alır; nesne artık kullanılmaz ve Dispose bir şey yapmaz.</summary>
    private (NativeMethods.PHYSICAL_MONITOR handle, uint min, uint max, bool useVcp) Release()
    {
        lock (_io)
        {
            _disposed = true;
            return (_handle, _min, _max, _useVcp);
        }
    }

    public override void Dispose()
    {
        lock (_io)
        {
            if (_disposed) return;
            _disposed = true;
            NativeMethods.DestroyPhysicalMonitors(1, new[] { _handle });
        }
    }
}

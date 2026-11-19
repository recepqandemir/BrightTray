using System;
using System.Threading.Tasks;

namespace BrightTray.Services;

/// <summary>
/// Parlaklığı ayarlanabilen bir ekran. DDC/CI çağrıları yavaş olabildiği için
/// değişiklikler arka planda "son değer kazanır" mantığıyla uygulanır;
/// slider sürüklenirken arayüz takılmaz.
/// </summary>
public abstract class MonitorDevice : IDisposable
{
    private readonly object _gate = new();
    private int _pending = -1;
    private bool _busy;

    /// <summary>Yenilemeler ve yeniden başlatmalar arasında değişmeyen ekran kimliği.</summary>
    public string Id { get; protected set; } = "";

    /// <summary>Sistemin/dilin ürettiği varsayılan ad.</summary>
    public string DefaultName { get; internal set; } = "";

    /// <summary>Kullanıcının verdiği ad (yoksa null).</summary>
    public string? CustomName { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(CustomName) ? DefaultName : CustomName!;

    public bool IsInternal { get; protected set; }

    /// <summary>İstenen son parlaklık (0-100).</summary>
    public int Percent { get; protected set; }

    /// <summary>Parlaklık kullanıcı/kısayol/zamanlama tarafından değiştirildiğinde tetiklenir.</summary>
    public event Action<MonitorDevice>? PercentRequested;

    /// <param name="notify">false ise değişiklik "kaydedilecek ayar" sayılmaz (geri yükleme için).</param>
    public void RequestPercent(int percent, bool notify = true)
    {
        percent = Math.Clamp(percent, 0, 100);

        lock (_gate)
        {
            Percent = percent;
            _pending = percent;
            if (!_busy)
            {
                _busy = true;
                Task.Run(Pump);
            }
        }

        if (notify)
            PercentRequested?.Invoke(this);
    }

    private void Pump()
    {
        while (true)
        {
            int value;
            lock (_gate)
            {
                if (_pending < 0)
                {
                    _busy = false;
                    return;
                }
                value = _pending;
                _pending = -1;
            }

            try { Apply(value); }
            catch { /* Tek bir başarısız çağrı diğerlerini engellemesin. */ }
        }
    }

    protected abstract void Apply(int percent);

    /// <summary>
    /// Donanımdaki gerçek parlaklığı okur; okunamıyorsa null. Arka plan iş parçacığından çağrılmalı.
    /// Yalnızca sapmayı düzeltmek için kullanılan doğrulama geçişi bunu kullanır (yalnızca DDC/CI ekranlar).
    /// </summary>
    internal virtual int? ReadPercent() => null;

    /// <summary>Panel açıldığında gösterilecek gerçek değer; varsayılan olarak <see cref="ReadPercent"/>.</summary>
    internal virtual int? ReadActualPercent() => ReadPercent();

    /// <summary>
    /// Donanımdan okunan değeri benimser (cihaza yazmaz). Bekleyen veya süren bir yazma varsa
    /// kullanıcının son isteği ezilmesin diye reddeder.
    /// </summary>
    internal bool TryAdoptHardwarePercent(int percent)
    {
        lock (_gate)
        {
            if (_busy || _pending >= 0)
                return false;

            Percent = Math.Clamp(percent, 0, 100);
            return true;
        }
    }

    /// <summary>
    /// Yenileme sonrası aynı ekranın taze bağlantısını bu nesneye devreder, böylece açık paneldeki
    /// slider'lar geçerli kalır. Desteklenmiyorsa false döner.
    /// </summary>
    internal virtual bool Adopt(MonitorDevice fresh) => false;

    public abstract void Dispose();
}

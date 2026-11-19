using System;
using System.Collections.Generic;
using System.Windows.Threading;
using BrightTray.Models;

namespace BrightTray.Services;

/// <summary>
/// Gün içinde belirlenen saatlerde parlaklığı otomatik değiştirir. Her ekran kendi listesini kullanabilir;
/// kendi listesi boş olan ekran genel ("Tüm ekranlar") listeyi kullanır.
/// </summary>
public sealed class SchedulerService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<IReadOnlyList<string>> _getIds;
    private readonly Action<string, int> _apply;

    private List<(TimeSpan time, int brightness)> _global = new();
    private Dictionary<string, List<(TimeSpan time, int brightness)>> _perMonitor = new();
    private DateTime _lastCheck = DateTime.Now;

    public SchedulerService(Func<IReadOnlyList<string>> getIds, Action<string, int> apply)
    {
        _getIds = getIds;
        _apply = apply;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => Tick();
    }

    private static List<(TimeSpan time, int brightness)> Build(IEnumerable<ScheduleEntry>? source)
    {
        var list = new List<(TimeSpan, int)>();
        if (source != null)
        {
            foreach (var entry in source)
            {
                if (entry.TryGetTime(out var time))
                    list.Add((time, Math.Clamp(entry.Brightness, 0, 100)));
            }
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list;
    }

    public void Configure(AppSettings settings, bool applyNow)
    {
        _timer.Stop();

        _global = Build(settings.Schedule);

        _perMonitor = new Dictionary<string, List<(TimeSpan, int)>>();
        foreach (var (id, entries) in settings.MonitorSchedules)
        {
            var built = Build(entries);
            if (built.Count > 0)
                _perMonitor[id] = built;
        }

        _lastCheck = DateTime.Now;

        if (!settings.ScheduleEnabled || (_global.Count == 0 && _perMonitor.Count == 0))
            return;

        if (applyNow)
            ApplyLatestPast();

        _timer.Start();
    }

    private List<(TimeSpan time, int brightness)> EntriesFor(string id) =>
        _perMonitor.TryGetValue(id, out var own) ? own : _global;

    /// <summary>Şu ana kadarki en son zamanlanmış değeri uygular (gün başındaysa dünkü son değeri).</summary>
    private void ApplyLatestPast()
    {
        var now = DateTime.Now.TimeOfDay;

        foreach (var id in _getIds())
        {
            var entries = EntriesFor(id);
            if (entries.Count == 0) continue;

            int value = entries[^1].brightness;
            foreach (var (time, brightness) in entries)
            {
                if (time <= now)
                    value = brightness;
            }

            _apply(id, value);
        }
    }

    private void Tick()
    {
        var now = DateTime.Now;

        // Gün değiştiyse kontrol aralığını gece yarısından başlat.
        if (now.Date != _lastCheck.Date)
            _lastCheck = now.Date.AddSeconds(-1);

        foreach (var id in _getIds())
        {
            int? value = null;
            foreach (var (time, brightness) in EntriesFor(id))
            {
                var at = now.Date + time;
                if (at > _lastCheck && at <= now)
                    value = brightness;
            }

            if (value is int v)
                _apply(id, v);
        }

        _lastCheck = now;
    }

    public void Dispose() => _timer.Stop();
}

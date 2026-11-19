using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BrightTray.Services;

namespace BrightTray.UI;

public sealed class MonitorViewModel : INotifyPropertyChanged
{
    private readonly MonitorDevice _device;
    private int _percent;

    public MonitorViewModel(MonitorDevice device)
    {
        _device = device;
        _percent = device.Percent;
    }

    public string Name => _device.DisplayName;

    public int Percent
    {
        get => _percent;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (_percent == value) return;

            _percent = value;
            _device.RequestPercent(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PercentText));
        }
    }

    public string PercentText => $"{_percent}%";

    /// <summary>Kısayol/zamanlama değiştirdiğinde slider'ı cihazdaki değere çeker (cihaza tekrar yazmaz).</summary>
    public void SyncFromDevice()
    {
        if (_percent == _device.Percent) return;

        _percent = _device.Percent;
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(PercentText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

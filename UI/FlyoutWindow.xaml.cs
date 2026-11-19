using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using BrightTray.Native;
using BrightTray.Services;

namespace BrightTray.UI;

public partial class FlyoutWindow : Window
{
    private readonly Stopwatch _age = Stopwatch.StartNew();
    private List<MonitorViewModel> _items = new();
    private bool _closing;
    private System.Drawing.Point? _anchor;

    /// <summary>Tanılama için olay izi alıcısı (App tarafından verilir).</summary>
    public Action<string>? TraceSink { get; set; }

    public FlyoutWindow(IEnumerable<MonitorDevice> monitors, int skipped, bool scanning)
    {
        InitializeComponent();

        // Genişlik bazı durumlarda 2 piksele çöküyordu (slider görünmüyordu); koddan da sabitle.
        SizeToContent = SizeToContent.Height;
        Width = 348;
        MinWidth = 348;
        MaxWidth = 348;

        Populate(monitors, skipped, scanning);

        Closing += (_, _) => _closing = true;
        Closed += (_, _) => Trace("Closed");

        Deactivated += (_, _) =>
        {
            Trace($"Deactivated (age {_age.ElapsedMilliseconds} ms)");
            if (_closing) return;

            // Simgeye tıklanırken Windows odağı kısa süre başka yere verebilir; açılıştan hemen sonraki
            // odak kaybında kapanmak yerine paneli öne alıp odağı geri iste.
            if (_age.ElapsedMilliseconds < 700)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ForceForeground));
            else
                Close();
        };

        ContentRendered += (_, _) =>
        {
            Reposition();
            ForceForeground();
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(TraceLayout));
        };

        SizeChanged += (_, _) => Reposition();
        DpiChanged += (_, _) => Reposition();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    /// <summary>Panelin açılacağı noktayı (ekran pikseli) verir; genelde tepsi simgesine tıklanan imleç konumu.</summary>
    public void SetAnchor(System.Drawing.Point point) => _anchor = point;

    private void Trace(string message)
    {
        try { TraceSink?.Invoke("Flyout: " + message); }
        catch { }
    }

    public void Populate(IEnumerable<MonitorDevice> monitors, int skipped, bool scanning)
    {
        _items = monitors.Select(m => new MonitorViewModel(m)).ToList();
        MonitorList.ItemsSource = _items;

        ScanningText.Visibility = scanning && _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = !scanning && _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (skipped > 0)
        {
            SkippedText.Text = Loc.T("SkippedHint", skipped);
            SkippedText.Visibility = Visibility.Visible;
        }
        else
        {
            SkippedText.Visibility = Visibility.Collapsed;
        }
    }

    public void Sync()
    {
        foreach (var item in _items)
            item.SyncFromDevice();
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    private void ForceForeground()
    {
        if (_closing) return;

        try
        {
            Topmost = true;
            Activate();

            IntPtr hwnd = Handle;
            if (hwnd != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(hwnd);

            Focus();
        }
        catch (Exception ex)
        {
            Trace("ForceForeground failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Paneli, tıklanan noktanın bulunduğu ekranın çalışma alanının köşesine piksel cinsinden yerleştirir.
    /// WPF'nin DIP tabanlı Left/Top değeri farklı ölçekli çok ekranlı kurulumlarda yanlış ekrana
    /// veya ekran dışına düşebildiği için doğrudan SetWindowPos kullanılır.
    /// </summary>
    private void Reposition()
    {
        try
        {
            IntPtr hwnd = Handle;
            if (hwnd == IntPtr.Zero) return;

            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;

            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return;

            var point = _anchor ?? System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(point);
            var work = screen.WorkingArea;
            var bounds = screen.Bounds;

            int x = work.Right - w;
            int y = work.Bottom - h;

            // Görev çubuğu solda/üstteyse karşı köşeye yerleştir.
            if (work.Left > bounds.Left) x = work.Left;
            if (work.Top > bounds.Top) y = work.Top;

            x = Math.Max(work.Left, x);
            y = Math.Max(work.Top, y);

            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
        catch (Exception ex)
        {
            Trace("Reposition failed: " + ex.Message);
        }
    }

    private void TraceLayout()
    {
        try
        {
            var sliders = new List<Slider>();
            Collect(this, sliders);

            string slider = "none";
            if (sliders.Count > 0)
                slider = $"{sliders[0].ActualWidth:0}x{sliders[0].ActualHeight:0}, value {sliders[0].Value:0}";

            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;

            string rectText = "?";
            if (NativeMethods.GetWindowRect(Handle, out var r))
                rectText = $"{r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top}px";

            Trace($"layout: items={_items.Count}, sliders={sliders.Count}, firstSlider={slider}, " +
                  $"window={rectText}, Width={Width:0}, wpf=({Left:0},{Top:0}) {ActualWidth:0}x{ActualHeight:0}dip, dpi={dpi:0.##}, " +
                  $"visible={IsVisible}, active={IsActive}, topmost={Topmost}");
        }
        catch (Exception ex)
        {
            Trace("TraceLayout failed: " + ex.Message);
        }
    }

    private static void Collect(DependencyObject parent, List<Slider> result)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Slider s)
                result.Add(s);
            Collect(child, result);
        }
    }
}

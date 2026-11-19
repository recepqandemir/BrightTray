using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BrightTray.Models;
using BrightTray.Native;
using BrightTray.Services;
using BrightTray.UI;
using Microsoft.Win32;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace BrightTray;

public partial class App : System.Windows.Application
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "BrightTray";

    private Mutex? _singleInstance;
    private WinForms.NotifyIcon? _tray;
    private Drawing.Icon? _trayIcon;

    private readonly SettingsService _settingsService = new();
    private readonly MonitorService _monitors = new();
    private string _reportPath = "";
    private AppSettings _settings = new();
    private HotkeyService? _hotkeys;
    private SchedulerService? _scheduler;
    private FlyoutWindow? _flyout;
    private DateTime _flyoutClosedAtUtc = DateTime.MinValue;

    private DispatcherTimer? _refreshDebounce;
    private DispatcherTimer? _saveTimer;
    private bool _refreshing;
    private bool _refreshAgain;
    private bool _firstRefreshDone;

    // ---------------------------------------------------------------- tanılama izi

    private readonly System.Text.StringBuilder _trace = new();
    private readonly object _traceLock = new();

    /// <summary>Son olayları bellekte tutar; tanılama dosyasına yazılır.</summary>
    private void Trace(string message)
    {
        lock (_traceLock)
        {
            if (_trace.Length > 20000)
                _trace.Remove(0, 10000);

            _trace.AppendLine($"{DateTime.Now:HH:mm:ss.fff}  {message}");
        }
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private string BuildDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"BrightTray {AppInfo.Version}");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Elevated (admin): {IsElevated()}");
        sb.AppendLine($"Process: {Environment.ProcessPath}");
        sb.AppendLine($"User: {Environment.UserName}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"UI culture: {System.Globalization.CultureInfo.CurrentUICulture.Name}, language setting: {_settings.Language}");
        sb.AppendLine($"Monitors in list: {_monitors.Monitors.Count}, skipped: {_monitors.SkippedCount}, scanning: {_monitors.IsScanning}");
        sb.AppendLine();

        sb.AppendLine("Screens:");
        foreach (var s in WinForms.Screen.AllScreens)
        {
            sb.AppendLine($"  {s.DeviceName}  primary={s.Primary}  bounds={s.Bounds}  work={s.WorkingArea}");
        }

        sb.AppendLine();
        sb.AppendLine("Events:");
        lock (_traceLock)
            sb.Append(_trace);

        sb.AppendLine();
        sb.AppendLine("Monitor scan report:");
        sb.AppendLine(_monitors.LastReport);
        return sb.ToString();
    }

    /// <summary>Beklenmeyen hataları sessizce yutmak yerine %APPDATA%\BrightTray\error.log dosyasına yazar.</summary>
    private void LogError(Exception? ex)
    {
        try
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(_settingsService.FilePath)!, "error.log");

            System.IO.File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] BrightTray {AppInfo.Version}{Environment.NewLine}" +
                $"{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Günlük yazılamazsa yapılacak bir şey yok.
        }
    }

    private bool _errorShown;

    /// <summary>
    /// Hatayı günlüğe yazar ve oturum başına bir kez kullanıcıya gösterir. Böylece bir şey (örn. panel) sessizce
    /// bozulduğunda nedeni görülebilir; her hatada üst üste pencere açılmaz.
    /// </summary>
    private void ReportUnexpected(Exception? ex)
    {
        LogError(ex);

        if (_errorShown || ex == null)
            return;

        _errorShown = true;

        try
        {
            System.Windows.MessageBox.Show(
                $"{Loc.T("UnexpectedError")}{Environment.NewLine}{Environment.NewLine}{ex.GetType().Name}: {ex.Message}",
                "BrightTray", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch
        {
            // Pencere gösterilemezse günlük yeterli.
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            ReportUnexpected(args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogError(args.ExceptionObject as Exception);
        WinForms.Application.ThreadException += (_, args) => ReportUnexpected(args.Exception);

        _singleInstance = new Mutex(true, "BrightTray_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _settings = _settingsService.Load();
        Loc.Set(_settings.Language);
        Trace($"Startup {AppInfo.Version}, elevated={IsElevated()}");

        CreateTray();

        _monitors.Settings = _settings;
        _reportPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(_settingsService.FilePath)!, "monitors.log");
        _monitors.ReportPath = _reportPath;
        _monitors.BrightnessChanged += OnBrightnessChanged;
        _monitors.MonitorsRefreshed += OnMonitorsRefreshed;
        _monitors.StateChanged += ScheduleSave;

        // Slider sürüklenirken her adımda diske yazmamak için kayıt geciktirilir.
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _settingsService.Save(_settings);
        };

        _scheduler = new SchedulerService(
            () => System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(_monitors.Monitors, m => m.Id)),
            (id, percent) => _monitors.SetOne(id, percent));
        _hotkeys = new HotkeyService();
        RegisterHotkeys();
        SetRunAtStartup(_settings.RunAtStartup);

        // Ekran bağlandığında/çıkarıldığında veya uykudan dönüldüğünde listeyi yenile.
        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshDebounce.Tick += async (_, _) =>
        {
            _refreshDebounce.Stop();
            await RefreshMonitorsAsync();
        };
        SystemEvents.DisplaySettingsChanged += OnSystemChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        _ = RefreshMonitorsAsync();
    }

    // ---------------------------------------------------------------- ekranlar

    private async Task RefreshMonitorsAsync()
    {
        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }

        _refreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                await _monitors.RefreshAsync();
            }
            while (_refreshAgain);
        }
        catch
        {
            // Ekran sorgusu başarısız olursa mevcut liste kalır.
        }
        finally
        {
            _refreshing = false;
        }

        // Zamanlama, ekranlar ilk kez listelendikten sonra başlar ki açılış değeri uygulanabilsin.
        if (!_firstRefreshDone)
        {
            _firstRefreshDone = true;
            _scheduler?.Configure(_settings, applyNow: _settings.ApplyScheduleOnStartup);
        }
    }

    private void OnSystemChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke((Action)(() =>
        {
            _refreshDebounce?.Stop();
            _refreshDebounce?.Start();
        }));

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            OnSystemChanged(sender, EventArgs.Empty);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        // Ekran kilidi açılınca veya uzak oturumdan dönülünce monitörler sıklıkla varsayılana döner.
        if (e.Reason is SessionSwitchReason.SessionUnlock
            or SessionSwitchReason.ConsoleConnect
            or SessionSwitchReason.RemoteConnect)
        {
            OnSystemChanged(sender, EventArgs.Empty);
        }
    }

    private void OnBrightnessChanged()
    {
        UpdateTrayText();
        _flyout?.Sync();
    }

    private void OnMonitorsRefreshed(bool setChanged)
    {
        UpdateTrayText();
        Trace($"Monitors refreshed: count={_monitors.Monitors.Count}, skipped={_monitors.SkippedCount}, setChanged={setChanged}");

        // Ekran kümesi aynıysa nesneler korunduğu için açık panel olduğu gibi çalışmaya devam eder;
        // değiştiyse (örn. tarama yeni bitti) panel kapanmadan yerinde yeniden oluşturulur.
        if (setChanged)
            _flyout?.Populate(_monitors.Monitors, _monitors.SkippedCount, _monitors.IsScanning);
        else
            _flyout?.Sync();
    }

    /// <summary>Panel açıldığında gerçek parlaklığı okur ve slider'lara yansıtır.</summary>
    private async Task SyncPanelFromHardwareAsync()
    {
        try
        {
            if (await _monitors.SyncFromHardwareAsync())
            {
                _flyout?.Sync();
                UpdateTrayText();
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    private void ScheduleSave()
    {
        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    // ---------------------------------------------------------------- kısayollar & ayarlar

    private void RegisterHotkeys()
    {
        if (_hotkeys == null) return;

        _hotkeys.UnregisterAll();

        int step = Math.Clamp(_settings.HotkeyStep, 1, 50);
        bool upOk = _hotkeys.Register(_settings.HotkeyUp, () => _monitors.AdjustAll(step));
        bool downOk = _hotkeys.Register(_settings.HotkeyDown, () => _monitors.AdjustAll(-step));

        if (!upOk || !downOk)
        {
            string failed = !upOk ? _settings.HotkeyUp : _settings.HotkeyDown;
            _tray?.ShowBalloonTip(4000, "BrightTray",
                Loc.T("HotkeyFailed", failed),
                WinForms.ToolTipIcon.Warning);
        }
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings.Clone(), _monitors.Monitors);
        if (window.ShowDialog() == true && window.Result is { } updated)
            ApplySettings(updated);
    }

    private void ApplySettings(AppSettings updated)
    {
        bool languageChanged = !string.Equals(updated.Language, _settings.Language, StringComparison.Ordinal);

        // Pencere açıkken kısayol/zamanlama parlaklığı değiştirmiş olabilir; canlı kaydı koru.
        updated.SavedBrightness = _settings.SavedBrightness;

        _settings = updated;
        _monitors.Settings = _settings;
        _monitors.ApplyNames();
        _settingsService.Save(_settings);

        if (languageChanged)
        {
            Loc.Set(_settings.Language);
            RebuildTrayMenu();
        }

        RegisterHotkeys();
        SetRunAtStartup(_settings.RunAtStartup);
        _scheduler?.Configure(_settings, applyNow: false);

        // Ekran adları dile göre üretildiği için liste yeniden oluşturulur.
        if (languageChanged)
            _ = RefreshMonitorsAsync();
    }

    private static void SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enable && Environment.ProcessPath is { } path)
                key.SetValue(RunValueName, $"\"{path}\"");
            else
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Kayıt defteri yazılamazsa sessizce geç.
        }
    }

    // ---------------------------------------------------------------- tepsi

    private void CreateTray()
    {
        _trayIcon = CreateTrayIcon();

        _tray = new WinForms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "BrightTray",
            ContextMenuStrip = BuildMenu(),
            Visible = true,
        };

        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                ToggleFlyout();
        };
    }

    /// <summary>Son ekran taramasının raporunu Not Defteri gibi varsayılan metin düzenleyicide açar.</summary>
    private void OpenDiagnostics()
    {
        try
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(_settingsService.FilePath)!, "diagnostics.txt");

            System.IO.File.WriteAllText(path, BuildDiagnostics());
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    private WinForms.ContextMenuStrip BuildMenu()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(new WinForms.ToolStripMenuItem($"BrightTray · {Loc.T("Version", AppInfo.Version)}") { Enabled = false });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Loc.T("MenuSettings"), null, (_, _) => OpenSettings());
        menu.Items.Add(Loc.T("MenuRefresh"), null, async (_, _) => await RefreshMonitorsAsync());
        menu.Items.Add(Loc.T("MenuDiagnostics"), null, (_, _) => OpenDiagnostics());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Loc.T("MenuExit"), null, (_, _) => ExitApp());
        return menu;
    }

    /// <summary>Dil değişince menüyü yeni dille yeniden kurar.</summary>
    private void RebuildTrayMenu()
    {
        if (_tray == null) return;

        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildMenu();
        old?.Dispose();
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        if (_tray == null) return;

        _tray.Text = _monitors.Monitors.Count == 0
            ? Loc.T("TrayNoMonitor")
            : Loc.T("TrayBrightness", _monitors.AveragePercent);
    }

    private void ToggleFlyout()
    {
        var cursor = WinForms.Cursor.Position;
        Trace($"Tray click at {cursor.X},{cursor.Y}; flyout open={_flyout != null}; monitors={_monitors.Monitors.Count}");

        if (_flyout != null)
        {
            _flyout.Close();
            return;
        }

        // Simgeye tıklamak pencereyi "Deactivated" ile zaten kapatır; hemen yeniden açılmasın.
        if ((DateTime.UtcNow - _flyoutClosedAtUtc).TotalMilliseconds < 250)
        {
            Trace("Tray click ignored (flyout just closed)");
            return;
        }

        try
        {
            _flyout = new FlyoutWindow(_monitors.Monitors, _monitors.SkippedCount, _monitors.IsScanning);
            _flyout.TraceSink = Trace;
            _flyout.SetAnchor(cursor);
            _flyout.Closed += (_, _) =>
            {
                _flyout = null;
                _flyoutClosedAtUtc = DateTime.UtcNow;
            };

            _flyout.Show();
            _flyout.Activate();
            Trace("Flyout shown");

            // Slider'lar önce bilinen değerle açılır; gerçek değer okununca yerinde güncellenir.
            _ = SyncPanelFromHardwareAsync();
        }
        catch (Exception ex)
        {
            // Panel açılamazsa sessiz kalma: hatayı günlüğe yaz ve kullanıcıya göster.
            _flyout = null;
            Trace("Flyout failed: " + ex);
            LogError(ex);
            System.Windows.MessageBox.Show(
                $"{Loc.T("PanelError")}{Environment.NewLine}{Environment.NewLine}{ex.GetType().Name}: {ex.Message}",
                "BrightTray", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Bir görsel varlık eklemeden, koddan basit bir güneş simgesi çizer.</summary>
    private static Drawing.Icon CreateTrayIcon()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            var color = Drawing.Color.FromArgb(255, 138, 61);
            using var brush = new Drawing.SolidBrush(color);
            using var pen = new Drawing.Pen(color, 2.5f);

            g.FillEllipse(brush, 9, 9, 14, 14);

            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);
                g.DrawLine(pen, 16 + cos * 10f, 16 + sin * 10f, 16 + cos * 14.5f, 16 + sin * 14.5f);
            }
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temp = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    // ---------------------------------------------------------------- çıkış

    private void ExitApp()
    {
        if (_tray != null)
            _tray.Visible = false;

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnSystemChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;

        // Bekleyen parlaklık kaydı varsa kaybolmasın.
        if (_saveTimer?.IsEnabled == true)
        {
            _saveTimer.Stop();
            _settingsService.Save(_settings);
        }

        _scheduler?.Dispose();
        _hotkeys?.Dispose();
        _monitors.Dispose();
        _tray?.Dispose();
        _trayIcon?.Dispose();

        try { _singleInstance?.ReleaseMutex(); } catch { }
        _singleInstance?.Dispose();

        base.OnExit(e);
    }
}

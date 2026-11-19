using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using BrightTray.Models;
using BrightTray.Native;
using BrightTray.Services;

namespace BrightTray.UI;

/// <summary>Dil açılır listesindeki bir satır.</summary>
public sealed record LangItem(string Code, string Name)
{
    // Tema şablonu seçili öğeyi DisplayMemberPath olmadan gösterdiği için okunur metin burada verilir.
    public override string ToString() => Name;
}

/// <summary>Ekran adları listesindeki bir satır.</summary>
public sealed class NameRow
{
    public NameRow(string id, string defaultName, string? customName)
    {
        Id = id;
        DefaultName = defaultName;
        CustomName = customName ?? "";
    }

    public string Id { get; }
    public string DefaultName { get; }
    public string CustomName { get; set; }
}

/// <summary>Zamanlama hedef listesindeki bir satır ("" anahtarı = tüm ekranlar).</summary>
public sealed record TargetItem(string Key, string Name)
{
    public override string ToString() => Name;
}

public partial class SettingsWindow : Window
{
    private readonly AppSettings _working;
    private readonly IReadOnlyList<MonitorDevice> _monitors;

    /// <summary>Hedef anahtarı ("" = genel) → düzenlenen zamanlama listesi.</summary>
    private readonly Dictionary<string, ObservableCollection<ScheduleEntry>> _lists = new();
    private readonly List<NameRow> _names;

    /// <summary>Kaydet'e basıldıysa doğrulanmış yeni ayarlar.</summary>
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings current, IReadOnlyList<MonitorDevice> monitors)
    {
        InitializeComponent();

        // Küçük ekranlarda pencere taşmasın; içerik kaydırılır, alt çubuktaki düğmeler hep görünür kalır.
        MaxHeight = SystemParameters.WorkArea.Height - 40;

        VersionText.Text = $"BrightTray · {Loc.T("Version", AppInfo.Version)}";

        _working = current;
        _monitors = monitors;

        _lists[""] = new ObservableCollection<ScheduleEntry>(current.Schedule);
        foreach (var m in monitors)
        {
            _lists[m.Id] = current.MonitorSchedules.TryGetValue(m.Id, out var own)
                ? new ObservableCollection<ScheduleEntry>(own)
                : new ObservableCollection<ScheduleEntry>();
        }

        _names = monitors
            .Select(m => new NameRow(
                m.Id,
                m.DefaultName,
                current.CustomNames.TryGetValue(m.Id, out var custom) ? custom : null))
            .ToList();
        NamesList.ItemsSource = _names;
        NamesEmpty.Visibility = _names.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var languages = new List<LangItem> { new("auto", Loc.T("LangAuto")) };
        languages.AddRange(Loc.Languages.Select(l => new LangItem(l.Code, l.Name)));
        LanguageBox.ItemsSource = languages;
        LanguageBox.SelectedValue = languages.Any(l => l.Code == current.Language) ? current.Language : "auto";

        UpBox.Text = current.HotkeyUp;
        DownBox.Text = current.HotkeyDown;
        StepBox.Text = current.HotkeyStep.ToString(CultureInfo.InvariantCulture);
        ScheduleEnabledBox.IsChecked = current.ScheduleEnabled;
        ApplyOnStartupBox.IsChecked = current.ApplyScheduleOnStartup;
        RestoreBox.IsChecked = current.RestoreBrightness;
        RunAtStartupBox.IsChecked = current.RunAtStartup;

        var targets = new List<TargetItem> { new("", Loc.T("AllDisplays")) };
        targets.AddRange(monitors.Select(m => new TargetItem(m.Id, m.DisplayName)));
        TargetBox.ItemsSource = targets;
        TargetBox.SelectedIndex = 0;
        ScheduleGrid.ItemsSource = _lists[""];
    }

    private void OnTargetChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TargetBox.SelectedValue is not string key || !_lists.ContainsKey(key))
            return;

        // Önceki listede yarım kalan düzenlemeyi kaydet, sonra yeni listeyi göster.
        ScheduleGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        ScheduleGrid.ItemsSource = _lists[key];
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            // Koyu tema ile uyumlu koyu başlık çubuğu. 20: Windows 10 20H1+ ve 11, 19: eski 10 sürümleri.
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1;
            if (NativeMethods.DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                NativeMethods.DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }
        catch
        {
            // Desteklenmiyorsa açık renkli başlık çubuğuyla devam.
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ScheduleGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        if (!int.TryParse(StepBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int step)
            || step < 1 || step > 50)
        {
            Warn(Loc.T("WarnStep"));
            return;
        }

        if (!HotkeyService.TryParse(UpBox.Text, out _, out _))
        {
            Warn(Loc.T("WarnUp"));
            return;
        }

        if (!HotkeyService.TryParse(DownBox.Text, out _, out _))
        {
            Warn(Loc.T("WarnDown"));
            return;
        }

        foreach (var list in _lists.Values)
        {
            foreach (var entry in list)
            {
                if (!entry.TryGetTime(out _))
                {
                    Warn(Loc.T("WarnTime", entry.Time));
                    return;
                }

                if (entry.Brightness < 0 || entry.Brightness > 100)
                {
                    Warn(Loc.T("WarnBrightness"));
                    return;
                }
            }
        }

        // Yalnızca şu an bağlı ekranların adları güncellenir; bağlı olmayanların kayıtlı adı korunur.
        foreach (var row in _names)
        {
            string text = (row.CustomName ?? "").Trim();

            if (text.Length == 0 || text == row.DefaultName)
                _working.CustomNames.Remove(row.Id);
            else
                _working.CustomNames[row.Id] = text;
        }

        _working.Language = LanguageBox.SelectedValue as string ?? "auto";
        _working.HotkeyUp = UpBox.Text.Trim();
        _working.HotkeyDown = DownBox.Text.Trim();
        _working.HotkeyStep = step;
        _working.ScheduleEnabled = ScheduleEnabledBox.IsChecked == true;
        _working.ApplyScheduleOnStartup = ApplyOnStartupBox.IsChecked == true;
        _working.RestoreBrightness = RestoreBox.IsChecked == true;
        _working.RunAtStartup = RunAtStartupBox.IsChecked == true;
        _working.Schedule = _lists[""].ToList();

        // Yalnızca bağlı ekranların listeleri güncellenir; bağlı olmayan ekranların kayıtlı zamanlaması korunur.
        foreach (var m in _monitors)
        {
            var own = _lists[m.Id].ToList();
            if (own.Count > 0)
                _working.MonitorSchedules[m.Id] = own;
            else
                _working.MonitorSchedules.Remove(m.Id);
        }

        Result = _working;
        DialogResult = true;
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "BrightTray", MessageBoxButton.OK, MessageBoxImage.Warning);
}

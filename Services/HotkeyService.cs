using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using BrightTray.Native;

namespace BrightTray.Services;

/// <summary>RegisterHotKey ile genel (global) klavye kısayolları. Mesaj-only gizli pencere kullanır.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HWND_MESSAGE = -3;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("BrightTrayHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(HWND_MESSAGE),
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>"Ctrl+Alt+Up" gibi bir ifadeyi kaydeder. Başka uygulama aynı kısayolu tutuyorsa false döner.</summary>
    public bool Register(string gesture, Action action)
    {
        if (!TryParse(gesture, out uint modifiers, out uint vk))
            return false;

        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_source.Handle, id, modifiers, vk))
            return false;

        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
    }

    public static bool TryParse(string? gesture, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(gesture))
            return false;

        foreach (var part in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "alt": modifiers |= 0x0001; break;
                case "ctrl":
                case "control": modifiers |= 0x0002; break;
                case "shift": modifiers |= 0x0004; break;
                case "win": modifiers |= 0x0008; break;
                default:
                    if (!Enum.TryParse<Key>(part, true, out var key))
                        return false;
                    vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }

        // Genel kısayol için en az bir değiştirici tuş gerekli.
        return vk != 0 && modifiers != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}

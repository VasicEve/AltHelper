using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Color = System.Drawing.Color;

namespace MouseToPad;

/// <summary>The tray icon, its context menu, window management, and the anti-AFK timer.</summary>
internal sealed class TrayController : IDisposable
{
    private readonly HookEngine _engine;
    private readonly TaskbarIcon _tray;
    private readonly MenuItem _enableItem;
    private readonly MenuItem _disableItem;
    private readonly MenuItem _keepAwakeItem;
    private readonly DispatcherTimer _keepAwakeTimer = new();
    private readonly Random _rng = new();
    private readonly Icon _iconOn = CreateIcon(enabled: true);
    private readonly Icon _iconOff = CreateIcon(enabled: false);
    private ImageSource? _windowIcon;
    private MappingsWindow? _mappingsWindow;
    private AppSettings _settings;

    public TrayController(HookEngine engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;

        _enableItem = new MenuItem { Header = "Enable" };
        _enableItem.Click += (_, _) => SetEnabled(true);
        _disableItem = new MenuItem { Header = "Disable" };
        _disableItem.Click += (_, _) => SetEnabled(false);
        _keepAwakeItem = new MenuItem { Header = "Keep player active" };
        _keepAwakeItem.Click += (_, _) => ToggleKeepAwake();
        var mappingsItem = new MenuItem { Header = "Button mappings…" };
        mappingsItem.Click += (_, _) => ShowMappings();
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApp();

        var menu = new ContextMenu();
        menu.Items.Add(_enableItem);
        menu.Items.Add(_disableItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_keepAwakeItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(mappingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _keepAwakeTimer.Tick += async (_, _) =>
        {
            ScheduleNextPulse();   // fresh jitter each round so the cadence never repeats
            if (!ShouldPulse())
                return;
            await _engine.KeepAwakePulseAsync(_settings.KeepAwake.Action);
        };
        ApplyKeepAwake();

        _tray = new TaskbarIcon { ContextMenu = menu };
        _tray.TrayMouseDoubleClick += (_, _) => ShowMappings();
        UpdateUi();

        _tray.ShowBalloonTip("MouseToPad",
            "Running in the system tray. Right-click the gamepad icon for options.", BalloonIcon.Info);
    }

    private void SetEnabled(bool on)
    {
        if (on) _engine.Enable();
        else _engine.Disable();
        UpdateUi();
    }

    private void ToggleKeepAwake()
    {
        _settings = _settings with
        {
            KeepAwake = _settings.KeepAwake with { Enabled = !_settings.KeepAwake.Enabled },
        };
        MappingStore.Save(_settings);
        ApplyKeepAwake();
        UpdateUi();
    }

    private void ApplyKeepAwake()
    {
        if (_settings.KeepAwake.Enabled)
        {
            ScheduleNextPulse();
            _keepAwakeTimer.Start();
        }
        else
        {
            _keepAwakeTimer.Stop();
        }
    }

    /// <summary>The configured interval is a midpoint, not a metronome: each pulse
    /// lands at 60-140% of it so the timing never shows a detectable pattern.</summary>
    private void ScheduleNextPulse()
    {
        double factor = 0.6 + _rng.NextDouble() * 0.8;
        double seconds = Math.Max(5, _settings.KeepAwake.IntervalSeconds * factor);
        _keepAwakeTimer.Interval = TimeSpan.FromSeconds(seconds);
    }

    private bool ShouldPulse()
    {
        var ka = _settings.KeepAwake;
        // the pulse only matters if Moonlight is there to stream it to the second PC
        if (ka.OnlyWhileMoonlightRuns && !IsMoonlightRunning())
            return false;
        // hands off while the user is actively driving the remote session:
        // Moonlight focused AND recent input. Activity outside Moonlight (e.g.
        // playing local SC, which has no gamepad bindings) never blocks the pulse.
        if (ka.PauseWhileActive && FocusWatch.IsMoonlightFocused()
            && FocusWatch.UserActiveWithin(TimeSpan.FromSeconds(30)))
            return false;
        return true;
    }

    private static bool IsMoonlightRunning()
    {
        var procs = Process.GetProcessesByName("Moonlight");
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }

    private void UpdateUi()
    {
        bool on = _engine.Enabled;
        _enableItem.IsEnabled = !on;
        _enableItem.IsChecked = on;
        _disableItem.IsEnabled = on;
        _disableItem.IsChecked = !on;
        _keepAwakeItem.IsChecked = _settings.KeepAwake.Enabled;
        _tray.Icon = on ? _iconOn : _iconOff;
        _tray.ToolTipText = $"MouseToPad — {(on ? "enabled" : "disabled")}, {_engine.MappingCount} mapping(s)"
            + (_settings.KeepAwake.Enabled ? ", anti-AFK on" : "");
    }

    private void ShowMappings()
    {
        if (_mappingsWindow is { } open)
        {
            if (open.WindowState == WindowState.Minimized)
                open.WindowState = WindowState.Normal;
            open.Activate();
            return;
        }

        _windowIcon ??= CreateWindowIcon();
        _mappingsWindow = new MappingsWindow(_engine, _settings) { Icon = _windowIcon };
        _mappingsWindow.Closed += (_, _) =>
        {
            _mappingsWindow = null;
            // the window persists on Save; re-read so the timer and menu reflect it
            _settings = MappingStore.Load();
            ApplyKeepAwake();
            UpdateUi();
        };
        _mappingsWindow.Show();
    }

    private void ExitApp()
    {
        _mappingsWindow?.Close();
        Application.Current.Shutdown();   // App.OnExit disposes us and the engine
    }

    public void Dispose()
    {
        _keepAwakeTimer.Stop();
        _tray.Dispose();
        _iconOn.Dispose();
        _iconOff.Dispose();
    }

    private ImageSource CreateWindowIcon()
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            _iconOn.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }

    /// <summary>Draw a tiny gamepad glyph: green when enabled, grey when disabled.</summary>
    private static Icon CreateIcon(bool enabled)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var body = new SolidBrush(enabled ? Color.FromArgb(40, 167, 69) : Color.FromArgb(108, 117, 125));
            g.FillEllipse(body, 1, 8, 14, 16);     // left grip
            g.FillEllipse(body, 17, 8, 14, 16);    // right grip
            g.FillRectangle(body, 8, 8, 16, 14);   // bridge
            using var dot = new SolidBrush(Color.White);
            g.FillEllipse(dot, 6, 13, 5, 5);       // d-pad blob
            g.FillEllipse(dot, 21, 13, 5, 5);      // face-button blob
        }
        IntPtr h = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(h).Clone();
        }
        finally
        {
            DestroyIcon(h);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace MouseToPad;

/// <summary>
/// Owns the virtual Xbox 360 pad and the low-level keyboard hook that drives it.
/// The hook stays installed for the app's lifetime (the mapping dialog also uses it
/// to capture keys); <see cref="Enabled"/> gates the key->pad translation.
/// Low-level hook callbacks arrive on the thread that installed the hook — here the
/// WinForms message-loop thread — so no cross-thread marshalling is needed.
/// </summary>
internal sealed class HookEngine : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const uint VK_ESCAPE = 0x1B;

    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _pad;
    private readonly HookProc _proc;                 // keep the delegate alive (no GC)
    private IntPtr _hook;
    private Dictionary<uint, PadButton> _map;
    private readonly HashSet<uint> _held = new();    // trigger keys currently down (debounces key-repeat)
    private readonly Random _rng = new();            // varies the anti-AFK pulses
    private Action<uint>? _capture;                  // single-shot key capture; 0 = cancelled via Esc

    public bool Enabled { get; private set; } = true;
    public bool SwallowKeys { get; set; } = true;    // stop trigger keys leaking to other apps

    public IReadOnlyList<Mapping> Mappings => _map.Select(kv => new Mapping(kv.Key, kv.Value)).ToList();
    public int MappingCount => _map.Count;

    public HookEngine(IEnumerable<Mapping> mappings)
    {
        _map = mappings.ToDictionary(m => m.Vk, m => m.Button);

        _client = new ViGEmClient();                 // throws if the ViGEmBus driver is missing
        _pad = _client.CreateXbox360Controller();
        _pad.Connect();

        _proc = Callback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx failed");
    }

    public void Enable() => Enabled = true;

    public void Disable()
    {
        Enabled = false;
        ReleaseHeld();                               // don't leave pad buttons stuck down
    }

    /// <summary>Swap in a new mapping set, releasing anything currently held first.</summary>
    public void SetMappings(IEnumerable<Mapping> mappings)
    {
        ReleaseHeld();
        _map = mappings.ToDictionary(m => m.Vk, m => m.Button);
    }

    /// <summary>
    /// Swallow the next key pressed anywhere and report its virtual-key code.
    /// Esc cancels: the callback receives 0. Only one capture can be pending.
    /// </summary>
    public void BeginCapture(Action<uint> onKey) => _capture = onKey;

    public void CancelCapture() => _capture = null;

    private void ReleaseHeld()
    {
        foreach (var vk in _held)
            if (_map.TryGetValue(vk, out var button))
                Apply(button, false);
        _held.Clear();
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool up = msg is WM_KEYUP or WM_SYSKEYUP;

            if (_capture is { } capture && down)
            {
                _capture = null;
                capture(data.vkCode == VK_ESCAPE ? 0u : data.vkCode);
                return (IntPtr)1;                    // never leak the captured press
            }

            if (Enabled && _map.TryGetValue(data.vkCode, out var button))
            {
                if (down)
                {
                    if (_held.Add(data.vkCode))
                        Apply(button, true);
                }
                else if (up && _held.Remove(data.vkCode))
                {
                    Apply(button, false);
                }
                if (SwallowKeys) return (IntPtr)1;   // consume the trigger key
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Brief synthetic input so a game's idle-logout timer resets (anti-AFK).
    /// Everything about the pulse is randomized — direction, strength, duration,
    /// number of movements — and stick motion ramps in and out through
    /// intermediate positions so the raw input stream looks like a thumb,
    /// not a square wave. Runs on the UI thread; awaits resume via the dispatcher.
    /// </summary>
    public async Task KeepAwakePulseAsync(KeepAwakeAction action)
    {
        try
        {
            switch (action)
            {
                case KeepAwakeAction.RightStickNudge:
                    await NudgeStickAsync(_pad, Xbox360Axis.RightThumbX, Xbox360Axis.RightThumbY);
                    break;

                case KeepAwakeAction.LeftStickNudge:
                    await NudgeStickAsync(_pad, Xbox360Axis.LeftThumbX, Xbox360Axis.LeftThumbY);
                    break;

                default:
                    var button = KeepAwakeActions.ToPadButton(action)!.Value;
                    // don't tap a button a real mapping is currently holding down
                    if (_held.Any(vk => _map.TryGetValue(vk, out var held) && held == button))
                        return;
                    Apply(button, true);
                    await Task.Delay(_rng.Next(60, 180));   // human-ish hold time
                    Apply(button, false);
                    break;
            }
        }
        catch
        {
            // pad may be disposed mid-pulse during app shutdown
        }
    }

    private async Task NudgeStickAsync(IXbox360Controller pad, Xbox360Axis xAxis, Xbox360Axis yAxis)
    {
        int movements = _rng.Next(1, 3);                     // one flick, or a glance-and-correct
        for (int m = 0; m < movements; m++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;  // any direction
            double magnitude = _rng.Next(5500, 11500);       // ~17-35% deflection
            short x = (short)(Math.Cos(angle) * magnitude);
            short y = (short)(Math.Sin(angle) * magnitude);

            int steps = _rng.Next(2, 5);
            for (int i = 1; i <= steps; i++)                 // ease in
            {
                pad.SetAxisValue(xAxis, (short)(x * i / steps));
                pad.SetAxisValue(yAxis, (short)(y * i / steps));
                pad.SubmitReport();
                await Task.Delay(_rng.Next(15, 40));
            }
            for (int i = steps - 1; i >= 0; i--)             // ease back out
            {
                pad.SetAxisValue(xAxis, (short)(x * i / steps));
                pad.SetAxisValue(yAxis, (short)(y * i / steps));
                pad.SubmitReport();
                await Task.Delay(_rng.Next(15, 40));
            }

            if (m < movements - 1)
                await Task.Delay(_rng.Next(120, 450));       // beat between movements
        }

        pad.SetAxisValue(xAxis, 0);                          // always end centered
        pad.SetAxisValue(yAxis, 0);
        pad.SubmitReport();
    }

    private void Apply(PadButton button, bool down) => ApplyTo(_pad, button, down);

    private static void ApplyTo(IXbox360Controller pad, PadButton button, bool down)
    {
        switch (button)
        {
            case PadButton.LeftTrigger:
                pad.SetSliderValue(Xbox360Slider.LeftTrigger, down ? byte.MaxValue : byte.MinValue);
                break;
            case PadButton.RightTrigger:
                pad.SetSliderValue(Xbox360Slider.RightTrigger, down ? byte.MaxValue : byte.MinValue);
                break;
            default:
                pad.SetButtonState(ToVigem(button), down);
                break;
        }
        pad.SubmitReport();
    }

    private static Xbox360Button ToVigem(PadButton b) => b switch
    {
        PadButton.A => Xbox360Button.A,
        PadButton.B => Xbox360Button.B,
        PadButton.X => Xbox360Button.X,
        PadButton.Y => Xbox360Button.Y,
        PadButton.LeftShoulder => Xbox360Button.LeftShoulder,
        PadButton.RightShoulder => Xbox360Button.RightShoulder,
        PadButton.Back => Xbox360Button.Back,
        PadButton.Start => Xbox360Button.Start,
        PadButton.Guide => Xbox360Button.Guide,
        PadButton.LeftThumb => Xbox360Button.LeftThumb,
        PadButton.RightThumb => Xbox360Button.RightThumb,
        PadButton.DpadUp => Xbox360Button.Up,
        PadButton.DpadDown => Xbox360Button.Down,
        PadButton.DpadLeft => Xbox360Button.Left,
        PadButton.DpadRight => Xbox360Button.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(b), b, null),
    };

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        try { _pad.Disconnect(); } catch { /* already gone */ }
        _client.Dispose();
    }

    // ---- Win32 interop ----------------------------------------------------
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MouseToPad;

/// <summary>Virtual Xbox 360 controls a trigger key can be mapped to.
/// LT/RT are sliders on the real pad; here a mapped key presses them fully.</summary>
internal enum PadButton
{
    A, B, X, Y,
    LeftShoulder, RightShoulder,
    LeftTrigger, RightTrigger,
    Back, Start, Guide,
    LeftThumb, RightThumb,
    DpadUp, DpadDown, DpadLeft, DpadRight,
}

internal static class PadButtons
{
    public static string DisplayName(PadButton b) => b switch
    {
        PadButton.LeftShoulder => "Left bumper (LB)",
        PadButton.RightShoulder => "Right bumper (RB)",
        PadButton.LeftTrigger => "Left trigger (LT)",
        PadButton.RightTrigger => "Right trigger (RT)",
        PadButton.LeftThumb => "Left stick click (L3)",
        PadButton.RightThumb => "Right stick click (R3)",
        PadButton.Guide => "Guide (Xbox logo)",
        PadButton.DpadUp => "D-pad Up",
        PadButton.DpadDown => "D-pad Down",
        PadButton.DpadLeft => "D-pad Left",
        PadButton.DpadRight => "D-pad Right",
        _ => b.ToString(),
    };
}

internal static class KeyNames
{
    /// <summary>Human-readable name for a Win32 virtual-key code, e.g. 0x7C -> "F13".</summary>
    public static string For(uint vk)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)vk);
        return key == Key.None ? $"VK 0x{vk:X2}" : key.ToString();
    }
}

/// <summary>One trigger-key -> pad-button mapping.</summary>
internal sealed record Mapping(uint Vk, PadButton Button)
{
    /// <summary>Readable key name; also makes the saved JSON self-documenting.</summary>
    public string KeyName => KeyNames.For(Vk);

    /// <summary>Friendly button name, bound by the mappings window's list.</summary>
    public string ButtonName => PadButtons.DisplayName(Button);
}

/// <summary>What the periodic anti-AFK pulse sends on the virtual pad.
/// Stick nudges are the least intrusive; the button entries mirror <see cref="PadButton"/>
/// by name for games whose idle timer only resets on a pressed action.</summary>
internal enum KeepAwakeAction
{
    RightStickNudge, LeftStickNudge,
    A, B, X, Y,
    LeftShoulder, RightShoulder,
    LeftTrigger, RightTrigger,
    Back, Start, Guide,
    LeftThumb, RightThumb,
    DpadUp, DpadDown, DpadLeft, DpadRight,
}

internal static class KeepAwakeActions
{
    public static string DisplayName(KeepAwakeAction a) => a switch
    {
        KeepAwakeAction.RightStickNudge => "Nudge right stick (tiny camera flick)",
        KeepAwakeAction.LeftStickNudge => "Nudge left stick (tiny movement tap)",
        _ => "Press " + PadButtons.DisplayName(ToPadButton(a)!.Value),
    };

    /// <summary>The pad button behind a button-press action; null for the stick nudges.</summary>
    public static PadButton? ToPadButton(KeepAwakeAction a)
        => Enum.TryParse<PadButton>(a.ToString(), out var b) ? b : null;
}

/// <summary>Anti-AFK: periodically send a tiny pad input that Moonlight streams
/// to the second PC, so the Star Citizen session there never idles out.
/// OnlyWhileMoonlightRuns / PauseWhileActive carry constructor defaults so
/// settings files saved by older versions pick up sensible behavior.</summary>
internal sealed record KeepAwakeSettings(
    bool Enabled,
    int IntervalSeconds,
    KeepAwakeAction Action,
    bool OnlyWhileMoonlightRuns = true,
    bool PauseWhileActive = true)
{
    public static KeepAwakeSettings Default => new(false, 60, KeepAwakeAction.RightStickNudge);

    public KeepAwakeSettings Sanitized() => this with { IntervalSeconds = Math.Clamp(IntervalSeconds, 10, 600) };
}

internal sealed record AppSettings(List<Mapping> Mappings, KeepAwakeSettings KeepAwake)
{
    public static AppSettings Default => new(MappingStore.DefaultMappings(), KeepAwakeSettings.Default);
}

internal static class MappingStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseToPad");
    private static readonly string FilePath = Path.Combine(Dir, "mappings.json");
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>F13 -> A: the original hardcoded behavior, used on first run.</summary>
    public static List<Mapping> DefaultMappings() => new() { new Mapping(0x7C, PadButton.A) };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string text = File.ReadAllText(FilePath);
                using var doc = JsonDocument.Parse(text);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // pre-1.1 format: a bare array of mappings
                    var maps = JsonSerializer.Deserialize<List<Mapping>>(text, Opts);
                    return new AppSettings(maps ?? DefaultMappings(), KeepAwakeSettings.Default);
                }

                var settings = JsonSerializer.Deserialize<AppSettings>(text, Opts);
                if (settings != null)
                    return new AppSettings(
                        settings.Mappings ?? DefaultMappings(),
                        (settings.KeepAwake ?? KeepAwakeSettings.Default).Sanitized());
            }
        }
        catch
        {
            // unreadable/corrupt file -> fall back to defaults
        }
        return AppSettings.Default;
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Opts));
    }
}

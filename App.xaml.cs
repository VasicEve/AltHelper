// MouseToPad — tiny free reWASD alternative, living in the system tray.
//
// Pipeline:  Scimitar thumb button --(iCUE)--> F13..F24 key
//            --(this app's low-level keyboard hook)--> virtual Xbox 360 button
//            --(Moonlight)--> second PC, where Star Citizen has the buttons bound.
//
// The LOCAL Star Citizen install has no gamepad bindings, so the virtual pad
// never disturbs local play — it exists purely to be picked up by Moonlight
// and streamed to the second PC. For the pad to reach the second PC while a
// local game has focus, enable Moonlight's "Process gamepad input when the
// app is in the background" option (Moonlight -> Settings -> Input).
//
// Setup:
//   1. Install the ViGEmBus driver (archived but works on Win11):
//      https://github.com/nefarius/ViGEmBus/releases  -> ViGEmBus_Setup_x64.exe
//   2. In iCUE, map each Scimitar thumb button you want to a key (F13-F24 are ideal:
//      no physical keyboard emits them, so nothing else reacts).
//   3. Build & run:  dotnet run   (run as Administrator if keys don't get picked
//      up while an elevated app is focused).
//   4. Right-click the tray icon -> "Button mappings..." to map keys to pad buttons.
//   5. In Star Citizen ON THE SECOND PC, bind actions to the Xbox buttons you chose.
//
// Mappings persist in %APPDATA%\MouseToPad\mappings.json.

using System.Windows;

namespace MouseToPad;

public partial class App : Application
{
    private static Mutex? _mutex;        // held for the app's lifetime
    private HookEngine? _engine;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // headless maintenance switches (run before the single-instance check so
        // they work while the tray app is up); results land in %TEMP%
        if (e.Args.Length > 0 && e.Args[0] is "--wipe-sc" or "--restore-sc")
        {
            List<string> results;
            var maps = ScProfiles.FindActionmaps();
            if (ScProfiles.StarCitizenRunning())
                results = new List<string> { "Star Citizen is running — close it first." };
            else if (maps.Count == 0)
                results = new List<string> { "no Star Citizen install found" };
            else
            {
                Func<string, string> op = e.Args[0] == "--wipe-sc" ? ScProfiles.Wipe : ScProfiles.Restore;
                results = maps.Select(op).ToList();
            }
            System.IO.File.WriteAllLines(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MouseToPad-sc-wipe.log"), results);
            Shutdown(maps.Count == 0 ? 1 : 0);
            return;
        }

        _mutex = new Mutex(initiallyOwned: true, "MouseToPad_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("MouseToPad is already running — look for the gamepad icon in the system tray.",
                "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        CleanUpLegacySdlIgnore();

        var settings = MappingStore.Load();
        try
        {
            _engine = new HookEngine(settings.Mappings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not create the virtual Xbox 360 controller.\n\n" +
                "Make sure the ViGEmBus driver is installed:\n" +
                "https://github.com/nefarius/ViGEmBus/releases\n\n" +
                ex.Message,
                "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _tray = new TrayController(_engine, settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _engine?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Version 1.4 briefly hid a "pulse pad" from Moonlight by writing this SDL
    /// environment variable — the opposite of what this setup needs (the pad
    /// exists to be streamed). Scrub our entry if a 1.4 run ever wrote it.
    /// </summary>
    private static void CleanUpLegacySdlIgnore()
    {
        const string Hint = "SDL_GAMECONTROLLER_IGNORE_DEVICES";
        const string LegacyId = "0x1209/0x4d54";
        try
        {
            string? current = Environment.GetEnvironmentVariable(Hint, EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(current))
                return;
            var kept = current
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(id => !id.Equals(LegacyId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            string? rewritten = kept.Length == 0 ? null : string.Join(",", kept);
            if (rewritten != current)
                Environment.SetEnvironmentVariable(Hint, rewritten, EnvironmentVariableTarget.User);
        }
        catch
        {
            // non-fatal
        }
    }
}

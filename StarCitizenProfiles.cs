using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MouseToPad;

/// <summary>
/// Wipes gamepad bindings from the local Star Citizen profile (actionmaps.xml)
/// and restores them. The backup IS the original file, renamed in place — so
/// Restore is just a rename back.
///
/// actionmaps.xml only stores bindings the player has changed; an entry like
/// input="gp1_y" rebinds an action, and input="gp1_" (blank suffix) explicitly
/// binds it to nothing, overriding the stock default too. Wipe therefore blanks
/// every gamepad entry in the file. Actions still on stock defaults have no
/// entry to blank — those are cleared once in-game (Options -> Keybindings ->
/// Advanced Controls Customization), after which they appear in the file and
/// are covered by future wipes.
/// </summary>
internal static class ScProfiles
{
    public const string BackupSuffix = ".mousetopad-backup";

    private static readonly Regex GamepadInput = new(@"^gp\d+_", RegexOptions.Compiled);

    public static bool StarCitizenRunning()
    {
        var procs = System.Diagnostics.Process.GetProcessesByName("StarCitizen");
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }

    /// <summary>actionmaps.xml of every installed channel (LIVE, PTU, EPTU, …),
    /// including ones that currently only have a backup waiting to be restored.</summary>
    public static List<string> FindActionmaps()
    {
        var found = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed) continue;
            string root = Path.Combine(drive.RootDirectory.FullName,
                "Program Files", "Roberts Space Industries", "StarCitizen");
            if (!Directory.Exists(root)) continue;
            foreach (string channel in Directory.GetDirectories(root))
            {
                string maps = Path.Combine(channel, "user", "client", "0", "Profiles", "default", "actionmaps.xml");
                if (File.Exists(maps) || File.Exists(maps + BackupSuffix))
                    found.Add(maps);
            }
        }
        return found;
    }

    /// <summary>Channel name (LIVE/PTU/…) for display, derived from the path:
    /// …\StarCitizen\CHANNEL\user\client\0\Profiles\default\actionmaps.xml</summary>
    public static string ChannelOf(string mapsPath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(mapsPath)!);   // …\default
        for (int i = 0; i < 5 && dir.Parent != null; i++)                // default→Profiles→0→client→user→CHANNEL
            dir = dir.Parent;
        return dir.Name;
    }

    /// <summary>Blank every gamepad binding in the profile — both the ones saved
    /// in the file AND the game's stock defaults. actionmaps.xml only stores
    /// deltas, so defaults are killed by INJECTING an explicit blank rebind
    /// (input="gp1_") for every action in <see cref="ScGamepadDefaults"/>.
    /// The first wipe renames the original to *.mousetopad-backup and writes the
    /// wiped copy in its place; later wipes keep that pristine backup.</summary>
    public static string Wipe(string mapsPath)
    {
        try
        {
            XDocument doc;
            if (File.Exists(mapsPath))
            {
                doc = XDocument.Load(mapsPath);
            }
            else
            {
                // never-customized install: start from the game's own skeleton
                doc = new XDocument(
                    new XElement("ActionMaps",
                        new XElement("ActionProfiles",
                            new XAttribute("version", "1"),
                            new XAttribute("optionsVersion", "2"),
                            new XAttribute("rebindVersion", "2"),
                            new XAttribute("profileName", "default"))));
            }

            var profile = doc.Root?.Elements("ActionProfiles")
                .FirstOrDefault(p => (string?)p.Attribute("profileName") is null or "default")
                ?? doc.Root?.Elements("ActionProfiles").FirstOrDefault();
            if (profile == null)
                return $"{ChannelOf(mapsPath)}: FAILED — unrecognized actionmaps.xml layout.";

            // 1) blank any gamepad rebind already stored in the file
            int blanked = 0;
            foreach (var rebind in doc.Descendants("rebind"))
            {
                string? input = (string?)rebind.Attribute("input");
                if (input == null) continue;
                var match = GamepadInput.Match(input);
                if (!match.Success || input.Length == match.Length)
                    continue;                                   // not a gamepad bind, or already blank
                rebind.SetAttributeValue("input", match.Value);  // "gp1_xxx" -> "gp1_"
                blanked++;
            }

            // 2) inject blanks for every stock default gamepad binding
            int injected = 0;
            foreach (string pair in ScGamepadDefaults.Pairs)
            {
                int sep = pair.IndexOf('|');
                string mapName = pair[..sep], actionName = pair[(sep + 1)..];

                var actionmap = profile.Elements("actionmap")
                    .FirstOrDefault(e => (string?)e.Attribute("name") == mapName);
                if (actionmap == null)
                {
                    actionmap = new XElement("actionmap", new XAttribute("name", mapName));
                    profile.Add(actionmap);
                }

                var action = actionmap.Elements("action")
                    .FirstOrDefault(e => (string?)e.Attribute("name") == actionName);
                if (action == null)
                {
                    action = new XElement("action", new XAttribute("name", actionName));
                    actionmap.Add(action);
                }

                bool hasGamepadRebind = action.Elements("rebind")
                    .Any(r => GamepadInput.IsMatch((string?)r.Attribute("input") ?? ""));
                if (!hasGamepadRebind)
                {
                    action.Add(new XElement("rebind", new XAttribute("input", "gp1_")));
                    injected++;
                }
            }

            if (blanked == 0 && injected == 0)
                return $"{ChannelOf(mapsPath)}: already wiped — nothing to do.";

            string backup = mapsPath + BackupSuffix;
            if (File.Exists(mapsPath) && !File.Exists(backup))
                File.Move(mapsPath, backup);                    // the rename IS the backup
            doc.Save(mapsPath);

            string backupNote = File.Exists(backup) ? $"; original kept as {Path.GetFileName(backup)}" : "";
            return $"{ChannelOf(mapsPath)}: cleared {blanked} saved binding(s) and overrode {injected} stock default(s){backupNote}.";
        }
        catch (Exception ex)
        {
            return $"{ChannelOf(mapsPath)}: FAILED — {ex.Message}";
        }
    }

    /// <summary>Rename the backup back over the working file.</summary>
    public static string Restore(string mapsPath)
    {
        try
        {
            string backup = mapsPath + BackupSuffix;
            if (!File.Exists(backup))
                return $"{ChannelOf(mapsPath)}: no backup found — nothing to restore.";
            if (File.Exists(mapsPath))
                File.Delete(mapsPath);
            File.Move(backup, mapsPath);
            return $"{ChannelOf(mapsPath)}: original gamepad bindings restored.";
        }
        catch (Exception ex)
        {
            return $"{ChannelOf(mapsPath)}: FAILED — {ex.Message}";
        }
    }
}

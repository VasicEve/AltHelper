using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace MouseToPad;

/// <summary>
/// Modeless window for editing trigger-key -> pad-button mappings and the
/// anti-AFK settings. Mappings are edited in place in the grid (double-click
/// a cell); new ones come from the key dropdown — which includes F13-F24 that
/// no physical keyboard can type — or from a global key capture. Save applies
/// everything to the engine and persists it; Cancel discards.
/// </summary>
public partial class MappingsWindow : Window
{
    private readonly HookEngine _engine;
    private readonly AppSettings _initial;
    private readonly ObservableCollection<MappingRow> _rows = new();
    private readonly ObservableCollection<KeyChoice> _keyChoices = new();
    private bool _capturing;

    private const string CaptureIdleText = "Capture key…";
    private const string CaptureArmedText = "Press a key…  (Esc cancels)";

    internal MappingsWindow(HookEngine engine, AppSettings settings)
    {
        _engine = engine;
        _initial = settings;
        InitializeComponent();

        foreach (uint vk in CuratedVks())
            _keyChoices.Add(new KeyChoice(vk, KeyNames.For(vk)));

        var buttonChoices = Enum.GetValues<PadButton>()
            .Select(b => new PadChoice(b, PadButtons.DisplayName(b)))
            .ToList();

        foreach (var mapping in _engine.Mappings)
        {
            EnsureKeyChoice(mapping.Vk);   // saved keys outside the curated list still display
            _rows.Add(new MappingRow { Vk = mapping.Vk, Button = mapping.Button });
        }

        KeyColumn.ItemsSource = _keyChoices;
        ButtonColumn.ItemsSource = buttonChoices;
        MappingGrid.ItemsSource = _rows;

        NewKeyCombo.ItemsSource = _keyChoices;
        NewKeyCombo.SelectedIndex = 0;     // F13 — the headline use case
        ButtonCombo.ItemsSource = buttonChoices;
        ButtonCombo.SelectedIndex = 0;

        KeepAwakeActionCombo.ItemsSource = Enum.GetValues<KeepAwakeAction>()
            .Select(a => new ActionChoice(a, KeepAwakeActions.DisplayName(a)))
            .ToList();
        var ka = settings.KeepAwake;
        KeepAwakeEnabledBox.IsChecked = ka.Enabled;
        KeepAwakeIntervalBox.Text = ka.IntervalSeconds.ToString();
        KeepAwakeActionCombo.SelectedIndex = (int)ka.Action;   // combo follows enum order
        KeepAwakeMoonlightOnlyBox.IsChecked = ka.OnlyWhileMoonlightRuns;
        KeepAwakePauseActiveBox.IsChecked = ka.PauseWhileActive;
    }

    /// <summary>F13-F24 first (iCUE's "ghost" keys), then the everyday keys.</summary>
    private static IEnumerable<uint> CuratedVks()
    {
        for (uint vk = 0x7C; vk <= 0x87; vk++) yield return vk;   // F13..F24
        for (uint vk = 0x70; vk <= 0x7B; vk++) yield return vk;   // F1..F12
        for (uint vk = 0x41; vk <= 0x5A; vk++) yield return vk;   // A..Z
        for (uint vk = 0x30; vk <= 0x39; vk++) yield return vk;   // 0..9
        for (uint vk = 0x60; vk <= 0x69; vk++) yield return vk;   // NumPad 0..9
        // arrows, nav cluster, misc
        foreach (uint vk in new uint[] { 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22, 0x13, 0x91 })
            yield return vk;
    }

    private KeyChoice EnsureKeyChoice(uint vk)
    {
        var existing = _keyChoices.FirstOrDefault(k => k.Vk == vk);
        if (existing != null)
            return existing;
        var added = new KeyChoice(vk, KeyNames.For(vk));
        _keyChoices.Add(added);
        return added;
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            StopCapture();
            return;
        }
        _capturing = true;
        CaptureButton.Content = CaptureArmedText;
        _engine.BeginCapture(vk =>
        {
            _capturing = false;
            CaptureButton.Content = CaptureIdleText;
            if (vk == 0)                                   // Esc — cancelled
                return;
            NewKeyCombo.SelectedItem = EnsureKeyChoice(vk);
        });
    }

    private void StopCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        _engine.CancelCapture();
        CaptureButton.Content = CaptureIdleText;
    }

    private void NewKeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => AddButton.IsEnabled = NewKeyCombo.SelectedItem != null;

    private void MappingGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RemoveButton.IsEnabled = MappingGrid.SelectedItem != null;

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (NewKeyCombo.SelectedItem is not KeyChoice key) return;
        var button = ((PadChoice)ButtonCombo.SelectedItem!).Button;

        // one button per key: update the existing row if the key is already mapped
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Vk == key.Vk)
            {
                _rows[i] = new MappingRow { Vk = key.Vk, Button = button };   // replace so the grid refreshes
                MappingGrid.SelectedIndex = i;
                return;
            }
        }

        _rows.Add(new MappingRow { Vk = key.Vk, Button = button });
        MappingGrid.SelectedIndex = _rows.Count - 1;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (MappingGrid.SelectedItem is MappingRow row)
            _rows.Remove(row);
    }

    private void WipeScButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScProfiles.StarCitizenRunning())
        {
            MessageBox.Show(this,
                "Close Star Citizen first — it rewrites its settings file on exit, which would undo the wipe.",
                "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var maps = FindOrBrowseActionmaps();
        if (maps.Count == 0) return;

        string fileList = string.Join("\n", maps.Select(m => "  •  " + m));
        if (MessageBox.Show(this,
            "This blanks every gamepad binding in your local Star Citizen profile(s) — both saved " +
            "customizations and the game's stock defaults:\n\n" +
            fileList + "\n\n" +
            $"The original file is kept next to it, renamed with a {ScProfiles.BackupSuffix} suffix, " +
            "so Restore can bring everything back.\n\nContinue?",
            "MouseToPad", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        string results = string.Join("\n", maps.Select(ScProfiles.Wipe));
        MessageBox.Show(this,
            results + "\n\nYour local Star Citizen will now ignore the virtual pad. " +
            "If a future game patch adds brand-new gamepad actions, run the wipe again after updating MouseToPad.",
            "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestoreScButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScProfiles.StarCitizenRunning())
        {
            MessageBox.Show(this,
                "Close Star Citizen first — restoring while it runs would be overwritten when it exits.",
                "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var maps = FindOrBrowseActionmaps();
        if (maps.Count == 0) return;

        string results = string.Join("\n", maps.Select(ScProfiles.Restore));
        MessageBox.Show(this, results, "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Auto-detected channel profiles, or a folder picker if the game
    /// isn't installed in the default location.</summary>
    private List<string> FindOrBrowseActionmaps()
    {
        var maps = ScProfiles.FindActionmaps();
        if (maps.Count > 0) return maps;

        MessageBox.Show(this,
            "Couldn't find a Star Citizen install in the default location. " +
            "Pick your channel folder (e.g. ...\\Roberts Space Industries\\StarCitizen\\LIVE) in the next dialog.",
            "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Information);
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select your Star Citizen channel folder (e.g. LIVE)",
        };
        if (dialog.ShowDialog(this) == true)
            maps.Add(System.IO.Path.Combine(dialog.FolderName,
                "user", "client", "0", "Profiles", "default", "actionmaps.xml"));
        return maps;
    }

    /// <summary>Fire one pulse immediately with the action currently selected in
    /// the dropdown (saved or not), bypassing the Moonlight/activity gates —
    /// the user clicked a button, so they are by definition "active".</summary>
    private async void TestPulseButton_Click(object sender, RoutedEventArgs e)
    {
        var action = ((ActionChoice)KeepAwakeActionCombo.SelectedItem!).Action;
        TestPulseButton.IsEnabled = false;
        try
        {
            TestPulseButton.Content = "Sending…";
            await _engine.KeepAwakePulseAsync(action);
            TestPulseButton.Content = "Sent ✓";
            await Task.Delay(900);   // brief confirmation before reverting
        }
        finally
        {
            TestPulseButton.Content = "Test";
            TestPulseButton.IsEnabled = true;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        MappingGrid.CommitEdit(DataGridEditingUnit.Row, true);   // flush an in-progress cell edit

        var duplicate = _rows.GroupBy(r => r.Vk).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            MessageBox.Show(this,
                $"Two mappings use the same trigger key ({KeyNames.For(duplicate.Key)}). Each key can only trigger one button.",
                "MouseToPad", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int interval = int.TryParse(KeepAwakeIntervalBox.Text.Trim(), out var s)
            ? s
            : _initial.KeepAwake.IntervalSeconds;
        var keepAwake = new KeepAwakeSettings(
            KeepAwakeEnabledBox.IsChecked == true,
            interval,
            ((ActionChoice)KeepAwakeActionCombo.SelectedItem!).Action,
            KeepAwakeMoonlightOnlyBox.IsChecked == true,
            KeepAwakePauseActiveBox.IsChecked == true).Sanitized();

        var settings = new AppSettings(
            _rows.Select(r => new Mapping(r.Vk, r.Button)).ToList(),
            keepAwake);
        _engine.SetMappings(settings.Mappings);
        MappingStore.Save(settings);   // TrayController re-reads this when we close
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        StopCapture();
        base.OnClosed(e);
    }

    /// <summary>Mutable row so the DataGrid can edit cells in place.</summary>
    internal sealed class MappingRow
    {
        public uint Vk { get; set; }
        public PadButton Button { get; set; }
    }

    internal sealed record KeyChoice(uint Vk, string Name);

    private sealed record PadChoice(PadButton Button, string Name);

    private sealed record ActionChoice(KeepAwakeAction Action, string Name);
}

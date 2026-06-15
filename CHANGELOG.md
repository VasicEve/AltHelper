# Changelog

All notable changes to MouseToPad. Dates are release dates.

## 1.8.0 — 2026-06-11
- **Wipe / Restore local Star Citizen gamepad bindings**: one button renames
  `actionmaps.xml` to a backup and writes a copy where every gamepad binding is
  bound to nothing — saved customizations are blanked AND all ~240 stock
  defaults are overridden with explicit blank rebinds (action list baked in from
  the game's SC 4.7 `defaultProfile.xml`). Restore renames the backup back.
  Auto-detects all installed channels (LIVE/PTU/…) and refuses to run while the
  game is open. Headless flags `--wipe-sc` / `--restore-sc` do the same from a
  terminal.

## 1.7.0 — 2026-06-11
- **Test button** in the keep-awake settings: fires one pulse immediately with the
  currently selected action, bypassing the Moonlight/activity gates.

## 1.6.0 — 2026-06-11
- Mapping editor overhaul: edit existing mappings **in place** (double-click a grid cell).
- Trigger keys can be picked from a **dropdown including F13–F24** — no capture needed
  for keys that don't exist on a physical keyboard. Global key capture still available.
- Duplicate-trigger-key validation on save; Delete key removes the selected row.

## 1.5.0 — 2026-06-11
- Corrected the streaming topology: the virtual pad always flows to Moonlight
  (local game has no gamepad bindings, so the pad never disturbs local play).
- Keep-awake gates reworked: **"Only while Moonlight is running"** and
  **"Pause while actively using the Moonlight window"** (focused + input in last 30 s).
- Scrubs the legacy SDL ignore-devices environment variable if 1.4.0 ever wrote it.

## 1.4.0 — 2026-06-11
- (Superseded by 1.5.0) Experimented with a second, hidden controller for pulses.

## 1.3.0 — 2026-06-11
- Keep-awake pauses while the user is actively present (input-activity detection).

## 1.2.0 — 2026-06-11
- Anti-AFK input fully randomized: ±40% timer jitter, random direction/strength/
  duration, eased stick motion instead of square-wave steps, 1–2 movements per pulse.

## 1.1.0 — 2026-06-11
- **Keep player active (anti-AFK)**: periodic stick nudge or button tap on a timer,
  configurable in the mappings window, toggle in the tray menu.
- Settings file gained a `KeepAwake` section (old format migrates automatically).

## 1.0.0 — 2026-06-11
- First release: WPF system-tray app with Enable/Disable/Exit, mapping editor with
  global key capture, ViGEm virtual Xbox 360 pad, JSON-persisted mappings,
  Inno Setup installer with start-with-Windows and desktop shortcut options.

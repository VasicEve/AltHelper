# AltHelper (MouseToPad)

[![build](https://github.com/VasicEve/AltHelper/actions/workflows/build.yml/badge.svg)](https://github.com/VasicEve/AltHelper/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/VasicEve/AltHelper)](https://github.com/VasicEve/AltHelper/releases/latest)
[![license](https://img.shields.io/github/license/VasicEve/AltHelper)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)

A tiny free **reWASD alternative** for a two-PC Star Citizen setup. MouseToPad turns
keystrokes that your mouse software emits (F13–F24 from Corsair iCUE, for example)
into **virtual Xbox 360 controller** input — which Moonlight streams to a second PC
where your alt account has those buttons bound. It can also keep that remote session
from idling out with randomized, human-looking anti-AFK input.

## How it works

```
Scimitar thumb button
        │  iCUE macro
        ▼
F13–F24 keystroke ──► low-level keyboard hook (MouseToPad, swallows the key)
        │
        ▼
virtual Xbox 360 pad (ViGEmBus)
        │
        ▼
Moonlight ──── stream ────► second PC ──► Star Citizen (alt account)
```

- The **local** machine never reacts to the pad — the local game simply has no
  gamepad bindings, so the virtual controller exists purely to be streamed.
- F13–F24 are ideal trigger keys: no physical keyboard emits them, so nothing
  else on the system ever responds to them. Any macro software that can send
  them works, not just iCUE.

## Features

- **System tray app** — Enable / Disable, mapping editor, exit; green/grey icon shows state
- **Mapping editor** — map any key to any Xbox 360 control (including full-press LT/RT):
  - edit existing rows in place (double-click a cell)
  - pick trigger keys from a dropdown that includes F13–F24
  - or capture any key globally with one press
- **Keep player active (anti-AFK)** — periodic stick nudges or button taps with
  randomized timing (±40% jitter), direction, strength, and eased motion so the
  input stream looks human, not scripted. Gates: only while Moonlight is running,
  and it pauses while you're actively using the Moonlight window. A **Test**
  button fires one pulse on demand.
- **Persistent settings** — `%APPDATA%\MouseToPad\mappings.json`, human-readable
- **Installer** — per-user (no UAC), optional start-with-Windows and desktop shortcut
- Single-instance, stuck-key protection, real input always wins over synthetic input

## Requirements

| What | Why |
|---|---|
| Windows 10/11 | Win32 keyboard hook + WPF |
| [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | app runtime |
| [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) | the virtual Xbox 360 controller |
| [Moonlight](https://moonlight-stream.org/) + [Sunshine](https://github.com/LizardByte/Sunshine) | streaming the pad to the second PC |
| iCUE (or any macro tool emitting F13–F24) | turning mouse buttons into trigger keys |

## Getting started

1. Install **ViGEmBus** on this PC, and grab `MouseToPadSetup.exe` from
   [Releases](https://github.com/VasicEve/AltHelper/releases/latest) (or build it — see below).
2. In **iCUE**, map each Scimitar thumb button you want to a key (F13–F24 recommended).
3. Right-click the **tray icon → Button mappings…** and map those keys to pad buttons.
4. In **Star Citizen on the second PC**, bind your actions to those Xbox buttons.
5. In **Moonlight → Settings → Input**, enable
   **“Process gamepad input when the app is in the background”** so the pad reaches
   the second PC even while a local game has focus.

> Run MouseToPad as Administrator if trigger keys stop working while an elevated
> app has focus — a non-elevated hook can't intercept keys destined for elevated windows.

## Keep player active (anti-AFK)

Enable it from the tray menu or the mappings window. Every *roughly* N seconds
(you set the midpoint; actual timing lands at 60–140% of it) the app sends a small
randomized input on the same virtual pad, which Moonlight forwards to the remote
session so it never hits the idle-logout timer.

| Setting | Effect |
|---|---|
| Action | nudge right/left stick (eased, random direction/strength) or tap any button |
| Only while Moonlight is running | don't bother pulsing into the void |
| Pause while actively using Moonlight | no camera flicks while *you* are flying — pulses hold off when Moonlight is focused and there was input in the last 30 s |
| Test | fire one pulse right now, bypassing the gates |

## Building from source

```powershell
git clone https://github.com/VasicEve/AltHelper.git
cd AltHelper
dotnet build MouseToPad.slnx -c Release      # needs .NET SDK 9.0.2xx+ for .slnx (app targets net8.0-windows)
```

Open `MouseToPad.slnx` in Visual Studio 2022/2026 for the full experience — the
run-target dropdown includes a **“Build installer (Inno Setup)”** profile.

To produce the installer from a terminal (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php),
`winget install -e --id JRSoftware.InnoSetup --scope user`):

```powershell
.\scripts\build-installer.ps1 -NoLaunch      # → installer\MouseToPadSetup.exe
```

## Repository layout

| Path | Purpose |
|---|---|
| `HookEngine.cs` | keyboard hook, virtual pad, key capture, anti-AFK pulses |
| `TrayController.cs` | tray icon, menu, pulse timer and its gates |
| `MappingsWindow.xaml(.cs)` | mapping editor + keep-awake settings UI |
| `Mappings.cs` | settings model + JSON persistence |
| `FocusWatch.cs` | foreground-window and idle detection |
| `App.xaml(.cs)` | startup, single-instance guard, setup notes |
| `installer.iss` | Inno Setup script (startup entry, desktop shortcut) |
| `scripts/` | `build-installer.ps1`, `make-icon.ps1` (regenerates `app.ico`) |

## License

[MIT](LICENSE)

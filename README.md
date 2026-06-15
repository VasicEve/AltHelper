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

- Your **own** machine never reacts to the pad — your local game simply has no
  gamepad bindings, so the virtual controller exists purely to be streamed.
- F13–F24 are ideal trigger keys: no physical keyboard has them, so nothing
  else on your system will ever react to them. Any macro software that can
  send them works, not just iCUE.

## Features

- **System tray app** — Enable / Disable, mapping editor, exit; green/grey icon shows state
- **Mapping editor** — map any key to any Xbox 360 control (including full-press LT/RT):
  edit rows in place, pick trigger keys from a dropdown that includes F13–F24,
  or capture any key with one press
- **Keep player active (anti-AFK)** — periodic stick nudges or button taps with
  randomized timing, direction, strength, and eased motion so the input stream
  looks human, not scripted — with a **Test** button to fire one on demand
- **Wipe / Restore local SC gamepad bindings** — one click blanks every gamepad
  binding saved in your local Star Citizen profile (with an automatic backup and
  a restore button), so your own game ignores the virtual pad
- **Persistent settings** — `%APPDATA%\MouseToPad\mappings.json`, human-readable
- **Installer** — per-user (no admin needed), optional start-with-Windows and desktop shortcut

## What you need

Two Windows 10/11 PCs on the same network. In this guide they are:

| Name | What it is | What gets installed on it |
|---|---|---|
| **YOUR PC** | the one you sit at and play your main account on | MouseToPad, Moonlight, ViGEmBus, iCUE |
| **ALT PC** | the one running the alt account's Star Citizen | Sunshine |

Everything below is free.

---

## Setup guide

Do the parts **in order**. Each part ends with a ✅ check — don't continue until
the check passes.

### Part 1 — ALT PC: install Sunshine (the streaming host)

1. On the **ALT PC**, open a web browser and go to the
   [Sunshine releases page](https://github.com/LizardByte/Sunshine/releases/latest).
2. Under **Assets**, download the file ending in **`-windows-installer.exe`** and run it.
   - If Windows shows a blue **"Windows protected your PC"** box, click
     **More info**, then **Run anyway**.
3. Click through the installer with the default options.
4. When it finishes, open a browser **on the ALT PC** and go to
   `https://localhost:47990`
   - The browser will warn **"Your connection is not private"** — that's normal
     (Sunshine uses a self-signed certificate). Click **Advanced**, then
     **Continue / Proceed to localhost**.
5. Sunshine asks you to **create a username and password**. Pick anything —
   write it down — and log in with it.

> 🖥️ **No monitor on the ALT PC?** Sunshine can only stream a screen that
> exists. If the ALT PC runs headless, buy a cheap **HDMI dummy plug** (a few
> dollars) and leave it in a video port — otherwise the stream will be black.

**✅ Check:** you are looking at the Sunshine web page on the ALT PC and you are
logged in.

### Part 2 — YOUR PC: install Moonlight (the streaming client) and pair it

1. On **YOUR PC**, press the **Windows key**, type `powershell`, press Enter, and run:
   ```powershell
   winget install -e --id MoonlightGameStreamingProject.Moonlight
   ```
   (Or download it from [moonlight-stream.org](https://moonlight-stream.org/).)
2. Start **Moonlight** (Start menu → type "Moonlight").
3. Moonlight automatically finds the ALT PC and shows it as a computer tile.
   Click the tile. Moonlight now shows a **4-digit PIN**. Leave this on screen.
   - If no tile appears, make sure both PCs are on the same network and that
     Sunshine is running on the ALT PC, then click the ↻ refresh button.
4. On the **ALT PC**, go back to the Sunshine web page and open the **PIN** tab
   (or browse to `https://localhost:47990/pin`). Type in the 4-digit PIN from
   Moonlight, give the device any name, and click **Send**.
5. Back on **YOUR PC**, the tile unlocks. Click it, then click **Desktop**.
   You should now see the ALT PC's screen inside a window on YOUR PC.
   - To leave the stream at any time press **Ctrl+Alt+Shift+Q**.
6. One required setting: in Moonlight click the ⚙ **Settings** gear, open the
   **Input** section, and turn ON
   **“Process gamepad input when the app is in the background.”**
   Without this, the controller only works while the Moonlight window is focused
   — and the whole point is using it while you play your own game.

**✅ Check:** you can see and control the ALT PC's desktop from YOUR PC, and the
background-gamepad setting is ON.

### Part 3 — YOUR PC: install the ViGEmBus driver

This driver is what lets MouseToPad create a fake Xbox 360 controller.

1. On **YOUR PC**, go to the
   [ViGEmBus releases page](https://github.com/nefarius/ViGEmBus/releases)
   and download **`ViGEmBus_Setup_x64.exe`** from the newest release's Assets.
2. Run it and click through with the defaults (this one does ask for admin —
   say Yes).

**✅ Check:** the installer finished without errors. (MouseToPad will tell you
loudly in Part 4 if the driver is missing.)

### Part 4 — YOUR PC: install MouseToPad

1. On **YOUR PC**, download **`MouseToPadSetup.exe`** from the
   [latest release](https://github.com/VasicEve/AltHelper/releases/latest)
   (it's under **Assets**).
2. Run it. If Windows shows **"Windows protected your PC"**, click
   **More info → Run anyway** (the app is unsigned, that's all).
3. Leave both checkboxes ticked (**desktop shortcut** and **start automatically
   when you sign in**) and finish the installer.
4. MouseToPad starts and lives in the **system tray** — the row of little icons
   next to the clock, bottom-right of your screen. If you don't see a **green
   gamepad icon**, click the **^** arrow next to the clock; it's hiding in
   there. (Tip: drag it onto the taskbar so it's always visible.)

**✅ Check:** a green gamepad icon is in your tray. If instead you got an error
about a missing virtual controller, redo Part 3.

### Part 5 — YOUR PC: make your mouse send trigger keys (iCUE)

The goal: each thumb button you want to use must send a keystroke. F13–F24 are
perfect because no real keyboard has them.

1. Open **iCUE**, click your **Scimitar**, and open **Key Assignments**.
2. Create a new assignment for a thumb button of type **Macro / Keystroke** and
   set it to a key in the **F13–F24** range. Repeat for each thumb button you
   want, using a different key for each (F13, F14, F15, …).
3. **Can't get iCUE to record F13** (you can't press a key that doesn't exist)?
   Two options:
   - Some iCUE versions let you type/select the key instead of recording it — look
     for an edit option on the recorded keystroke.
   - Or just use ordinary keys you never use (F9, Pause, etc.). MouseToPad
     **swallows** trigger keys system-wide, so nothing else will see them anyway.

**✅ Check (the fun one):** right-click the green tray icon →
**Button mappings…** → click the **Capture key…** button → press a thumb
button on your mouse. The box should select the key it sent (e.g. **F13**).
If nothing happens, iCUE isn't sending the key — check the right profile is
active.

### Part 6 — YOUR PC: map the keys to controller buttons

1. Right-click the green tray icon → **Button mappings…**
2. For each thumb button: pick its key in the dropdown (or use **Capture key…**
   and press the thumb button), pick the Xbox button it should press
   (A, B, LB, RT, …), and click **Add**.
3. Click **Save**.
4. Prove it works: press **Win+R**, type `joy.cpl`, press Enter. Double-click
   **Xbox 360 Controller for Windows**. Now press your thumb buttons — the
   matching buttons light up in that window.

**✅ Check:** thumb buttons light up buttons in `joy.cpl`.

### Part 7 — ALT PC: bind the buttons in Star Citizen

1. On **YOUR PC**, open the Moonlight stream of the ALT PC and start
   Star Citizen on it (your alt account).
2. In the streamed Star Citizen: **Options → Keybindings**, choose the action
   you want, select the **gamepad** column, and when it says "press a key",
   press the **thumb button on your mouse**. Because the Moonlight window is
   focused, the virtual pad press travels down the stream and SC binds it.
3. Repeat for each action/button.

**✅ Check:** with the Moonlight window focused, pressing a thumb button makes
the alt do the thing. Then click back into your own game and try again — thanks
to the background-gamepad setting from Part 2, it should **still** work.

### Part 8 — optional: keep the alt logged in (anti-AFK)

1. Right-click the green tray icon and tick **Keep player active**.
2. Fine-tune it in **Button mappings…** under *Keep player active*: how often
   (roughly — actual timing is randomized), and what input to send (a tiny
   stick nudge is the default and the least intrusive).
3. Click **Test** to fire one pulse right now — watch the streamed Star Citizen
   camera twitch.

It automatically holds off while **you** are actively flying the alt (Moonlight
focused + recent input), and does nothing if Moonlight isn't running.

### Part 9 — optional: stop your local Star Citizen reacting to the pad

Star Citizen ships with default gamepad bindings, so the copy on YOUR PC would
also react to the virtual pad while it's focused (B is *eject* by default — you
want this part). One step, done once:

1. **Close Star Citizen**, then in MouseToPad open **Button mappings…** and
   click **Wipe gamepad bindings…** (under *Local Star Citizen gamepad
   bindings*). It backs up `actionmaps.xml` (renamed alongside the original),
   then writes a copy where **every gamepad binding is bound to nothing** —
   your saved binds *and* all ~240 stock defaults, whose list is baked in from
   the game's own SC 4.7 profile. All installed channels (LIVE/PTU/…) are
   covered.

**Restore backup…** puts the original file back exactly as it was. MouseToPad
refuses to touch the files while Star Citizen is running (the game rewrites
them on exit). If a future patch adds brand-new gamepad actions, update
MouseToPad and run the wipe again.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| "Windows protected your PC" when running an installer | **More info → Run anyway** |
| No green icon in the tray | Click the **^** arrow next to the clock — it's hidden there |
| MouseToPad error: *could not create the virtual controller* | Install ViGEmBus (Part 3) |
| **Capture key…** sees nothing when pressing a thumb button | iCUE isn't sending the key — check the assignment and active profile (Part 5) |
| Buttons work in `joy.cpl` but not in the stream | Moonlight's **background gamepad** setting is off (Part 2, step 6) |
| Buttons work only while the Moonlight window is focused | Same fix — background gamepad setting (Part 2, step 6) |
| The Moonlight stream is a black screen | The ALT PC has no display — use an HDMI dummy plug (Part 1) |
| Trigger keys stop working when an admin program has focus | Right-click MouseToPad → run as Administrator |
| My own Star Citizen reacts to thumb buttons / camera flicks | Wipe its gamepad bindings (Part 9) |
| The alt still gets logged out | Make sure **Keep player active** is ticked in the tray menu and Moonlight is running; click **Test** to confirm the pulse arrives |

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

# Dirt Sweeper

A small helper for the Steam game **Dirt Clicker**. It sweeps the mouse over the dig field so tools get hovered without you doing it, and plays the red-button timer minigame by itself. It runs as a compact HUD bar on top of the game.

Unofficial fan-made tool, not affiliated with the game's developers.

- **One small exe** (about 85 KB) with no installer. It runs on any Windows 10/11 PC using the built-in .NET Framework 4.8.
- **Light on your PC:** almost no CPU when idle. While sweeping or watching the timer it does nothing but move the mouse and look at a small part of the screen.
- **Finds the game window by itself** and scales to its resolution, aspect ratio and monitor refresh rate.

## Features

### Sweeper
The game checks what the cursor is over once per frame. The sweeper moves the cursor from spot to spot and holds each one for a few frames. The spots form a hexagonal grid sized to your hover circle, which is the fewest spots that still leave no gaps. A bigger circle means fewer spots and faster passes.

- The hover circle size comes from your in-game **collision radius**. Calibrate it once with the green ring (F7), then just update the number when you buy upgrades.
- Hold time is set in game frames, so it stays right at any FPS.
- It only sweeps while the game is in front, and stops when you move the mouse yourself.

### Timer minigame
Each level shows `0N.00`, beeps, counts down visibly for about 0.75 s, then hides. Dirt Sweeper watches the timer digits and treats the moment they start changing as the start of the countdown. It then clicks the red button exactly N seconds later, for every level.

- It only looks at light-grey digit pixels, so the orange dirt particles flying past are ignored.
- A countdown has to keep changing the digits for at least 0.5 s, and change them a lot compared with the still `0N.00`. A particle drifting over the digits, or the screen switching between levels, doesn't count.
- If clicks are consistently early or late, the offset setting corrects for it.

### HUD and guides
- The HUD bar floats over the game. Clicking it never takes focus away from the game, and the mouse passes through it while the app is moving the cursor. It hides when you switch to another app, and you can drag it anywhere.
- The guides (F6) draw the dig field, the sweep spots, the timer box and the button position over the game, so you can check everything lines up.

## Usage

1. Download `DirtSweeper.exe` from the [Releases](../../releases) page, or build it yourself (see below).
2. Run it. It finds Dirt Clicker by itself; if the game isn't running, the HUD has a button to launch it.
3. Open settings with the ⚙ button on the HUD (or double-click the tray icon), set your collision radius and calibrate once.

| Key | Action |
| --- | --- |
| F5 | Start / stop sweeping |
| F6 | Show / hide guides |
| F7 | Calibration ring around the cursor |
| F8 | Arm / cancel the timer game (then click Play) |
| F9 | Show / hide the HUD |

Settings are saved to `DirtSweeper.settings.json` next to the exe. If that folder isn't writable, they go to `%AppData%\DirtSweeper`.

Positions in the settings use the game's 1920×1080 layout. The app scales them to your real window size. Elements in the right-hand shop panel follow its right edge on screens wider than 16:9.

## Building

You need the [.NET SDK](https://dot.net) (any recent version; it builds against .NET Framework 4.8).

```
build.cmd
```

This produces `dist\DirtSweeper.exe`. You can also open `DirtSweeper.sln` in Visual Studio or Rider.

GitHub Actions builds the exe on every push. Pushing a tag such as `v2.1.0` creates a release with the exe attached.

## Project layout

```
src/DirtSweeper/
  Program.cs        entry point, single-instance handling
  App.cs            wires the HUD, guides, settings, sweeper and minigames together
  Ui.cs             theme, the HUD bar and the guides overlay
  SettingsForm.cs   the settings window
  Sweeper.cs        hex-grid sweep path and the sweep loop
  TimerGame.cs      Minigame base class, screen capture, and the timer minigame
  Game.cs           finds the game window and maps 1080p layout to screen pixels
  Native.cs         Win32 calls: mouse input, monitors, GDI capture, precise timing
  Settings.cs       saved settings and their defaults
```

### Adding a minigame
1. Derive from `Minigame` (see `TimerGame` in `TimerGame.cs`) and implement `Run`. Use `ScreenGrab` to watch part of the screen and `Native.MoveTo` / `MouseDown` / `MouseUp` to act.
2. Add its settings to `Settings.cs` (in the 1080p layout) and a page for them in `SettingsForm.cs`.
3. In `App.cs`, create an instance, give it a HUD button and a hotkey, and start it with `RunMinigame`.

# Changelog

All notable changes to Dirt Sweeper are documented here.
Versions follow `MAJOR.MINOR.PATCH`; every release bumps at least the patch number.

## [0.1.2] - 2026-11-05

_Updated by [eyyMinda](https://github.com/eyyMinda)._

### Changed

- **Calibration (F7):** scrolling now sets the collision radius level; Ctrl + scroll adjusts the size per level; Shift for steps of 10.
- **Defaults are a fresh game:** collision radius level 1 and timer levels 1–3, with 6.4 px per level and 15 ms per spot. Players further along just raise the level once.

### Fixed

- **Safer timer clicks:** the cursor is put back on the red button just before each click (and checked again right before pressing), so a nudged mouse can no longer make a click miss. The log notes when it had to.
- Small hover circles (low levels) no longer freeze the settings panel while the sweep path is worked out, and the guides just tint the field when there are thousands of spots. Long passes show in minutes.

## [0.1.1] - 2026-11-05

_Updated by [eyyMinda](https://github.com/eyyMinda)._

### Added

- **Updates in one click.** Dirt Sweeper checks GitHub for a new version at startup and once a day (optional). When there is one, the gear on the HUD gets a blue dot, the bar says so, and a notification pops up; **Update to x.y.z** in the new About tab downloads it, checks it against the release's `SHA256SUMS.txt`, swaps it in and restarts. **Check for updates** checks right away.
- **About tab**: version, updates, a quick way to report a bug or request a feature on GitHub, and links to GitHub, the website and Buy me a coffee.
- Bug report and feature request forms on GitHub issues.
- **Open settings folder** in the General tab.

### Changed

- **Settings are saved in `%LOCALAPPDATA%\DirtSweeper\settings.json`** instead of next to the exe, so they survive moving or re-downloading the exe. Your old settings file is moved there on the first start. To remove Dirt Sweeper completely, delete the exe and that folder.
- Settings are written safely (a crash can't leave a half-written file), and a file that can't be read is kept as `settings.broken.json` instead of being overwritten.
- New defaults: px per level 6.44 and hold 15 ms per spot. At collision radius 56 a pass now takes less than half a second.
- Dirt Sweeper now goes online only to check GitHub for updates; turn it off in About.

## [0.1.0] - 2026-11-05

_Updated by [eyyMinda](https://github.com/eyyMinda)._

### Added

- **Settings panel in the overlay.** The gear on the HUD unfolds every setting right over the game, in Sweeper, Timer game and General tabs. It never takes focus from the game, so changes show up live without alt-tabbing. Values change with − / + (hold to repeat) or by scrolling over a row; Shift for bigger steps. The separate settings window is gone.
- **Calibrate with the mouse wheel:** F7 shows the green ring at the cursor; scroll over the game until it matches the game's hover circle (Shift = fine, Ctrl = coarse).
- **Edit the dig field on screen:** drag its edges and corners directly over the game.
- Guides draw each spot's hover circle as a see-through disc, so any gap is easy to spot.
- Timer levels are on/off chips (1–6) instead of a text field.
- Licensed under the GNU General Public License v3.0.
- Project website with a direct download link: https://eyyminda.github.io/Dirt-Sweeper/.

### Changed

- **Sweep path covers the whole field with the fewest spots.** Hex and staggered layouts are tried, each is checked for full coverage, and the smallest wins. At collision radius 56 on 1920×1080 that's 27 spots, about 0.95 s per pass. The old path could leave small gaps at the edges, and its random shift used 5–15% more spots.
- "Vary the pattern every pass" now mirrors the path instead of shifting it randomly, so coverage never drops.
- **Hold time is in milliseconds** (default 35 ms) instead of game frames. Dirt Clicker is a Godot game whose hover check most likely runs on 60 Hz physics, so 2 frames at 144 FPS could fall between checks. The FPS setting is gone.
- Defaults are tuned for 1920×1080 borderless fullscreen at 144 FPS: collision radius 56, dig field 0, 0 → 1485, 1080.
- The overlays are anti-aliased with real transparency and soft shadows.

### Fixed

- The dig field stays on the monitor. Godot's fullscreen window is 2 px taller than the screen (1920×1082), and the sweeper used to aim at pixels the cursor can't reach.

## [0.0.1] - 2026-11-05

_Updated by [eyyMinda](https://github.com/eyyMinda)._

### Added

- First public version: HUD bar over Dirt Clicker, hex-grid sweeper with hotkeys (F5–F9), red-button timer minigame, guides overlay, settings window, launch-game button.

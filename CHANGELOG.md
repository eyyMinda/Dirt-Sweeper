# Changelog

All notable changes to Dirt Sweeper are documented here.
Versions follow `MAJOR.MINOR.PATCH`; every release bumps at least the patch number.

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

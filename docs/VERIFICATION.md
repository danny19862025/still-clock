## Version 2.5.0 validation

- Windows 11: Release builds of the Windows app and the Linux (Avalonia) app succeeded. All 40 core regression tests passed, including UI theme, version comparison, release parsing and checksum lookup.
- The update checker was run against the live v2.4.0 GitHub release. It found the installer and checksum file, and read the same installer hash that was verified by hand earlier.
- `Still.exe --verify` renders of Settings and About in dark and light mode were visually inspected.
- Not exercised: the full in-app download-and-install hand-off (it needs a release newer than the running copy), and the Linux About window and light mode at runtime (no Linux session or WSL here). The Linux CI job builds and tests the Linux app.

## Version 2.3.0 Linux validation

Build host: Arch Linux x64, KDE Plasma 6 on Wayland (Still runs through XWayland), one
1920×1200 display at 100%, .NET SDK 10.0.404, Avalonia 11.3.23.

- `src/Still.Linux` builds with 0 warnings and 0 errors. All 32 core regression tests passed.
- `scripts/publish-linux.sh` produced the self-contained single-file `Still-linux-x64.tar.gz`.
  The extracted executable ran with no .NET installed, and `--version` and `--verify` worked.
- `still --verify` renders of all twelve presets and Settings were compared side by side
  with the Windows renders in `docs/images`: layout, fonts, fades, gradients, frames,
  indicators and scanlines match. The offscreen renderer omits the glow by design.
- On the live desktop: transparent borderless widget at the saved position; neon glow drawn;
  tray icon registered with the StatusNotifierWatcher; widget flagged skip-taskbar,
  skip-pager and skip-switcher; locked click-through empties the X11 input region and
  unlock restores it; a second launch unlocks, restores input and opens Settings, then
  exits; SIGTERM saves settings and removes the instance socket.
- `install.sh` and `uninstall.sh` ran in a sandboxed home. The desktop entry passed
  `desktop-file-validate`, and uninstall removed every installed file.
- Fixed during porting: Avalonia drawing-context opacity masks mis-scale later drawing on
  HiDPI renders, so fades use a masked layer. Avalonia draws each text run in its own
  coordinate space, so text gradients are shifted per glyph.
- Not exercised: clicking through the tray menu and Settings controls by hand, drag and the
  scale grip, the login autostart entry at a real login, multiple monitors or HiDPI
  scaling, "on every virtual desktop" (this host has one desktop), GNOME and other
  desktops, a native Wayland backend (Avalonia 11 uses XWayland), and linux-arm64.

## Version 2.3.0 Windows validation

- Windows 11 Release build succeeded. All 32 core regression tests passed, including the new Overlay/Arc preset and ordinal-date tests.
- `Still.exe --verify` exported actual WPF renders of all twelve presets and Settings. The Midnight and Arc renders were visually inspected and are in `docs/images`.
- Not exercised: drag, the scale grip, and the tray menu with the new layouts, and the installer build.

## Version 2.2.0 Windows validation

- Windows 11 Release build succeeded. All 30 core regression tests passed, including the new Script layout test.
- `Still.exe --verify` exported actual WPF renders of all ten presets and Settings. The Signature render was visually inspected and is in `docs/images`.
- Not exercised: drag, the scale grip, and the tray menu with the Script layout, and the installer build.

## Version 2.1.0 Windows validation

- Windows 11 Release build succeeded. All 29 core regression tests passed, including the new layout and compact-time tests.
- `Still.exe --verify` exported actual WPF renders of all nine presets and Settings. The Poster and Neon Poster renders were visually inspected and are in `docs/images`.
- Not exercised: drag, the scale grip, and the tray menu with the Poster layout, and the installer build.

## Version 2.0.0 Windows validation

- Windows 11 Release build of Still.App succeeded with 0 warnings and 0 errors (.NET SDK 10.0.404).
- All 27 core regression tests passed, including new checks for effect normalization, settings from 1.x keeping their look, date styles, per-second scheduling for indicators, and theme copy/paste isolation.
- `Still.exe --verify` exported actual WPF renders of all seven presets and the settings window. They were visually inspected. Neon, HUD, Holo, Terminal and Settings renders are in `docs/images`.
- Not exercised: drag, scale grip, tray menu, click-through and multi-monitor behaviour with the new renderer, and the installer build.

## Version 1.1.0 Windows validation

- Windows Release build and self-contained x64 publish succeeded.
- All 20 core regression tests passed, including preset background and opacity checks.
- Actual WPF renders of Minimal, Type, Glass and Settings were generated and visually inspected.
- Inno Setup compiles the per-user x64 installer. Full install/uninstall registration should be exercised on an unrestricted Windows account before relying on managed deployment.

The following notes describe the original 1.0.0 validation baseline.

# Verification record

Build host: Linux x64 (Arch), 2026-11-12. SDK installed locally for this task:
.NET SDK 10.0.404. No Windows session, WPF runtime, Wine, or Windows VM is available.

## Automated verification

- Core tests execute natively on Linux using .NET 10.
- WPF source cross-compiles using Microsoft's Windows desktop targeting pack.
- Portable Windows x64 self-contained release is cross-published with .NET's
  native Windows app host, runtime and WPF components. This creates a Windows PE
  executable; compiling/publishing does **not** prove Windows runtime behaviour.

**20 core regression tests passed**, with no failing tests. WPF cross-build and
self-contained publish succeeded. Tests deliberately
separate pure placement/formatting logic from OS APIs. No synthetic screenshots
are presented as screenshots of a running Windows application.

## Windows acceptance checklist (not executed on this host)

1. On a Windows 11 account without admin privileges, extract release and run
   Still.exe offline. Verify first-run settings, tray icon, no taskbar/Alt+Tab
   entry for the widget, and default normal stacking. Reopen a second instance.
2. Inspect all presets on dark/light wallpapers and 100/125/150/200% scaling.
   Verify crisp rendering, digit stability through 09:59→10:00, 12-hour AM→PM,
   midnight/date changes, and readable small text. Check colour picking and fonts.
3. Change every setting, close/reopen settings, restart the app, verify saved
   appearance/scale/position/display. Tab through all controls, use Space/arrows,
   access keys, Ctrl+1/2/3, Escape, F2 and Ctrl+,.
4. Drag, resize, lock, enable click-through. Verify input reaches another app,
   then recover using the explicit tray restore action. Hide/show and Exit.
   Restart Explorer; verify the tray icon returns.
5. Move between monitors with different scaling, including a negative-origin
   monitor. Restart there, disconnect it, change resolution/scaling, reconnect.
   Verify fit/clamping and that settings remains usable. Try maximum size/scale.
6. Win+D / Show Desktop with topmost off/on. Restore using tray Show clock and
   explicit unlock action. There is no claimed wallpaper integration.
7. Suspend/resume over minute/day boundaries, change timezone and manually move
   the system clock forward/backward. Verify immediate resampling and cadence.
8. Test startup off/on at login, disable again, move portable folder and
   re-register. Test denied registry access. Windows policy may disable startup.
9. While stopped, damage JSON or insert null/unknown/oversized fields. Verify
   defaults and damaged backup. Deny settings-folder writes; confirm status and
   session-only behaviour. Remove that restriction after the test.
10. Test custom placeholders, locale names, malformed braces, scripts as literal
    text, overlong input, and three-line templates. Verify the last valid layout
    remains active while invalid input is displayed. There is no code execution.
11. Run Still.exe --capture in a writable folder. Inspect actual WPF renders of
    all presets and settings. Confirm screenshots appear in screenshots/.

## Known limits

Native tray, click-through, mixed-DPI window movement, Explorer restart, power
notifications, login launch and visual appearance still need the Windows checks
above. The Glass surface is translucent shading, not native acrylic/blur. Normal
top-level-window Show Desktop behaviour applies. Advanced arbitrary text can
change width as words change; numeric cells stay fixed-width. Monitor device names
are Windows display identifiers and can change if the display topology is rebuilt.
The release is unsigned. No installations or startup registration were performed on this Linux host.

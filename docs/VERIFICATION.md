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
The release is unsigned. No automated commits, pushes, installations or startup
registration were performed on this Linux host.

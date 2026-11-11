# Reference review, completed before implementation

Repository: https://github.com/MarcinOrlowski/html-clock-plasmoid
Revision: `e725ef960231a00a99f6942d9fd9bd694a8c0781` (master, inspected 2026-11-12).

Inspected README.md, docs/configuration.md, docs/placeholders.md,
src/contents/config/main.xml, src/metadata.json,
src/contents/js/layouts.js, DateTimeFormatter.js, placeholders.js,
src/contents/ui/main.qml, HtmlClock.qml, and config/Layout.qml.

## Design understood

- The Plasma widget delegates appearance to Qt rich text. Built-in templates
  combine spans and tables; examples emphasize a pink hour, differently sized
  minutes, a muted/blinking colon, optional elevated seconds, and date grids.
- main.xml groups appearance, user layouts, locale, tooltips, clock timezone and
  calendar settings. It stores three user-layout slots, custom fonts, container
  fill/width constraints, timezone offsets and cycle/random intervals.
- Layout configuration has a live preview, template cloning, font/color helpers
  and selection styling. General configuration separates built-in versus user
  layouts and chooses the active slot.
- DateTimeFormatter.js substitutes date/time tokens using locale and optional
  offsets/modifiers. placeholders.js separately handles brace-aware cycle,
  random and legacy flip expansion; those can change markup as well as text.
- HtmlClock.qml renders rich text, measures a stable width to avoid neighbour
  movement and supports configurable container filling. Clock refresh samples
  time rather than counting seconds; main.qml uses a self-correcting heartbeat
  for tooltip formatting. The widget integrates with Plasma calendars, panels
  and optional application launch actions.
- The README, metadata and source headers identify MIT licensing, copyright
  2020–2026 Marcin Orlowski. No standalone LICENSE file was present in the
  inspected tree. Attribution is recorded in THIRD-PARTY-NOTICES.md.

## Translation to Still

Retained ideas: immediate visual feedback, flexible but documented substitution,
useful finished starting layouts, installed-font/color customization, and stable
digit sizing. Original identity: warm white, sage, quiet typography and generous
spacing, with transparent and shaded rounded-card surfaces. No pink/blinking
colon, copied markup, artwork or Qt dependencies.

Replaced Plasma-specific integration with a normal WPF tool window and a WinForms
notification-area menu. Settings is a separate keyboard-accessible window with
a shared ClockFace preview renderer. Display placement uses physical work-area
origins and DIP offsets; Win32 handles layered click-through. Default stacking
is normal, with optional topmost. No wallpaper embedding is claimed.

Reduced scope deliberately: a single local clock. No calendar dashboard, multiple
timezones, animation, shell commands, randomization, rich text or remote resources.
Advanced templates use a new, small, case-sensitive vocabulary, validated before
applying. This is an independently implemented product, not a Windows port or a
compatible interpreter of upstream templates.

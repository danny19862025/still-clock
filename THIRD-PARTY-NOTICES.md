# Inspiration and attribution

Still is an independent desktop application inspired by the configurable layouts
and live previews of **HTML Clock for Plasma**, by Marcin Orlowski:
https://github.com/MarcinOrlowski/html-clock-plasmoid

Inspected revision: `e725ef960231a00a99f6942d9fd9bd694a8c0781`.
Upstream README, metadata and source headers identify the licence as MIT and
copyright ©2020–2026 Marcin Orlowski <mail (#) marcinorlowski (.) com>.

No upstream source code, templates, graphics, fonts or icons are included in
Still. Its C# formatter, layouts, UI, and icon are original implementations.
The Linux edition bundles the open-licence fonts listed under *Linux release*.
Placeholder concepts and the product's flexibility are the inspiration; Still
does not implement upstream's markup syntax or claim compatibility with it.
The following standard MIT notice is retained here for clear attribution:

Copyright (c) 2020–2026 Marcin Orlowski

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

# Runtime

Self-contained releases bundle Microsoft's .NET Windows Desktop runtime.
.NET is distributed under MIT and other component licences. Runtime licence
and third-party notices supplied by the publish process are retained in the
release directory. WPF and WinForms are Microsoft .NET components. Windows
system fonts are used from the operating system; no fonts are redistributed.

# Linux release

The Linux executable is self-contained. It bundles:

- **.NET runtime** (Microsoft, MIT and component licences). Licence files are in
  `licences/runtime/` in the release folder.
- **Avalonia** UI framework 11.3, including Avalonia.Controls.ColorPicker and
  Avalonia.Fonts.Inter (AvaloniaUI OÜ, MIT): https://github.com/AvaloniaUI/Avalonia
- **SkiaSharp** and **HarfBuzzSharp** (Microsoft/.NET Foundation, MIT, with native Skia
  and HarfBuzz under their own licences). Licence files are in `licences/`.
- **Tmds.DBus.Protocol** (Tom Deseyn, MIT), used by Avalonia for the tray icon, and
  **MicroCom.Runtime** (AvaloniaUI, MIT).

Fonts, each under the SIL Open Font License 1.1 (https://openfontlicense.org). The
licence texts are in `licences/fonts/` and in `src/Still.Linux/Assets/Fonts/`:

- **Inter**, Copyright 2016 The Inter Project Authors (https://github.com/rsms/inter),
  supplied by the Avalonia.Fonts.Inter package.
- **Barlow**, Copyright 2017 The Barlow Project Authors (https://github.com/jpt/barlow).
- **Kaushan Script**, Copyright (c) 2011 Pablo Impallari (www.impallari.com).
- **Share Tech Mono**, Copyright (c) 2012 Carrois Type Design, Ralph du Carrois.

The fonts are unmodified and are not sold on their own.

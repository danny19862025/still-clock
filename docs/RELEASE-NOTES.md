Still 2.5.0 adds an About window, update checks, Buy Me a Coffee, and light and dark settings.

- **About Still:** open it with the new **About** button at the top of Settings, press F1, or use the tray menu. It shows the version, who made it, and links to the GitHub repository, the issue tracker and Buy Me a Coffee.
- **Buy Me a Coffee:** a coffee-cup button sits at the top of Settings, and there's a yellow Buy Me a Coffee button in About.
- **Check for updates:** asks GitHub for the latest stable release, only when you press the button. On Windows installs made with Setup, **Download and install** fetches the new installer, verifies its SHA-256 against the published checksums, updates Still in place and reopens it. Portable copies and Linux open the release page instead.
- **Light and dark mode:** Settings > Theme > Settings appearance switches Settings and About between Dark, Light and Match Windows/system. The live preview and preset cards stay dark so clocks look as they will on your wallpaper. The choice stays on your computer and is not part of copied themes.

Existing preferences are kept, and Settings stays dark until you choose otherwise.

Windows: download Still-Setup-win-x64.exe, exit the old Still from its notification-area icon, and run Setup. It installs for your Windows user without administrator access and includes .NET. A portable Still-win-x64.zip is also available. From this version on, you can update from inside Still.

Linux: download Still-linux-x64.tar.gz, then run: tar -xzf Still-linux-x64.tar.gz && cd Still-linux-x64 && ./install.sh

The downloads are unsigned. SHA256SUMS.txt contains the checksums of all three.

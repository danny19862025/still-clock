#!/bin/sh
# Installs Still for the current user. No root access is needed.
#   ./install.sh            install or upgrade, then start Still
#   ./install.sh --no-start install or upgrade without starting it
set -eu

here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
data=${XDG_DATA_HOME:-$HOME/.local/share}
app="$HOME/.local/opt/still"
bin="$HOME/.local/bin"
start=1
[ "${1:-}" = "--no-start" ] && start=0

if [ ! -f "$here/still" ]; then
    echo "Run install.sh from the extracted Still folder." >&2
    exit 1
fi

# Close a running copy so it saves its settings; it is started again below.
was_running=0
if pgrep -u "$(id -u)" -x still >/dev/null 2>&1; then
    was_running=1
    pkill -TERM -u "$(id -u)" -x still || true
    for _ in 1 2 3 4 5 6 7 8 9 10; do pgrep -u "$(id -u)" -x still >/dev/null 2>&1 || break; sleep 0.3; done
fi

mkdir -p "$app" "$bin" "$data/applications" "$data/icons/hicolor/256x256/apps"
# Copy to a temporary name, then rename: replacing the executable is atomic.
cp "$here/still" "$app/still.new" && chmod 755 "$app/still.new" && mv -f "$app/still.new" "$app/still"
for file in README.md LICENSE THIRD-PARTY-NOTICES.md uninstall.sh still.png; do
    [ -f "$here/$file" ] && cp "$here/$file" "$app/"
done
chmod 755 "$app/uninstall.sh"
rm -rf "$app/licences" && cp -R "$here/licences" "$app/licences"
cp "$here/still.png" "$data/icons/hicolor/256x256/apps/still.png"
ln -sf "$app/still" "$bin/still"
sed "s|@EXEC@|$app/still|" "$here/still.desktop" > "$data/applications/still.desktop"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q "$data/applications" || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q -t "$data/icons/hicolor" || true

echo "Still is installed in $app"
echo "Open it from your application menu, or run: still"
case ":$PATH:" in *":$bin:"*) ;; *) echo "(Add $bin to your PATH to run 'still' from a terminal.)";; esac

if [ "$start" = 1 ] || [ "$was_running" = 1 ]; then
    if command -v setsid >/dev/null 2>&1; then setsid -f "$app/still" >/dev/null 2>&1 < /dev/null
    else nohup "$app/still" >/dev/null 2>&1 < /dev/null & fi
fi

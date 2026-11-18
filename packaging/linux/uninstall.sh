#!/bin/sh
# Removes Still for the current user.
#   ./uninstall.sh          remove the app, keep your settings in ~/.config/still
#   ./uninstall.sh --purge  also remove your settings
set -eu

data=${XDG_DATA_HOME:-$HOME/.local/share}
config=${XDG_CONFIG_HOME:-$HOME/.config}
app="$HOME/.local/opt/still"

pkill -TERM -u "$(id -u)" -x still 2>/dev/null || true
for _ in 1 2 3 4 5 6 7 8 9 10; do pgrep -u "$(id -u)" -x still >/dev/null 2>&1 || break; sleep 0.3; done

# Only remove the login entry when it starts this installed copy.
if [ -f "$config/autostart/still.desktop" ] && grep -q "$app/still" "$config/autostart/still.desktop"; then
    rm -f "$config/autostart/still.desktop"
fi
[ "$(readlink "$HOME/.local/bin/still" 2>/dev/null || true)" = "$app/still" ] && rm -f "$HOME/.local/bin/still"
rm -f "$data/applications/still.desktop" "$data/icons/hicolor/256x256/apps/still.png"
rm -rf "$app"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q "$data/applications" || true

if [ "${1:-}" = "--purge" ]; then
    rm -rf "$config/still"
    echo "Still and its settings have been removed."
else
    echo "Still has been removed. Your settings remain in $config/still (use --purge to delete them)."
fi

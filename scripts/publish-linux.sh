#!/usr/bin/env bash
# Tests, publishes and packages Still for Linux: artifacts/Still-linux-<arch>.tar.gz
#   scripts/publish-linux.sh [linux-x64|linux-arm64]
set -euo pipefail

runtime=${1:-linux-x64}
case "$runtime" in linux-x64|linux-arm64) ;; *) echo "Runtime must be linux-x64 or linux-arm64." >&2; exit 2;; esac
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
dotnet=${DOTNET:-dotnet}
[ -x "$root/.dotnet/dotnet" ] && [ -z "${DOTNET:-}" ] && dotnet="$root/.dotnet/dotnet"

"$dotnet" build tests/Still.Tests -c Release --disable-build-servers -m:1
"$dotnet" tests/Still.Tests/bin/Release/net10.0/Still.Tests.dll

name="Still-$runtime"
out="$root/artifacts/$name"
rm -rf "$out" "$root/artifacts/$name.tar.gz"
"$dotnet" publish src/Still.Linux -c Release -r "$runtime" --self-contained true -o "$out/.publish" \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
    -p:DebugType=None -p:DebugSymbols=false --disable-build-servers -m:1
mv "$out/.publish/still" "$out/still"
rm -rf "$out/.publish"

cp README.md LICENSE THIRD-PARTY-NOTICES.md "$out/"
cp packaging/linux/install.sh packaging/linux/uninstall.sh packaging/linux/still.desktop "$out/"
cp src/Still.Linux/Assets/still.png "$out/still.png"
chmod 755 "$out/still" "$out/install.sh" "$out/uninstall.sh"

# Licences for everything bundled inside the executable.
licences="$out/licences"
mkdir -p "$licences/fonts" "$licences/runtime"
cp src/Still.Linux/Assets/Fonts/OFL-*.txt "$licences/fonts/"
packages=$(sed -n 's/.*"packagesPath": "\(.*\)",/\1/p' src/Still.Linux/obj/project.assets.json | head -1)
for pack in "microsoft.netcore.app.runtime.$runtime" "microsoft.netcore.app.host.$runtime"; do
    dir=$(ls -d "$packages/$pack"/*/ 2>/dev/null | sort -V | tail -1 || true)
    [ -n "$dir" ] || continue
    mkdir -p "$licences/runtime/$pack"
    find "$dir" -maxdepth 1 -type f \( -iname 'LICENSE*' -o -iname 'THIRD-PARTY-NOTICES*' \) -exec cp {} "$licences/runtime/$pack/" \;
done
for pack in avalonia skiasharp harfbuzzsharp; do
    dir=$(ls -d "$packages/$pack"/*/ 2>/dev/null | sort -V | tail -1 || true)
    [ -n "$dir" ] || continue
    for file in "$dir"LICENSE* "$dir"license* "$dir"THIRD-PARTY-NOTICES*; do [ -f "$file" ] && { mkdir -p "$licences/$pack"; cp "$file" "$licences/$pack/"; }; done
done

tar -C "$root/artifacts" -czf "$root/artifacts/$name.tar.gz" "$name"
sha256sum "$root/artifacts/$name.tar.gz"

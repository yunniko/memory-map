#!/usr/bin/env bash
# Builds the Linux app (D035), compiled ahead of time (D043), for this machine's architecture, amd64 or
# arm64, into publish/:
#   linux-<x64|arm64>/                   MemoryMap and its two native libraries, to copy anywhere and run;
#   memory-map_<version>_<arch>.deb      the same three files as a package.
# Each architecture is built on its own kind of machine, so dpkg-shlibdeps reads that architecture's
# libraries (D041). Runs on Linux with the .NET SDK, clang (the AOT compiler's linker), python3,
# dpkg-deb and dpkg-shlibdeps (dpkg-dev). It refuses a build needing a glibc newer than Ubuntu 22.04's;
# scripts/publish-linux-container.sh runs it on Ubuntu 22.04, as releases do (D045).
# Check the package with scripts/deb-check.sh.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
out=$root/publish
app=$root/src/MemoryMap.App
assets=$root/assets/linux
version=$(dotnet msbuild "$app" -getProperty:Version -nologo)
changelog_version=$(sed -n '1s/^memory-map (\([^)]*\)).*/\1/p' "$assets/changelog")
if [[ $version != "$changelog_version" ]]; then
    echo "Version $version (Directory.Build.props) has no entry in assets/linux/changelog ($changelog_version)." >&2
    exit 1
fi
arch=$(dpkg --print-architecture)
case $arch in
    amd64) rid=linux-x64 ;;
    arm64) rid=linux-arm64 ;;
    *) echo "No build for architecture $arch: only amd64 and arm64 are built (D041)." >&2; exit 1 ;;
esac

command -v clang > /dev/null || { echo "clang is missing: apt install clang" >&2; exit 1; }

build=$out/$rid
rm -rf "$build"
dotnet publish "$app" -c Release -r "$rid" -p:DebugType=none -nologo -v q -o "$build"
libs=("$build/libSkiaSharp.so" "$build/libHarfBuzzSharp.so")
files=$(ls "$build" | LC_ALL=C sort | tr '\n' ' ')
if [[ $files != "MemoryMap libHarfBuzzSharp.so libSkiaSharp.so " ]]; then
    echo "Unexpected files in $build: $files" >&2
    exit 1
fi
# Upstream ships the libraries with symbols; the AOT compiler has already stripped the program.
strip --strip-unneeded --remove-section=.comment "${libs[@]}"
strip --remove-section=.comment "$build/MemoryMap"
chmod 755 "$build/MemoryMap" && chmod 644 "${libs[@]}"
# The newest glibc symbol version a shipped file needs. Above the floor, the package will not install
# on Ubuntu 22.04, Debian 12 or ChromeOS's Linux (D045).
glibc_floor=2.35
glibc=$(objdump -T "$build/MemoryMap" "${libs[@]}" | grep -o 'GLIBC_[0-9][0-9.]*' | cut -d_ -f2 | sort -uV | tail -1)
if [[ $(printf '%s\n' "$glibc" "$glibc_floor" | sort -V | tail -1) != "$glibc_floor" ]]; then
    echo "The build needs glibc $glibc, newer than $glibc_floor: build with scripts/publish-linux-container.sh (D045)." >&2
    exit 1
fi
echo "Built $build ($(du -sh "$build" | cut -f1), glibc $glibc): $files"

stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
pkg=$stage/pkg
lib=$pkg/usr/lib/memory-map
doc=$pkg/usr/share/doc/memory-map
umask 022
install -d "$lib" "$pkg/usr/bin" "$pkg/usr/share/applications" "$pkg/usr/share/icons/hicolor/scalable/apps" \
    "$pkg/usr/share/man/man1" "$doc" "$pkg/usr/share/lintian/overrides" "$pkg/DEBIAN"
install -m 755 "$build/MemoryMap" "$lib/"
install -m 644 "${libs[@]}" "$lib/"
ln -s ../lib/memory-map/MemoryMap "$pkg/usr/bin/memory-map"
install -m 644 "$assets/memory-map.desktop" "$pkg/usr/share/applications/"
install -m 644 "$root/assets/memory-map.svg" "$pkg/usr/share/icons/hicolor/scalable/apps/"
sed "s/@VERSION@/$version/" "$assets/memory-map.1" | gzip -9n > "$pkg/usr/share/man/man1/memory-map.1.gz"
gzip -9n < "$assets/changelog" > "$doc/changelog.gz"
install -m 644 "$assets/copyright" "$doc/"
install -m 644 "$assets/lintian-overrides" "$pkg/usr/share/lintian/overrides/memory-map"

# The bundled components' own notices, from the exact package versions this build restored.
packages=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')
read -r runtime skia < <(python3 - "$app/obj/project.assets.json" "$rid" <<'EOF'
import json, sys
assets, rid = json.load(open(sys.argv[1])), sys.argv[2]
runtime = next(d['version'].strip('[]').split(',')[0].strip()
               for d in assets['project']['frameworks']['net10.0']['downloadDependencies']
               if d['name'] == f'Microsoft.NETCore.App.Runtime.NativeAOT.{rid}')
skia = next(k.split('/')[1] for k in assets['targets'][f'net10.0/{rid}'] if k.startswith('SkiaSharp.NativeAssets.Linux/'))
print(runtime, skia)
EOF
)
gzip -9n < "$packages/microsoft.netcore.app.runtime.nativeaot.$rid/$runtime/THIRD-PARTY-NOTICES.TXT" > "$doc/dotnet-runtime-notices.txt.gz"
gzip -9n < "$packages/skiasharp.nativeassets.linux/$skia/THIRD-PARTY-NOTICES.txt" > "$doc/skia-harfbuzz-notices.txt.gz"

# Libraries the files link against, as dpkg computes them, and those the app opens at run time
# (measured with LD_DEBUG under WSLg: docs/reviews/2026-10-12-g003-m4-deb.md; deb-check.sh re-checks it, D036).
mkdir -p "$stage/debian"
printf 'Source: memory-map\n\nPackage: memory-map\nArchitecture: %s\n' "$arch" > "$stage/debian/control"
linked=$(cd "$stage" && dpkg-shlibdeps -O "$lib/MemoryMap" "$lib/libSkiaSharp.so" "$lib/libHarfBuzzSharp.so" \
    | sed -n 's/^shlibs:Depends=//p')
[[ $linked == *libc6* ]] || { echo "dpkg-shlibdeps found no libc6 dependency: '$linked'" >&2; exit 1; }
opened="libx11-6, libxext6, libxfixes3, libxi6, libxrandr2, libice6, libsm6, libdbus-1-3, libicu76 | libicu74 | libicu72 | libicu70"
cat > "$pkg/DEBIAN/control" <<EOF
Package: memory-map
Version: $version
Architecture: $arch
Maintainer: yunniko <29886186+yunniko@users.noreply.github.com>
Installed-Size: $(du -sk --exclude=DEBIAN "$pkg" | cut -f1)
Depends: $linked, $opened
Recommends: dbus-bin | dbus, xdg-utils
Section: utils
Priority: optional
Description: live treemap of what is using the computer's memory
 Memory Map shows RAM as a treemap refreshed every second: apps in the
 user's session, system services, virtual machines and containers, kernel
 memory, shared memory and cache, and available memory. Each process is
 sized by its private memory, which needs no root.
 .
 It also has a table, an hour of history, a low-memory alert, a tray icon
 where the desktop shows one, and a list of the programs that start at
 login with a switch for this user only.
EOF
(cd "$pkg" && find usr -type f -print0 | sort -z | xargs -0 md5sum) > "$pkg/DEBIAN/md5sums"
chmod 644 "$pkg/DEBIAN/control" "$pkg/DEBIAN/md5sums"
deb=$out/memory-map_${version}_$arch.deb
rm -f "$out"/memory-map_*.deb # one package in publish/, so `publish/memory-map_*.deb` names it
dpkg-deb --root-owner-group -Zxz --build "$pkg" "$deb" > /dev/null
echo "Built $deb"
dpkg-deb --field "$deb" Depends

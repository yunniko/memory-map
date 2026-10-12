#!/usr/bin/env bash
# Builds the Linux app (D035) into publish/:
#   linux-x64/MemoryMap               one self-contained file to copy anywhere and run;
#   memory-map_<version>_amd64.deb    the same build as a package, its two native libraries beside it.
# Runs on Linux with the .NET SDK, python3, dpkg-deb and dpkg-shlibdeps (dpkg-dev).
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

publish() {
    dotnet publish "$app" -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:DebugType=none \
        -nologo -v q "$@"
}

# The single file: Skia and HarfBuzz go inside and are unpacked under ~/.net on first run (as on Windows, D014).
publish -p:IncludeNativeLibrariesForSelfExtract=true -o "$out/linux-x64"
echo "Built $out/linux-x64/MemoryMap"

# The package: the libraries stay beside the program, so it unpacks nothing into users' homes.
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
publish -o "$stage/build"
pkg=$stage/pkg
lib=$pkg/usr/lib/memory-map
doc=$pkg/usr/share/doc/memory-map
umask 022
install -d "$lib" "$pkg/usr/bin" "$pkg/usr/share/applications" "$pkg/usr/share/icons/hicolor/scalable/apps" \
    "$pkg/usr/share/man/man1" "$doc" "$pkg/usr/share/lintian/overrides" "$pkg/DEBIAN"
install -m 755 "$stage/build/MemoryMap" "$lib/"
install -m 644 "$stage/build/libSkiaSharp.so" "$stage/build/libHarfBuzzSharp.so" "$lib/"
# Upstream ships them with symbols; the program itself is a bundle and must not be stripped.
strip --strip-unneeded --remove-section=.comment "$lib/libSkiaSharp.so" "$lib/libHarfBuzzSharp.so"
ln -s ../lib/memory-map/MemoryMap "$pkg/usr/bin/memory-map"
install -m 644 "$assets/memory-map.desktop" "$pkg/usr/share/applications/"
install -m 644 "$assets/memory-map.svg" "$pkg/usr/share/icons/hicolor/scalable/apps/"
sed "s/@VERSION@/$version/" "$assets/memory-map.1" | gzip -9n > "$pkg/usr/share/man/man1/memory-map.1.gz"
gzip -9n < "$assets/changelog" > "$doc/changelog.gz"
install -m 644 "$assets/copyright" "$doc/"
install -m 644 "$assets/lintian-overrides" "$pkg/usr/share/lintian/overrides/memory-map"

# The bundled components' own notices, from the exact package versions this build restored.
packages=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')
read -r runtime skia < <(python3 - "$app/obj/project.assets.json" <<'EOF'
import json, sys
assets = json.load(open(sys.argv[1]))
runtime = next(d['version'].strip('[]').split(',')[0].strip()
               for d in assets['project']['frameworks']['net10.0']['downloadDependencies']
               if d['name'] == 'Microsoft.NETCore.App.Runtime.linux-x64')
skia = next(k.split('/')[1] for k in assets['targets']['net10.0/linux-x64'] if k.startswith('SkiaSharp.NativeAssets.Linux/'))
print(runtime, skia)
EOF
)
gzip -9n < "$packages/microsoft.netcore.app.runtime.linux-x64/$runtime/THIRD-PARTY-NOTICES.TXT" > "$doc/dotnet-runtime-notices.txt.gz"
gzip -9n < "$packages/skiasharp.nativeassets.linux/$skia/THIRD-PARTY-NOTICES.txt" > "$doc/skia-harfbuzz-notices.txt.gz"

# Libraries the files link against, as dpkg computes them, and those the app opens at run time
# (measured with LD_DEBUG under WSLg: docs/reviews/2026-10-12-g003-m4-deb.md; deb-check.sh re-checks it, D036).
mkdir -p "$stage/debian"
printf 'Source: memory-map\n\nPackage: memory-map\nArchitecture: amd64\n' > "$stage/debian/control"
linked=$(cd "$stage" && dpkg-shlibdeps -O "$lib/MemoryMap" "$lib/libSkiaSharp.so" "$lib/libHarfBuzzSharp.so" \
    | sed -n 's/^shlibs:Depends=//p')
[[ $linked == *libc6* ]] || { echo "dpkg-shlibdeps found no libc6 dependency: '$linked'" >&2; exit 1; }
opened="libx11-6, libxext6, libxfixes3, libxi6, libxrandr2, libice6, libsm6, libdbus-1-3, libicu76 | libicu74 | libicu72 | libicu70"
cat > "$pkg/DEBIAN/control" <<EOF
Package: memory-map
Version: $version
Architecture: amd64
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
deb=$out/memory-map_${version}_amd64.deb
rm -f "$out"/memory-map_*_amd64.deb # one package in publish/, so `publish/memory-map_*_amd64.deb` names it
dpkg-deb --root-owner-group -Zxz --build "$pkg" "$deb" > /dev/null
echo "Built $deb"
dpkg-deb --field "$deb" Depends

#!/usr/bin/env bash
# Turns the release workflow's downloaded artifacts into the files a release carries, with versioned
# names, SHA256SUMS and notes.md (the newest assets/linux/changelog entry, downloads, checksums).
# Usage: VERSION=0.5.1 SIGNED=true|false scripts/release-files.sh <artifacts-dir> <out-dir>
set -euo pipefail
in=$1 out=$2
: "${VERSION:?VERSION is not set}" "${SIGNED:?SIGNED is not set}"
root=$(cd "$(dirname "$0")/.." && pwd)
files=$out/files
rm -rf "$out" && mkdir -p "$files" && files=$(cd "$files" && pwd)

# Each build is a program and its two native libraries (D043).
windows_libs=(libSkiaSharp.dll libHarfBuzzSharp.dll)
linux_libs=(libSkiaSharp.so libHarfBuzzSharp.so)
# Only the exe is signed by SignPath; the libraries keep their upstream signatures and always come
# from the unsigned build.
if [[ $SIGNED == true ]]; then exe=$in/windows-signed/MemoryMap.exe; else exe=$in/windows-unsigned/MemoryMap.exe; fi
# Every archived file gets the commit's time, so two runs for one tag give the same checksums.
stamp=$(git -C "$root" log -1 --format=%ct)
# Each Linux architecture's artifact (linux-<deb arch>) holds <rid>/ and its .deb (D041).
linux=("amd64 linux-x64" "arm64 linux-arm64")
needed=("$exe" "${windows_libs[@]/#/$in/windows-unsigned/}" "$in/notices/LICENSE")
for pair in "${linux[@]}"; do
  read -r arch rid <<< "$pair"
  needed+=("$in/linux-$arch/$rid/MemoryMap" "${linux_libs[@]/#/$in/linux-$arch/$rid/}"
    "$in/linux-$arch/memory-map_${VERSION}_$arch.deb")
done
for f in "${needed[@]}"; do
  [[ -f $f ]] || { echo "missing: $f" >&2; exit 1; }
done

# Windows: the three files at the zip's top, so "Extract all" makes one folder holding them.
mkdir -p "$out/zip"
cp "$exe" "${windows_libs[@]/#/$in/windows-unsigned/}" "$out/zip/"
touch -d "@$stamp" "$out/zip/"*
(cd "$out/zip" && zip -q -X "$files/MemoryMap-$VERSION-win-x64.zip" MemoryMap.exe "${windows_libs[@]}")
# Linux: one folder in the archive. Artifacts lose file modes, so they are set here.
for pair in "${linux[@]}"; do
  read -r arch rid <<< "$pair"
  dir=MemoryMap-$VERSION-$rid
  mkdir -p "$out/tar/$dir"
  install -m 755 "$in/linux-$arch/$rid/MemoryMap" "$out/tar/$dir/"
  install -m 644 "${linux_libs[@]/#/$in/linux-$arch/$rid/}" "$out/tar/$dir/"
  tar -C "$out/tar" --sort=name --mtime="@$stamp" --owner=0 --group=0 --numeric-owner -cf - "$dir" \
    | gzip -9n > "$files/$dir.tar.gz"
  cp "$in/linux-$arch/memory-map_${VERSION}_$arch.deb" "$files/"
done
touch -d "@$stamp" "$in/notices/"*
(cd "$in/notices" && zip -q -X "$files/MemoryMap-$VERSION-notices.zip" ./*)
(cd "$files" && sha256sum -- * > SHA256SUMS)

# The newest changelog entry's bullets, as Markdown.
changes=$(awk 'NR == 1 { next } /^ -- / { exit } /^  \* / { sub(/^  \* /, "- "); print; next } /^    / { sub(/^    /, "  "); print }' \
  "$root/assets/linux/changelog")
if [[ $SIGNED == true ]]; then
  signing="The Windows program is signed by SignPath Foundation."
else
  signing="The Windows program is not signed, so SmartScreen may warn the first time it runs."
fi
repo=https://github.com/${GITHUB_REPOSITORY:-yunniko/memory-map}

cat > "$out/notes.md" <<EOF
## Memory Map $VERSION

$changes

### Downloads
| File | For |
|---|---|
| \`MemoryMap-$VERSION-win-x64.zip\` | Windows 10/11, 64-bit: extract it and run \`MemoryMap.exe\` (keep the three files together). |
| \`memory-map_${VERSION}_amd64.deb\` | Debian, Ubuntu and similar on Intel or AMD: \`sudo apt install ./memory-map_${VERSION}_amd64.deb\` |
| \`memory-map_${VERSION}_arm64.deb\` | The same on 64-bit ARM (such as an ARM Chromebook's Linux): \`sudo apt install ./memory-map_${VERSION}_arm64.deb\` |
| \`MemoryMap-$VERSION-linux-x64.tar.gz\` | Other Linux on Intel or AMD: extract it and run \`MemoryMap\` in its folder. |
| \`MemoryMap-$VERSION-linux-arm64.tar.gz\` | Other Linux on 64-bit ARM: extract it and run \`MemoryMap\` in its folder. |
| \`MemoryMap-$VERSION-notices.zip\` | The licence and the bundled components' notices. |

$signing [Code signing policy]($repo#code-signing-policy)

SHA-256:
\`\`\`
$(cat "$files/SHA256SUMS")
\`\`\`
EOF
ls -l "$files"

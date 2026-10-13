#!/usr/bin/env bash
# Turns the release workflow's downloaded artifacts into the files a release carries, with versioned
# names, SHA256SUMS and notes.md (the newest assets/linux/changelog entry, downloads, checksums).
# Usage: VERSION=0.4.1 SIGNED=true|false scripts/release-files.sh <artifacts-dir> <out-dir>
set -euo pipefail
in=$1 out=$2
: "${VERSION:?VERSION is not set}" "${SIGNED:?SIGNED is not set}"
root=$(cd "$(dirname "$0")/.." && pwd)
files=$out/files
rm -rf "$out" && mkdir -p "$files" && files=$(cd "$files" && pwd)

if [[ $SIGNED == true ]]; then exe=$in/windows-signed/MemoryMap.exe; else exe=$in/windows-unsigned/MemoryMap.exe; fi
deb=$in/linux/memory-map_${VERSION}_amd64.deb
for f in "$exe" "$in/linux/linux-x64/MemoryMap" "$deb" "$in/notices/LICENSE"; do
  [[ -f $f ]] || { echo "missing: $f" >&2; exit 1; }
done

cp "$exe" "$files/MemoryMap-$VERSION-win-x64.exe"
cp "$in/linux/linux-x64/MemoryMap" "$files/MemoryMap-$VERSION-linux-x64"
cp "$deb" "$files/"
(cd "$in/notices" && zip -q -X "$files/MemoryMap-$VERSION-notices.zip" ./*)
(cd "$files" && sha256sum -- * > SHA256SUMS)

# The newest changelog entry's bullets, as Markdown.
changes=$(awk 'NR == 1 { next } /^ -- / { exit } /^  \* / { sub(/^  \* /, "- "); print; next } /^    / { sub(/^    /, "  "); print }' \
  "$root/assets/linux/changelog")
if [[ $SIGNED == true ]]; then
  signing="The Windows exe is signed by SignPath Foundation."
else
  signing="The Windows exe is not signed, so SmartScreen may warn the first time it runs."
fi
repo=https://github.com/${GITHUB_REPOSITORY:-yunniko/memory-map}

cat > "$out/notes.md" <<EOF
## Memory Map $VERSION

$changes

### Downloads
| File | For |
|---|---|
| \`MemoryMap-$VERSION-win-x64.exe\` | Windows 10/11, 64-bit: one file, run it. |
| \`memory-map_${VERSION}_amd64.deb\` | Debian, Ubuntu and similar: \`sudo apt install ./memory-map_${VERSION}_amd64.deb\` |
| \`MemoryMap-$VERSION-linux-x64\` | Other 64-bit Linux: \`chmod +x\` it and run it. |
| \`MemoryMap-$VERSION-notices.zip\` | The licence and the bundled components' notices. |

$signing [Code signing policy]($repo#code-signing-policy)

SHA-256:
\`\`\`
$(cat "$files/SHA256SUMS")
\`\`\`
EOF
ls -l "$files"

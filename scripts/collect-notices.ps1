# Gathers the licence and the bundled components' notice texts for a release, from the exact package
# versions the Windows build restored (run after scripts/publish.ps1). Writes publish/notices/.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$assets = Get-Content (Join-Path $root 'src/MemoryMap.App/obj/project.assets.json') -Raw | ConvertFrom-Json
$packages = $assets.project.restore.packagesPath
$out = Join-Path $root 'publish/notices'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $out | Out-Null

function Version-Of([string]$name) {
    $key = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like "$name/*" } | Select-Object -First 1
    if (-not $key) { throw "$name is not in the restored packages." }
    $key.Split('/')[1]
}

$runtime = $assets.project.frameworks.'net10.0'.downloadDependencies |
    Where-Object { $_.name -eq 'Microsoft.NETCore.App.Runtime.NativeAOT.win-x64' } | Select-Object -First 1
if (-not $runtime) { throw 'The win-x64 Native AOT runtime pack is not in project.assets.json; publish for win-x64 first.' }
$runtimeVersion = $runtime.version.Trim('[', ']').Split(',')[0].Trim()

$copies = [ordered]@{
    'dotnet-runtime-notices.txt' = "microsoft.netcore.app.runtime.nativeaot.win-x64/$runtimeVersion/THIRD-PARTY-NOTICES.TXT"
    # SkiaSharp's and HarfBuzzSharp's packages carry the same file, covering both.
    'skia-harfbuzz-notices.txt'  = "skiasharp.nativeassets.win32/$(Version-Of 'SkiaSharp.NativeAssets.Win32')/THIRD-PARTY-NOTICES.txt"
}
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md'),
    (Join-Path $root 'assets/licenses/material-symbols-LICENSE.txt') $out
foreach ($entry in $copies.GetEnumerator()) {
    Copy-Item (Join-Path $packages $entry.Value) (Join-Path $out $entry.Key)
}
Get-ChildItem $out | ForEach-Object { "{0,-28} {1,8:N0}" -f $_.Name, $_.Length }

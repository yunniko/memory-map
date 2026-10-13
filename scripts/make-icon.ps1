# Makes assets/memory-map.ico, the Windows exe's and the window's icon, from assets/memory-map.svg
# (the one source; the Linux package installs the SVG itself). Each size is drawn from the SVG by
# headless Microsoft Edge, so small sizes are rendered, not shrunk, and stored as PNG in the .ico.
# Run after changing the SVG, and commit both: pwsh scripts/make-icon.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$svg = Join-Path $root 'assets\memory-map.svg'
$ico = Join-Path $root 'assets\memory-map.ico'
$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edge)) { throw "Microsoft Edge not found at $edge" }
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

$work = Join-Path ([IO.Path]::GetTempPath()) "memory-map-icon-$PID"
New-Item -ItemType Directory -Force $work | Out-Null
try {
    Copy-Item $svg (Join-Path $work 'icon.svg')
    $pngs = foreach ($size in $sizes) {
        $page = Join-Path $work "$size.html"
        $png = Join-Path $work "$size.png"
        Set-Content $page "<html><body style='margin:0;background:transparent'><img src='icon.svg' width=$size height=$size style='display:block'></body></html>"
        # Its own profile folder, so each run is a separate Edge that exits when done instead of
        # handing the job to one still running.
        $edgeArgs = '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
            '--default-background-color=00000000', "--user-data-dir=`"$work\profile-$size`"",
            "--window-size=$size,$size", "--screenshot=`"$png`"", ([Uri]$page).AbsoluteUri
        $run = Start-Process $edge $edgeArgs -PassThru -WindowStyle Hidden
        if (-not $run.WaitForExit(60000)) { $run.Kill($true); throw "Edge did not finish the $size px image" }
        if (-not (Test-Path $png)) { throw "Edge drew no $size px image" }
        , [IO.File]::ReadAllBytes($png)
    }

    # ICONDIR, one ICONDIRENTRY per size, then the PNGs (a width or height of 256 is written as 0).
    $out = [IO.MemoryStream]::new()
    $w = [IO.BinaryWriter]::new($out)
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i] % 256
        $w.Write([byte]$s); $w.Write([byte]$s); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $w.Write($png) }
    $w.Flush()
    [IO.File]::WriteAllBytes($ico, $out.ToArray())
    "Wrote $ico ($($sizes -join ', ') px, $([math]::Round($out.Length / 1KB)) KB)"
}
finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }

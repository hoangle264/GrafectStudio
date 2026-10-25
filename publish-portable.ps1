# Builds a self-contained portable folder + versioned zip under artifacts/portable.
#   .\publish-portable.ps1                 -> version = yyyy.MM.dd-<git short hash>
#   .\publish-portable.ps1 -Version 1.2.0  -> explicit version
#   .\publish-portable.ps1 -SkipBridge     -> don't build/ship the TIA bridge
param(
    [string]$Version,
    [switch]$SkipBridge
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not $Version) {
    $hash = (git -C $root rev-parse --short HEAD 2>$null)
    $dirty = if (git -C $root status --porcelain 2>$null) { '-dirty' } else { '' }
    $Version = "$(Get-Date -Format 'yyyy.MM.dd')-$hash$dirty"
}

$outRoot = Join-Path $root 'artifacts\portable'
$out     = Join-Path $outRoot "GrafcetStudio-$Version"
$zip     = "$out.zip"
foreach ($p in $out, $zip) { if (Test-Path $p) { Remove-Item $p -Recurse -Force } }

dotnet publish "$root\src\GrafcetStudio.App\GrafcetStudio.App.csproj" -c Release -r win-x64 --self-contained true -o $out
if ($LASTEXITCODE) { throw 'App publish failed' }

# TIA bridges (net48) live in their own subfolder so their DLLs don't clash with .NET 8 ones.
# One exe per TIA version; each only works where that TIA Portal version is installed.
# A bridge is skipped when this machine lacks that version's Openness DLL (it would build as a stub).
if (-not $SkipBridge) {
    foreach ($tia in 'V19', 'V20') {
        $proj = "$root\src\GrafcetStudio.TiaBridge.$tia\GrafcetStudio.TiaBridge.$tia.csproj"
        $openness = "$(dotnet msbuild $proj -getProperty:TiaOpennessDir -nologo)".Trim()
        if ($LASTEXITCODE -or -not $openness -or -not (Test-Path "$openness\Siemens.Engineering.dll")) { Write-Warning "TIA $tia bridge skipped (TIA Portal $tia Openness not found on this machine)"; continue }
        dotnet publish $proj -c Release -o "$out\tia-bridge"
        if ($LASTEXITCODE) { Write-Warning "TIA $tia bridge not built - continuing without it" }
    }
}

# Drop debug symbols to keep the zip small; marker file switches WebView2 data into .\data
Get-ChildItem $out -Recurse -Filter *.pdb | Remove-Item -Force
Set-Content (Join-Path $out 'version.txt') $Version
New-Item (Join-Path $out 'portable.flag') -ItemType File -Force | Out-Null

Compress-Archive -Path $out -DestinationPath $zip
Write-Host "Done: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root "companion\build.ps1"
$companion = Join-Path $root "companion\bin\TartarusIO.Companion.exe"
$sources = Get-ChildItem -LiteralPath (Join-Path $root "companion\src") -Filter "*.cs" -File
$buildInputs = @($sources) + @(
    $(Get-Item -LiteralPath (Join-Path $root "companion\app.manifest"))
    $(Get-Item -LiteralPath (Join-Path $root "companion\build.ps1"))
)
$needsBuild = -not (Test-Path -LiteralPath $companion)

if (-not $needsBuild) {
    $binaryTime = (Get-Item -LiteralPath $companion).LastWriteTimeUtc
    $needsBuild = $buildInputs | Where-Object { $_.LastWriteTimeUtc -gt $binaryTime } | Select-Object -First 1
}

if ($needsBuild) {
    & $buildScript
}

Start-Process -FilePath $companion -ArgumentList @("--root", "`"$root`"")
Write-Output "Tartarus IO Companion started at http://127.0.0.1:47651/"

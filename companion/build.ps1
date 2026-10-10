$ErrorActionPreference = "Stop"

$companionRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $companionRoot "src"
$outputRoot = Join-Path $companionRoot "bin"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$output = Join-Path $outputRoot "TartarusIO.Companion.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The Windows .NET Framework C# compiler was not found at $compiler."
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" -File |
    Sort-Object Name |
    ForEach-Object { $_.FullName }

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/warnaserror+",
    "/out:$output",
    "/win32manifest:$companionRoot\app.manifest",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Management.dll",
    "/reference:System.Web.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:System.Windows.Forms.dll"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Tartarus IO Companion failed to compile."
}

Write-Output "Built $output"

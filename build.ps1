param([string]$OutputDir = (Join-Path $PSScriptRoot 'dist'))

$ErrorActionPreference = 'Stop'
$outputDir = [IO.Path]::GetFullPath($OutputDir)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $PSScriptRoot 'Program.cs'
$manifest = Join-Path $PSScriptRoot 'app.manifest'
$target = Join-Path $outputDir 'ImageToJpg-0.1.2.exe'

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:$manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /out:$target $source
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
Write-Output $target

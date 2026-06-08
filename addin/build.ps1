# Builds Eplan.EplAddIn.EngravingData.dll against the installed EPLAN API assemblies.
# Run:  powershell -ExecutionPolicy Bypass -File addin\build.ps1
$ErrorActionPreference = "Stop"

$bin = "C:\Program Files\EPLAN\Platform\2026.0.3\Bin"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = Join-Path $PSScriptRoot "Eplan.EplAddIn.EngravingData.cs"
$outDir = Join-Path $PSScriptRoot "bin"
$out = Join-Path $outDir "Eplan.EplAddIn.EngravingData.dll"

New-Item -ItemType Directory -Force $outDir | Out-Null

$refs = @(
  "Eplan.EplApi.AFu.dll",
  "Eplan.EplApi.Baseu.dll",
  "Eplan.EplApi.DataModelu.dll",
  "Eplan.EplApi.HEServicesu.dll",
  "Eplan.EplApi.Guiu.dll"
) | ForEach-Object { "/reference:`"$(Join-Path $bin $_)`"" }

$args = @("/nologo", "/target:library", "/platform:x64", "/out:`"$out`"") + $refs + @("`"$src`"")
Write-Output "csc $($args -join ' ')"
& $csc @args
if ($LASTEXITCODE -eq 0) { Write-Output "`nBUILD OK -> $out" } else { Write-Output "`nBUILD FAILED ($LASTEXITCODE)"; exit 1 }

# ---- ProjectCheck add-in ----
$src2 = Join-Path $PSScriptRoot "Eplan.EplAddIn.ProjectCheck.cs"
$out2 = Join-Path $outDir "Eplan.EplAddIn.ProjectCheck.dll"
$args2 = @("/nologo", "/target:library", "/platform:x64", "/out:`"$out2`"") + $refs + @("`"$src2`"")
Write-Output ""
Write-Output "csc $($args2 -join ' ')"
& $csc @args2
if ($LASTEXITCODE -eq 0) { Write-Output "`nBUILD OK -> $out2" } else { Write-Output "`nBUILD FAILED ($LASTEXITCODE)"; exit 1 }

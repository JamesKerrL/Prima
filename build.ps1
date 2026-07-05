#requires -Version 5
# Builds Prima end to end: native (CMake + MinGW) first so the prima_c DLL exists,
# then the .NET solution (which copies that DLL next to each output).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

& "$root\build-native.ps1"

Write-Host '== .NET: build solution ==' -ForegroundColor Cyan
dotnet build "$root\Prima.slnx" -c Debug
if ($LASTEXITCODE -ne 0) { throw '.NET build failed' }

Write-Host 'Build complete.' -ForegroundColor Green

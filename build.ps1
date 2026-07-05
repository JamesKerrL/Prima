#requires -Version 5
# Builds Prima end to end: native (CMake + MinGW) first so the prima_c DLL exists,
# then the .NET solution (which copies that DLL next to each output).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Make freshly-installed tools (cmake, gcc) visible even in a stale shell.
$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User')

Write-Host '== Native: configure (CMake / MinGW Makefiles) ==' -ForegroundColor Cyan
cmake -S $root -B "$root\build\native" -G "MinGW Makefiles" -DCMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE -ne 0) { throw 'CMake configure failed' }

Write-Host '== Native: build ==' -ForegroundColor Cyan
cmake --build "$root\build\native"
if ($LASTEXITCODE -ne 0) { throw 'CMake build failed' }

Write-Host '== .NET: build solution ==' -ForegroundColor Cyan
dotnet build "$root\Prima.slnx" -c Debug
if ($LASTEXITCODE -ne 0) { throw '.NET build failed' }

Write-Host 'Build complete.' -ForegroundColor Green

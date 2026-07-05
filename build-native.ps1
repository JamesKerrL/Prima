#requires -Version 5
# Configures and builds only the native side (CMake + MinGW) so prima_c.dll
# exists. Split out from build.ps1 so VS Code tasks can invoke it directly via
# -File, avoiding the quoting problems of passing this as an inline -Command
# string through the shell task's extra layer of re-parsing.
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

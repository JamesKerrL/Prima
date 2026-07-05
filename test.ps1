#requires -Version 5
# Runs both test suites. Assumes build.ps1 has configured the native build dir.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User')

Write-Host '== C++ engine tests (GoogleTest / CTest) ==' -ForegroundColor Cyan
ctest --test-dir "$root\build\native" --output-on-failure
if ($LASTEXITCODE -ne 0) { throw 'C++ tests failed' }

Write-Host '== .NET tests (xUnit) ==' -ForegroundColor Cyan
dotnet test "$root\Prima.slnx" -c Debug
if ($LASTEXITCODE -ne 0) { throw '.NET tests failed' }

Write-Host 'All tests passed.' -ForegroundColor Green

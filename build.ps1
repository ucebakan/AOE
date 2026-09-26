param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
& taskkill.exe /F /IM 4UnityAOEManager.exe
if (Get-Process -Name 4UnityAOEManager -ErrorAction SilentlyContinue) { throw 'Manager is still running; refusing to build a locked executable.' }
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
$cmakePath = if ($cmakeCommand) { $cmakeCommand.Source } else { 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' }
if (-not (Test-Path -LiteralPath $cmakePath)) { throw 'Install Visual Studio C++ x64 Build Tools and CMake.' }
& $cmakePath -S $taskRoot -B "$taskRoot\build" -G 'Visual Studio 18 2026' -A x64
if ($LASTEXITCODE) { throw 'CMake configure failed.' }
& $cmakePath --build "$taskRoot\build" --config Release --parallel
if ($LASTEXITCODE) { throw 'Release x64 build failed.' }
if (-not $SkipTests) {
    $ctestPath = Join-Path (Split-Path $cmakePath) 'ctest.exe'
    & $ctestPath --test-dir "$taskRoot\build" -C Release --output-on-failure
    if ($LASTEXITCODE) { throw 'Verification failed.' }
}
Write-Host "Ready: $taskRoot\build\Release\4UnityAOEManager.exe"

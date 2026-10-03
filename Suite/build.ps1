param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
$cmakePath = if ($cmakeCommand) { $cmakeCommand.Source } else { 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' }
if (-not (Test-Path -LiteralPath $cmakePath)) { throw 'Visual Studio C++ x64 Build Tools and CMake are required.' }
& $cmakePath -S "$taskRoot/native" -B "$taskRoot/build/native" -G 'Visual Studio 18 2026' -A x64
if ($LASTEXITCODE) { throw 'Native configure failed.' }
& $cmakePath --build "$taskRoot/build/native" --config Release --target UnityAoe UnityCounter --parallel
if ($LASTEXITCODE) { throw 'Native modules failed to build.' }
dotnet publish "$taskRoot/src/4UnityTools.csproj" -c Release -o "$taskRoot/build/publish" -p:DebugType=None -p:SelfContained=true
if ($LASTEXITCODE) { throw 'Suite publish failed.' }
& "$taskRoot/verify-uac.ps1" -Path "$taskRoot/build/publish/4UnityTools.exe"
if (-not $SkipTests) {
    # Test UI without a UAC prompt; the distributed native EXE is verified above.
    $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
    $testDll = "$taskRoot/src/bin/Release/net9.0-windows/win-x64/4UnityTools.dll"
    $test = Start-Process -FilePath $dotnetPath -ArgumentList @("`"$testDll`"", '--self-test', "`"$taskRoot/evidence`"") -WindowStyle Hidden -PassThru
    if (-not $test.WaitForExit(120000)) { throw 'Suite UI tests did not finish within 120 seconds.' }
    if ($test.ExitCode -ne 0) { throw "Suite tests failed ($($test.ExitCode)); inspect evidence/suite-tests.json." }
    $recovery = Start-Process -FilePath $dotnetPath -ArgumentList @("`"$testDll`"", '--recovery-test', "`"$taskRoot/evidence/recovery-tests-2026-10-03.json`"") -WindowStyle Hidden -PassThru
    if (-not $recovery.WaitForExit(120000)) { throw 'Patch recovery tests timed out.' }
    if ($recovery.ExitCode -ne 0) { throw 'Patch recovery tests failed; inspect evidence/recovery-tests-2026-10-03.json.' }
}
[xml]$suiteProject = Get-Content -LiteralPath "$taskRoot/src/4UnityTools.csproj"
$release = Join-Path $taskRoot ("releases/" + $suiteProject.Project.PropertyGroup.Version)
New-Item -ItemType Directory -Path $release -Force | Out-Null
Copy-Item -LiteralPath "$taskRoot/build/publish/4UnityTools.exe" -Destination "$release/4UnityTools.exe" -Force
& "$taskRoot/verify-uac.ps1" -Path "$release/4UnityTools.exe"
Get-Item -LiteralPath "$release/4UnityTools.exe" | Select-Object FullName,Length,LastWriteTime

param()
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
dotnet publish "$taskRoot/src/Multikill.csproj" -c Release -o "$taskRoot/build/publish" -p:DebugType=None
if ($LASTEXITCODE) { throw 'Multikill publish failed.' }
& "$taskRoot/../Suite/verify-uac.ps1" -Path "$taskRoot/build/publish/4UnityMultikill.exe"
$dotnetPath = (Get-Command dotnet).Source
New-Item -ItemType Directory -Path "$taskRoot/evidence" -Force | Out-Null
$test = Start-Process -FilePath $dotnetPath -ArgumentList @("`"$taskRoot/src/bin/Release/net9.0-windows/win-x64/4UnityMultikill.dll`"", '--self-test', "`"$taskRoot/evidence/tests.json`"") -WindowStyle Hidden -PassThru
if (-not $test.WaitForExit(60000) -or $test.ExitCode -ne 0) { throw 'Multikill tests failed; inspect evidence/tests.json.' }
New-Item -ItemType Directory -Path "$taskRoot/releases/1.2.0" -Force | Out-Null
Copy-Item -LiteralPath "$taskRoot/build/publish/4UnityMultikill.exe" -Destination "$taskRoot/releases/1.2.0/4UnityMultikill.exe" -Force
& "$taskRoot/../Suite/verify-uac.ps1" -Path "$taskRoot/releases/1.2.0/4UnityMultikill.exe"

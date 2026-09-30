param([switch]$SelfTest)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'tools/MobTP/MobTP.csproj'
dotnet publish $project -c Release -o (Join-Path $PSScriptRoot 'build')
if ($LASTEXITCODE -ne 0) { throw 'MobTP publish failed.' }
if ($SelfTest) {
    dotnet (Join-Path $PSScriptRoot 'tools/MobTP/bin/Release/net9.0-windows/win-x64/MobTP.dll') --self-test
    if ($LASTEXITCODE -ne 0) { throw 'MobTP self-test failed.' }
}

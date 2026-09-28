param([switch]$SelfTest)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\SpeedJump.csproj'
$output = Join-Path $PSScriptRoot 'build\publish'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if ($SelfTest) {
    $dll = Join-Path $PSScriptRoot 'src\bin\Release\net9.0-windows\win-x64\4UnitySpeedJump.dll'
    dotnet $dll --self-test (Join-Path $PSScriptRoot 'build\self-test.json')
    if ($LASTEXITCODE -ne 0) { throw 'Self-test failed. Exact current TClient.exe is required at C:\Games\4Unity\TClient.exe.' }
}
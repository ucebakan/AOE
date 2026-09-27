param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\InvisibleAggro.csproj'
& dotnet build $project -c Release --nologo
if ($LASTEXITCODE) { throw 'Build failed.' }
if (-not $SkipTests) {
    $output = Join-Path $PSScriptRoot 'build'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $assembly = Join-Path $PSScriptRoot 'src\bin\Release\net9.0-windows\win-x64\4UnityInvisibleAggro.dll'
    & dotnet $assembly --self-test (Join-Path $output 'self-test.json')
    if ($LASTEXITCODE) { throw 'Self-tests failed. The current TClient.exe is required for disk-profile tests.' }
}
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $PSScriptRoot 'build\release') --nologo
if ($LASTEXITCODE) { throw 'Publish failed.' }

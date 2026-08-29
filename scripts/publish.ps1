$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $repositoryRoot 'artifacts\publish'

dotnet test (Join-Path $repositoryRoot 'tests\PcCare.Core.Tests\PcCare.Core.Tests.csproj') -c Release
dotnet publish (Join-Path $repositoryRoot 'src\PcCare.App\PcCare.App.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -o $outputDirectory

$executable = Join-Path $outputDirectory 'PcCare.exe'
$hash = (Get-FileHash $executable -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  PcCare.exe" | Set-Content (Join-Path $outputDirectory 'PcCare.exe.sha256') -Encoding ascii

Write-Host "Published: $executable"
Write-Host "SHA256: $hash"

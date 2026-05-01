param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "HAL-9000.csproj"
& dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$dll = Join-Path $PSScriptRoot "GameData\HAL-9000\Plugins\HAL9000.dll"
Write-Host "Built $dll"

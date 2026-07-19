[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputRoot))
$publishDirectory = Join-Path $resolvedOutputRoot "AssetManager-win-x64"
$archivePath = Join-Path $resolvedOutputRoot "AssetManager-win-x64.zip"
$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if ($resolvedOutputRoot -ne $repositoryRoot -and
    -not $resolvedOutputRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must be inside the repository."
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse
}

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

dotnet publish (Join-Path $repositoryRoot "src/AssetManager.App/AssetManager.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishProfile=PortableWinX64 `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Release publish failed."
}

$executable = Join-Path $publishDirectory "AssetManager.App.exe"
if (-not (Test-Path -LiteralPath $executable)) {
    throw "AssetManager.App.exe was not found in the publish directory."
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archivePath -CompressionLevel Optimal

Write-Output "Publish: $publishDirectory"
Write-Output "ZIP: $archivePath"

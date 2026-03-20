[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Invoke-DotNet
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$solutionPath = Join-Path $repoRoot "Arcade.sln"
$releaseOutputDirectory = Join-Path $repoRoot "Arcade\\bin\\x64\\Release"
$repoManifestPath = Join-Path $repoRoot "repo.json"
$distDirectory = Join-Path $repoRoot "dist"
$packagePath = Join-Path $distDirectory "Arcade.zip"
$requiredPackageFiles = @(
    "Arcade.deps.json",
    "Arcade.dll",
    "Arcade.json",
    "hangman_words.txt",
    "sudoku_puzzles.txt"
)

$ownerAndRepo = "ThePurplePigeon/arcade"
$rawRoot = "https://raw.githubusercontent.com/$ownerAndRepo/master"
$downloadUrl = "$rawRoot/dist/Arcade.zip"
$iconUrl = "$rawRoot/Data/Arcade_Logo.png"

Write-Host "==> Restore"
Invoke-DotNet -Arguments @("restore", $solutionPath)

Write-Host "==> Build (Release)"
Invoke-DotNet -Arguments @("build", $solutionPath, "-c", "Release", "-v", "minimal")

Write-Host "==> Test"
Invoke-DotNet -Arguments @("test", $solutionPath, "-c", "Release", "-v", "minimal")

if (-not (Test-Path -Path $releaseOutputDirectory -PathType Container))
{
    throw "Release output directory not found: $releaseOutputDirectory"
}

$packageSources = New-Object System.Collections.Generic.List[string]
foreach ($fileName in $requiredPackageFiles)
{
    $filePath = Join-Path $releaseOutputDirectory $fileName
    if (-not (Test-Path -Path $filePath -PathType Leaf))
    {
        throw "Required package file missing: $filePath"
    }

    $packageSources.Add($filePath)
}

Write-Host "==> Package dist/Arcade.zip"
New-Item -ItemType Directory -Path $distDirectory -Force | Out-Null
if (Test-Path -Path $packagePath -PathType Leaf)
{
    Remove-Item -Path $packagePath -Force
}

Compress-Archive -Path $packageSources.ToArray() -DestinationPath $packagePath -CompressionLevel Optimal

$builtManifestPath = Join-Path $releaseOutputDirectory "Arcade.json"
$builtManifest = Get-Content -Path $builtManifestPath -Raw | ConvertFrom-Json
if (-not $builtManifest.AssemblyVersion)
{
    throw "AssemblyVersion was not found in $builtManifestPath"
}

Write-Host "==> Sync repo.json metadata"
$parsedManifest = Get-Content -Path $repoManifestPath -Raw | ConvertFrom-Json
$entries = @($parsedManifest)
if ($entries.Count -lt 1)
{
    throw "repo.json must contain at least one manifest entry."
}

$entry = $entries[0]
$entry.RepoUrl = "https://github.com/$ownerAndRepo"
$entry.DownloadLinkInstall = $downloadUrl
$entry.DownloadLinkUpdate = $downloadUrl
$entry.IconUrl = $iconUrl
$entry.ImageUrls = @($iconUrl)
$entry.AssemblyVersion = [string]$builtManifest.AssemblyVersion
$entry.LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()

$repoManifestJson = ConvertTo-Json -InputObject $entries -Depth 10
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($repoManifestPath, $repoManifestJson, $utf8NoBom)

Write-Host "Publish complete."
Write-Host "Package: $packagePath"
Write-Host "AssemblyVersion: $($entry.AssemblyVersion)"
Write-Host "LastUpdate: $($entry.LastUpdate)"

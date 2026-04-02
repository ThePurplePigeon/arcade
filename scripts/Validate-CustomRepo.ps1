[CmdletBinding()]
param(
    [string]$ExpectedOwnerAndRepo = "ThePurplePigeon/arcade"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$releaseOutputDirectory = Join-Path $repoRoot "Arcade\bin\x64\Release"
$repoManifestPath = Join-Path $repoRoot "repo.json"
$distPackagePath = Join-Path $repoRoot "dist\Arcade.zip"
$corePackageFiles = @(
    "Arcade.deps.json",
    "Arcade.dll",
    "Arcade.json"
)
$excludedPackageExtensions = @(".pdb", ".xml")

function Assert-EqualValue
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Actual
    )

    if ($Actual -ne $Expected)
    {
        throw "$Name mismatch. Expected '$Expected' but found '$Actual'."
    }
}

if (-not (Test-Path -Path $releaseOutputDirectory -PathType Container))
{
    throw "Release output directory not found: $releaseOutputDirectory"
}

foreach ($fileName in $corePackageFiles)
{
    $filePath = Join-Path $releaseOutputDirectory $fileName
    if (-not (Test-Path -Path $filePath -PathType Leaf))
    {
        throw "Release output is missing core file: $filePath"
    }
}

$expectedPackageEntries = @(
    Get-ChildItem -Path $releaseOutputDirectory -File | Where-Object {
        $extension = $_.Extension.ToLowerInvariant()
        $excludedPackageExtensions -notcontains $extension
    } | Select-Object -ExpandProperty Name
)

if ($expectedPackageEntries.Count -lt 1)
{
    throw "Release output did not produce any package entries."
}

if (-not (Test-Path -Path $repoManifestPath -PathType Leaf))
{
    throw "repo.json not found at expected path: $repoManifestPath"
}

if (-not (Test-Path -Path $distPackagePath -PathType Leaf))
{
    throw "dist package not found at expected path: $distPackagePath"
}

$builtManifestPath = Join-Path $releaseOutputDirectory "Arcade.json"
$builtManifest = Get-Content -Path $builtManifestPath -Raw | ConvertFrom-Json
$builtAssemblyVersion = [string]$builtManifest.AssemblyVersion
if ([string]::IsNullOrWhiteSpace($builtAssemblyVersion))
{
    throw "AssemblyVersion is missing from release manifest: $builtManifestPath"
}

$repoEntries = @(Get-Content -Path $repoManifestPath -Raw | ConvertFrom-Json)
if ($repoEntries.Count -lt 1)
{
    throw "repo.json must contain at least one manifest entry."
}

$repoEntry = $repoEntries[0]
$rawRoot = "https://raw.githubusercontent.com/$ExpectedOwnerAndRepo/master"
$expectedDownloadUrl = "$rawRoot/dist/Arcade.zip"
$expectedIconUrl = "$rawRoot/Data/Arcade_Logo.png"
$expectedRepoUrl = "https://github.com/$ExpectedOwnerAndRepo"

Assert-EqualValue -Name "RepoUrl" -Expected $expectedRepoUrl -Actual ([string]$repoEntry.RepoUrl)
Assert-EqualValue -Name "DownloadLinkInstall" -Expected $expectedDownloadUrl -Actual ([string]$repoEntry.DownloadLinkInstall)
Assert-EqualValue -Name "DownloadLinkUpdate" -Expected $expectedDownloadUrl -Actual ([string]$repoEntry.DownloadLinkUpdate)
Assert-EqualValue -Name "IconUrl" -Expected $expectedIconUrl -Actual ([string]$repoEntry.IconUrl)
Assert-EqualValue -Name "AssemblyVersion" -Expected $builtAssemblyVersion -Actual ([string]$repoEntry.AssemblyVersion)

$lastUpdate = 0L
if (-not [long]::TryParse([string]$repoEntry.LastUpdate, [ref]$lastUpdate) -or $lastUpdate -le 0)
{
    throw "LastUpdate must be a positive Unix timestamp. Found '$($repoEntry.LastUpdate)'."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($distPackagePath)
try
{
    $entryNames = @($zip.Entries | ForEach-Object { $_.FullName })

    $nestedEntries = @($entryNames | Where-Object { $_ -match "[\\/]" })
    if ($nestedEntries.Count -gt 0)
    {
        throw "dist/Arcade.zip should place files at zip root. Nested entries found: $($nestedEntries -join ', ')"
    }

    $missingEntries = @($expectedPackageEntries | Where-Object { $entryNames -notcontains $_ })
    if ($missingEntries.Count -gt 0)
    {
        throw "dist/Arcade.zip is missing files from release output: $($missingEntries -join ', ')"
    }

    $unexpectedEntries = @($entryNames | Where-Object { $expectedPackageEntries -notcontains $_ })
    if ($unexpectedEntries.Count -gt 0)
    {
        throw "dist/Arcade.zip has unexpected files not present in release output: $($unexpectedEntries -join ', ')"
    }

    foreach ($coreFile in $corePackageFiles)
    {
        if (-not ($entryNames -contains $coreFile))
        {
            throw "dist/Arcade.zip is missing core file '$coreFile'."
        }
    }

    $manifestEntry = $zip.Entries | Where-Object { $_.FullName -eq "Arcade.json" } | Select-Object -First 1
    if ($null -eq $manifestEntry)
    {
        throw "dist/Arcade.zip does not contain Arcade.json."
    }

    $manifestStream = $manifestEntry.Open()
    $manifestReader = New-Object System.IO.StreamReader($manifestStream)
    try
    {
        $zipManifest = $manifestReader.ReadToEnd() | ConvertFrom-Json
    }
    finally
    {
        $manifestReader.Dispose()
        $manifestStream.Dispose()
    }

    $zipAssemblyVersion = [string]$zipManifest.AssemblyVersion
    Assert-EqualValue -Name "dist/Arcade.zip AssemblyVersion" -Expected $builtAssemblyVersion -Actual $zipAssemblyVersion
}
finally
{
    $zip.Dispose()
}

Write-Host "Custom repo metadata validation passed."
Write-Host "AssemblyVersion: $builtAssemblyVersion"
Write-Host "Package: $distPackagePath"

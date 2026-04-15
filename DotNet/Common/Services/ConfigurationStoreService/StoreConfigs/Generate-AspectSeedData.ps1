<#
.SYNOPSIS
    Reads all JSON files from the AspectConfigs folder and generates Combine_Aspect_SeedData.json.

.DESCRIPTION
    Each file in AspectConfigs contains an array of aspect configuration objects.
    This script transforms each aspect into the SeedData schema used by the
    ConfigurationStore (ComponentName = "AspectConfiguration") and writes the
    combined output to Combine_Aspect_SeedData.json in the same directory.

.EXAMPLE
    .\Generate-AspectSeedData.ps1
    .\Generate-AspectSeedData.ps1 -AspectConfigsPath ".\AspectConfigs" -OutputPath ".\Combine_Aspect_SeedData.json"
#>

param(
    [string]$AspectConfigsPath = (Join-Path $PSScriptRoot "AspectConfigs"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "Combine_Aspect_SeedData.json")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Validate AspectConfigs folder exists
if (-not (Test-Path $AspectConfigsPath)) {
    Write-Error "AspectConfigs folder not found at: $AspectConfigsPath"
    exit 1
}

# Collect all JSON files from AspectConfigs (including subdirectories)
$jsonFiles = Get-ChildItem -Path $AspectConfigsPath -Filter "*.json" -Recurse
if ($jsonFiles.Count -eq 0) {
    Write-Warning "No JSON files found in: $AspectConfigsPath"
    exit 0
}

Write-Host "Found $($jsonFiles.Count) JSON file(s) in AspectConfigs" -ForegroundColor Cyan

$seedEntries = [System.Collections.Generic.List[object]]::new()

foreach ($file in $jsonFiles) {
    Write-Host "  Processing: $($file.Name)" -ForegroundColor Gray

    $rawContent = Get-Content -Raw -Path $file.FullName
    $aspects = $rawContent | ConvertFrom-Json

    # Handle both single object and array formats
    if ($aspects -isnot [System.Collections.IEnumerable] -or $aspects -is [PSCustomObject]) {
        $aspects = @($aspects)
    }

    foreach ($aspect in $aspects) {
        if (-not $aspect.Name) {
            Write-Warning "    Skipping entry without 'Name' property in $($file.Name)"
            continue
        }

        # Build Description: "{Name} {Description} {Keywords joined by comma}"
        $keywords = ""
        if ($aspect.Keywords -and $aspect.Keywords.Count -gt 0) {
            $keywords = ($aspect.Keywords -join ", ")
        }

        $description = "$($aspect.Name) $($aspect.Description) $keywords".Trim()

        # Serialize the full aspect object as a compact JSON string for Configuration field
        $configuration = $aspect | ConvertTo-Json -Depth 20 -Compress

        $seedEntry = [ordered]@{
            ComponentName     = "AspectConfiguration"
            ConfigurationName = $aspect.Name
            Description       = $description
            Configuration     = $configuration
            DependsOn         = @()
        }

        $seedEntries.Add($seedEntry)
    }
}

# Write output with UTF-8 encoding (no BOM)
$outputJson = $seedEntries | ConvertTo-Json -Depth 10
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($OutputPath, $outputJson, $utf8NoBom)

Write-Host ""
Write-Host "Generated $($seedEntries.Count) AspectConfiguration entries" -ForegroundColor Green
Write-Host "Output: $OutputPath" -ForegroundColor Green
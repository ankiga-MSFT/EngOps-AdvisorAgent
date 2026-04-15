<#
.SYNOPSIS
    Combines all configuration sources into environment-specific SeedData files.

.DESCRIPTION
    1. Runs Generate-AspectSeedData.ps1 to produce Combine_Aspect_SeedData.json from AspectConfigs (shared).
    2. For each environment (default: test, ppe):
       a. Reads each file in $SeedDataSources with env-prefix fallback
          (e.g., for env 'test' and source 'Skills.json': tries 'test.Skills.json', falls back to 'Skills.json').
       b. Appends shared AspectConfiguration entries.
       c. Writes the env-specific SeedData file (e.g., 'test.SeedData.json', 'ppe.SeedData.json').

    To add a new source file, simply add its filename to the $SeedDataSources array below.
    To add a new environment, add it to the $Environments parameter.

.EXAMPLE
    .\CombineAll.ps1
    # Produces test.SeedData.json and ppe.SeedData.json using defaults.

.EXAMPLE
    .\CombineAll.ps1 -Environments @('test','ppe', 'prvw', 'prod')
    # Produces test.SeedData.json, ppe.SeedData.json, prvw.SeedData.json, and prod.SeedData.json.
#>

param(
    [string]$OutputDir = $PSScriptRoot,
    [string[]]$Environments = @('test')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================================
# CONFIGURATION: Add any new JSON files (in SeedData schema) to this list.
# Each file should contain an array of objects with:
#   ComponentName, ConfigurationName, Description, Configuration, DependsOn
# ============================================================================
$SeedDataSources = @(
    "Skills.json"
    "tools.json"           # <-- uncomment or add new files here
    # "Orchestrator.json"
)

# ============================================================================
# Step 0: Generate AspectConfiguration entries from AspectConfigs folder (shared)
# ============================================================================
$generateScript = Join-Path $PSScriptRoot "Generate-AspectSeedData.ps1"
$aspectSeedDataPath = Join-Path $PSScriptRoot "Combine_Aspect_SeedData.json"

if (-not (Test-Path $generateScript)) {
    Write-Error "Generate-AspectSeedData.ps1 not found at: $generateScript"
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Step 0: Generating AspectConfiguration entries (shared)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
& $generateScript -OutputPath $aspectSeedDataPath

if (-not (Test-Path $aspectSeedDataPath)) {
    Write-Error "Generate-AspectSeedData.ps1 did not produce output at: $aspectSeedDataPath"
    exit 1
}

$aspectEntries = Get-Content -Raw -Path $aspectSeedDataPath | ConvertFrom-Json

if ($aspectEntries -isnot [System.Collections.IEnumerable] -or $aspectEntries -is [PSCustomObject]) {
    $aspectEntries = @($aspectEntries)
}

Write-Host "  -> $($aspectEntries.Count) AspectConfiguration entries (shared)" -ForegroundColor DarkGray

# ============================================================================
# Process each environment
# ============================================================================
foreach ($envName in $Environments) {
    $outputPath = Join-Path $OutputDir "$envName.SeedData.json"

    Write-Host ""
    Write-Host "########################################" -ForegroundColor Magenta
    Write-Host "  Environment: $envName" -ForegroundColor Magenta
    Write-Host "  Output:      $outputPath" -ForegroundColor Magenta
    Write-Host "########################################" -ForegroundColor Magenta

    # -- Backup existing output -----------------------------------------------
    if (Test-Path $outputPath) {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $backupPath = $outputPath -replace '\.json$', "_backup_$timestamp.json"
        Copy-Item -Path $outputPath -Destination $backupPath -Force
        Write-Host "  Backup created: $backupPath" -ForegroundColor Yellow
    }

    # -- Step 1: Read source files (env-aware) --------------------------------
    Write-Host ""
    Write-Host "  Step 1: Reading SeedData source files for '$envName'" -ForegroundColor Cyan

    $allEntries = [System.Collections.Generic.List[object]]::new()

    foreach ($sourceFile in $SeedDataSources) {
        $envSourceFile = "$envName.$sourceFile"
        $envSourcePath = Join-Path $PSScriptRoot $envSourceFile
        $baseSourcePath = Join-Path $PSScriptRoot $sourceFile

        if (Test-Path $envSourcePath) {
            $resolvedPath = $envSourcePath
            $resolvedFile = $envSourceFile
        } elseif (Test-Path $baseSourcePath) {
            $resolvedPath = $baseSourcePath
            $resolvedFile = "$sourceFile (base fallback)"
        } else {
            Write-Warning "    Source file not found, skipping: $envSourceFile / $sourceFile"
            continue
        }

        Write-Host "    Reading: $resolvedFile" -ForegroundColor Gray
        $entries = Get-Content -Raw -Path $resolvedPath | ConvertFrom-Json

        if ($entries -isnot [System.Collections.IEnumerable] -or $entries -is [PSCustomObject]) {
            $entries = @($entries)
        }

        foreach ($entry in $entries) {
            $allEntries.Add($entry)
        }

        Write-Host "      -> $($entries.Count) entries" -ForegroundColor DarkGray
    }

    # -- Step 2: Append shared AspectConfiguration entries --------------------
    foreach ($entry in $aspectEntries) {
        $allEntries.Add($entry)
    }
    Write-Host "    + $($aspectEntries.Count) AspectConfiguration entries (shared)" -ForegroundColor DarkGray

    # -- Step 3: Write env-specific SeedData ----------------------------------
    $outputJson = $allEntries | ConvertTo-Json -Depth 10
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($outputPath, $outputJson, $utf8NoBom)

    # -- Per-environment summary ----------------------------------------------
    Write-Host ""
    Write-Host "  [$envName] Total entries: $($allEntries.Count)" -ForegroundColor Green
    $grouped = $allEntries | Group-Object -Property ComponentName
    foreach ($group in $grouped) {
        Write-Host "    $($group.Name): $($group.Count)" -ForegroundColor DarkGreen
    }
    Write-Host "  Output: $outputPath" -ForegroundColor Green
}

# ============================================================================
# Final summary
# ============================================================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Done! Generated $($Environments.Count) environment SeedData files." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
foreach ($envName in $Environments) {
    Write-Host "  -> $(Join-Path $OutputDir "$envName.SeedData.json")" -ForegroundColor Green
}

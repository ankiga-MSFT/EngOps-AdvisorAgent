$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$aspectConfigsFolder = Join-Path $scriptDir "..\..\..\Common\Services\ConfigurationStoreService\StoreConfigs\AspectConfigs"
$outputFile = Join-Path $scriptDir "KnowledgeGraph.json"

# Read all JSON files and collect aspects
$allAspects = @()
Get-ChildItem -Path $aspectConfigsFolder -Filter "*.json" | ForEach-Object {
    $content = Get-Content -Path $_.FullName -Raw | ConvertFrom-Json
    foreach ($aspect in $content) {
        # Build filters summary from the aspect's Filters array
        $filtersSummary = @{}
        if ($aspect.Filters) {
            foreach ($filter in $aspect.Filters) {
                # Skip inactive filters (treat missing IsActive as active)
                if ($null -ne $filter.IsActive -and -not $filter.IsActive) { continue }

                $name = $filter.Name
                $desc = $filter.Description
                $keywords = if ($filter.Keywords) { ($filter.Keywords -join ", ") } else { "" }
                $entities = if ($filter.SupportedEntities) { ($filter.SupportedEntities -join ", ") } else { "all" }

                $summary = "$name is applicable for ``" +$entities + "`` entity"
                if ($desc) { $summary += ", description of this filter is as follows - ``" + $desc + "``" }
                if ($keywords) { $summary += " and $name is also known as ``" + $keywords + "``" }

                $filtersSummary[$name] = $summary
            }
        }

        # For KustoExecutor aspects, merge Parameters into filters
        if ($aspect.PluginType -eq "KustoExecutor" -and $aspect.Parameters) {
            foreach ($param in $aspect.Parameters) {
                $name = $param.Name
                $desc = $param.Description
                $keywords = if ($param.ValueEnums) { ($param.ValueEnums -join ", ") } else { "" }
                $entities = if ($aspect.SupportedEntityTypes) { ($aspect.SupportedEntityTypes -join ", ") } else { "all" }

                $summary = "$name is applicable for ``" + $entities + "`` entity"
                if ($desc) { $summary += ", description of this filter is as follows - ``" + $desc + "``" }
                if ($keywords) { $summary += " and $name is also known as ``" + $keywords + "``" }

                $filtersSummary[$name] = $summary
            }
        }

        $allAspects += [PSCustomObject]@{
            Name         = ($aspect.Name -replace '_', ' ')
            Description  = $aspect.Description
            Keywords     = @($aspect.Keywords)
            Domain       = $aspect.Domain
            OriginalName = $aspect.Name
            Filters      = $filtersSummary
        }
    }
}

# Build the KnowledgeGraph entries
$entries = @()
foreach ($a in $allAspects) {
    $entry = [PSCustomObject]@{
        Node = [PSCustomObject]@{
            Name         = $a.Name
            Descriptions = @(
                [PSCustomObject]@{
                    DescriptionType = "General"
                    Text            = $a.Description
                },
                [PSCustomObject]@{
                    DescriptionType = "System"
                    Text            = "there is an aspect with name ``" + $a.OriginalName + "`` which can be used by AspectSkill to fetch data"
                }
            )
            Tags = $a.Keywords
        }
        Filters       = $a.Filters
        Relationships = @()
    }
    $entries += $entry
}

# Write output
$json = $entries | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($outputFile, $json, [System.Text.Encoding]::UTF8)

Write-Host "KnowledgeGraph.json generated with $($entries.Count) nodes at $outputFile"

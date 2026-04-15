$useLocalAuth = $false # make it '$true' for local debugging

Write-Host "Trying to connect to Azure Account"
Write-Host "IdentityClientID": $env:IdentityClientId
Write-Host "Environment": $env:Environment
Write-Host "SubscriptionId": $env:SubscriptionId
Write-Host "TryForAllSubscriptions": $env:TryForAllSubscriptions
Write-Host "ResourceFileName": $env:ResourceFileName
Write-Host "RetryCount": $env:RetryCount

Import-Module Az.Websites

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$jsonFilePath = Join-Path $scriptDir $env:ResourceFileName #"deleteResources\sdpcleaningpayload.json"

function Authenticate-Azure {
    param([bool]$useLocalAuth)
    Write-Host "`n=== Authenticating to Azure ===" -ForegroundColor Cyan
    try {
        if ($useLocalAuth) {
            Write-Host "Using local authentication..." -ForegroundColor Yellow
            az login
            Write-Host "Successfully authenticated locally" -ForegroundColor Green
        } else {
            Write-Host "Using managed identity authentication..." -ForegroundColor Yellow
            if (-not $env:IdentityClientId) {
                Write-Error "Environment variable 'IdentityClientId' is not set. Required for managed identity authentication."
                exit 1
            }
            az login --identity --username $env:IdentityClientId
            Write-Host "Successfully authenticated using managed identity" -ForegroundColor Green
        }
        az account set --subscription $env:SubscriptionId
        Write-Host "Set subscription context to: $env:SubscriptionId (az CLI)" -ForegroundColor Green
    } catch {
        Write-Error "Failed to authenticate to Azure: $_"
        exit 1
    }
}

function Load-Config {
    param([string]$jsonFilePath)
    if (-not (Test-Path $jsonFilePath)) {
        Write-Error "JSON configuration file not found at: $jsonFilePath"
        exit 1
    }
    try {
        $config = Get-Content $jsonFilePath -Raw | ConvertFrom-Json
        Write-Host "Successfully loaded configuration from: $jsonFilePath" -ForegroundColor Green
        return $config
    } catch {
        Write-Error "Failed to parse JSON configuration: $_"
        exit 1
    }
}

function Remove-VNetIntegration {
    param($vnetintegration)
    Write-Host "`n--- VNet Integration Resources ---" -ForegroundColor Yellow
    if ($vnetintegration.Count -gt 0) {
        $retryCount = [int]$env:RetryCount
        if (-not $retryCount -or $retryCount -le 0) { $retryCount = 1 }
        $remainingItems = @($vnetintegration)
        $attempt = 1
        while ($remainingItems.Count -gt 0 -and $attempt -le $retryCount) {
            Write-Host ("Attempt $attempt to remove VNet integrations: " + ($remainingItems | ForEach-Object { ($_ -split "/")[-1] }) -join ", ") -ForegroundColor Yellow
            $failedItems = @()
            foreach ($entry in $remainingItems) {
                $resourceId = $entry
                Write-Host $resourceId -ForegroundColor White
                $parts = $resourceId -split "/"
                $resourceGroup = $parts[4]
                $provider = $parts[6]
                $type = $parts[7]
                $name = $parts[8]
                if ($provider -eq "Microsoft.Web" -and $type -eq "sites") {
                    Write-Host ("Removing VNet integration for Function App: " + $name + " in Resource Group: " + $resourceGroup) -ForegroundColor Yellow
                    try {
                        az functionapp vnet-integration remove --name $name --resource-group $resourceGroup
                        Write-Host ("Successfully removed VNet integration for " + $name) -ForegroundColor Green
                    } catch {
                        Write-Error ("Failed to remove VNet integration for " + $name + ". Error details: " + $_)
                        $failedItems += $entry
                    }
                } else {
                    Write-Host "VNet integration removal not supported for this resource type or missing private endpoint name." -ForegroundColor Red
                }
            }
            $remainingItems = $failedItems
            if ($remainingItems.Count -eq 0) {
                Write-Host "All VNet integrations removed successfully." -ForegroundColor Green
                break
            }
            $attempt++
        }
        if ($remainingItems.Count -gt 0) {
            Write-Host ("The following VNet integrations could not be removed after $retryCount attempts: " + ($remainingItems | ForEach-Object { ($_ -split "/")[-1] }) -join ", ") -ForegroundColor Red
        }
    } else {
        Write-Host "No VNet integration resources found" -ForegroundColor Gray
    }
}

function Purge-KeyVault {
    param($name, $location)
    try {
        if ($location) {
            Write-Host "Purging Key Vault $name in location $location" -ForegroundColor Yellow
            $purgeJob = Start-Job -ScriptBlock { param($n, $l) az keyvault purge --name $n --location $l 2>&1 } -ArgumentList $name, $location
            $purgeJob | Wait-Job -Timeout 1200 | Out-Null #20 mins timeout for purge
            if ($purgeJob.State -eq 'Completed') {
                $purgeOutput = Receive-Job $purgeJob
                if ($purgeOutput -and ($purgeOutput | Select-String -Pattern 'error|failed|not found|cannot' -SimpleMatch)) {
                    Write-Host "Purge output indicates failure for $name" -ForegroundColor Red
                    $purgeOutput | ForEach-Object { Write-Host $_ -ForegroundColor Red }
                } else {
                    Write-Host ("Successfully purged Key Vault " + $name) -ForegroundColor Green
                }
            } else {
                Write-Host ("Purge command timed out for " + $name + ". You may need to purge manually.") -ForegroundColor Red
                Stop-Job $purgeJob | Out-Null
            }
            Remove-Job $purgeJob | Out-Null
        } else {
            Write-Host ("Could not fetch location for Key Vault " + $name + ". Skipping purge.") -ForegroundColor Red
        }
    } catch {
        Write-Host ("Error purging Key Vault " + $name + ". You may need to purge manually.") -ForegroundColor Red
    }
}

function Test-And-DeleteResource {
    param(
        [string]$checkCmd,
        [string]$deleteCmd,
        [string]$resourceType,
        [string]$resourceName
    )
    $exists = $false
    $notFoundMsg = $null
    # try {
    #     $output = Invoke-Expression $checkCmd
    #     if ($output) { $exists = $true }
    # } catch {}
    # if ($exists) {
        Write-Host ("Deleting " + $resourceType + ": " + $resourceName) -ForegroundColor Yellow
        $deleteResult = $null
        try {
            $deleteResult = Invoke-Expression $deleteCmd
            if ($LASTEXITCODE -eq 0) {
                Write-Host ("Successfully deleted " + $resourceType + ": " + $resourceName) -ForegroundColor Green
            } else {
                # Print only the relevant error output from the delete command
                if ($deleteResult) {
                    $lines = $deleteResult -split "\r?\n"
                    $infoLines = $lines | Where-Object { $_ -match "^INFO:|^Code:|^Message:" }
                    if ($infoLines) {
                        $infoLines | ForEach-Object { Write-Host $_ }
                    } else {
                        Write-Host $deleteResult
                    }
                } else {
                    Write-Host "Delete command failed for ${resourceType}: ${resourceName}. No output returned."
                }
            }
        } catch {
            throw "Delete unsuccessful for ${resourceType}: ${resourceName}. Exception: $_"
        }
    # } else {
    #     Write-Host ($resourceType + " " + $resourceName + " does not exist. Skipping deletion.") -ForegroundColor Cyan
    # }
}

function Remove-GeneralResources {
    param($resources)
    Write-Host "`n--- General Resources ---" -ForegroundColor Yellow
    if ($resources.Count -gt 0) {
        $retryCount = [int]$env:RetryCount
        if (-not $retryCount -or $retryCount -le 0) { $retryCount = 1 }
        $remainingResources = @($resources)
        $attempt = 1
        while ($remainingResources.Count -gt 0 -and $attempt -le $retryCount) {
            Write-Host ("Attempt $attempt to remove general resources: " + ($remainingResources | ForEach-Object { ($_ -split "/")[-1] }) -join ", ") -ForegroundColor Yellow
            $failedResources = @()
            $vnets = @()
            $subnets = @()
            $nsgs = @()
            $others = @()
            foreach ($resourceId in $remainingResources) {
                $parts = $resourceId -split "/"
                $provider = $parts[6]
                $type = $parts[7]
                $name = $parts[8]
                if ($provider -eq "Microsoft.Network" -and $type -eq "virtualNetworks") {
                    $vnets += $resourceId
                } elseif ($provider -eq "Microsoft.Network" -and $type -eq "networkSecurityGroups") {
                    $nsgs += $resourceId
                } elseif ($provider -eq "Microsoft.Network" -and $type -eq "subnets") {
                    $subnets += $resourceId
                } else {
                    $others += $resourceId
                }
            }

            # Delete resources in subnets (NICs, VMs, etc.) and remove NSG association
            foreach ($resourceId in $subnets) {
                Write-Host $resourceId -ForegroundColor White
                $parts = $resourceId -split "/"
                $resourceGroup = $parts[4]
                $vnetName = $parts[8]
                $subnetName = $parts[10]
                Write-Host "Removing NSG association from subnet $subnetName in VNet $vnetName" -ForegroundColor Yellow
                $removeNsgCmd = "az network vnet subnet update --resource-group $resourceGroup --vnet-name $vnetName --name $subnetName --network-security-group ''"
                try {
                    Invoke-Expression $removeNsgCmd
                } catch {
                    Write-Error "Failed to remove NSG association from subnet $subnetName in VNet $vnetName. Error details: $_"
                    $failedResources += $resourceId
                    continue
                }

                $subnetId = "/subscriptions/$($parts[2])/resourceGroups/$resourceGroup/providers/Microsoft.Network/virtualNetworks/$vnetName/subnets/$subnetName"
                $nicListCmd = "az network nic list --resource-group $resourceGroup --query '[?ipConfigurations[].subnet.id==\'$subnetId\']'"
                $nicList = Invoke-Expression $nicListCmd | ConvertFrom-Json
                if ($nicList) {
                    foreach ($nic in $nicList) {
                        $nicName = $nic.name
                        Write-Host "Deleting NIC $nicName in subnet $subnetName" -ForegroundColor Yellow
                        $nicDeleteCmd = "az network nic delete --name $nicName --resource-group $resourceGroup"
                        try {
                            Invoke-Expression $nicDeleteCmd
                        } catch {
                            Write-Error "Failed to delete NIC $nicName in subnet $subnetName. Error details: $_"
                            $failedResources += $resourceId
                        }
                    }
                }
                Write-Host "Deleting subnet $subnetName in VNet $vnetName" -ForegroundColor Yellow
                $subnetDeleteCmd = "az network vnet subnet delete --name $subnetName --vnet-name $vnetName --resource-group $resourceGroup"
                try {
                    Invoke-Expression $subnetDeleteCmd
                } catch {
                    Write-Error "Failed to delete subnet $subnetName in VNet $vnetName. Error details: $_"
                    $failedResources += $resourceId
                }
            }

            # Delete NSGs
            foreach ($resourceId in $nsgs) {
                Write-Host $resourceId -ForegroundColor White
                $parts = $resourceId -split "/"
                $resourceGroup = $parts[4]
                $nsgName = $parts[8]
                $typeKey = "Microsoft.Network/networkSecurityGroups"
                $quotedName = '"' + $nsgName + '"'
                $checkCmd = "az network nsg show --name $nsgName --resource-group $resourceGroup"
                $deleteCmd = "az network nsg delete --name $nsgName --resource-group $resourceGroup"
                try {
                    Test-And-DeleteResource $checkCmd $deleteCmd $typeKey $nsgName
                } catch {
                    Write-Error "Failed to delete NSG $nsgName. Error details: $_"
                    $failedResources += $resourceId
                }
            }

            # Delete VNets
            foreach ($resourceId in $vnets) {
                Write-Host $resourceId -ForegroundColor White
                $parts = $resourceId -split "/"
                $resourceGroup = $parts[4]
                $vnetName = $parts[8]
                $typeKey = "Microsoft.Network/virtualNetworks"
                $quotedName = '"' + $vnetName + '"'
                $checkCmd = "az network vnet show --name $vnetName --resource-group $resourceGroup"
                $deleteCmd = "az network vnet delete --name $vnetName --resource-group $resourceGroup"
                try {
                    Test-And-DeleteResource $checkCmd $deleteCmd $typeKey $vnetName
                } catch {
                    Write-Error "Failed to delete VNet $vnetName. Error details: $_"
                    $failedResources += $resourceId
                }
            }

            # Delete all other resources except network dependencies
            foreach ($resourceId in $others) {
                Write-Host $resourceId -ForegroundColor White
                $parts = $resourceId -split "/"
                $resourceGroup = $parts[4]
                $provider = $parts[6]
                $type = $parts[7]
                $name = $parts[8]
                $typeKey = "$provider/$type"
                $quotedName = '"' + $name + '"'
                $checkCmd = "az resource show --name $quotedName --resource-type $typeKey --resource-group $resourceGroup"
                $deleteCmd = "az resource delete --name $quotedName --resource-type $typeKey --resource-group $resourceGroup --verbose"

                # Special handling for Service Bus namespaces with GeoDR
                if ($typeKey -eq "Microsoft.ServiceBus/namespaces") {
                    try{
                        $aliasList = az servicebus georecovery-alias list --resource-group $resourceGroup --namespace-name $name | ConvertFrom-Json
                        if ($aliasList) {
                            foreach ($aliasObj in $aliasList) {
                                $aliasName = $aliasObj.name
                                # Extract primary and secondary namespace using role and partnerNamespace
                                $primaryNs = $null
                                $secondaryNs = $null
                                if ($aliasObj.role -eq "Primary") {
                                    $primaryNs = ($aliasObj.id -split "/")[-3] # namespace name from alias id
                                    $secondaryNs = ($aliasObj.partnerNamespace -split "/")[-1] # namespace name from partnerNamespace ARM ID
                                } elseif ($aliasObj.role -eq "Secondary") {
                                    $secondaryNs = ($aliasObj.id -split "/")[-3]
                                    $primaryNs = ($aliasObj.partnerNamespace -split "/")[-1]
                                }
                                Write-Host "Processing alias: $aliasName (primary: $primaryNs, secondary: $secondaryNs)" -ForegroundColor Yellow
                                if ($primaryNs -eq $name -or $secondaryNs -eq $name) {
                                    Write-Host "Breaking GeoDR pairing for alias: $aliasName" -ForegroundColor Yellow
                                    # The correct namespace for break-pair is the one where the alias is defined (current $name)
                                    if ($name) {
                                        az servicebus georecovery-alias break-pair --resource-group $resourceGroup --namespace-name $name --alias $aliasName
                                    } else {
                                        throw "Cannot break GeoDR pairing: namespace name is not available for alias $aliasName"
                                    }
                                    try{
                                        Write-Host "Deleting GeoDR alias: $aliasName" -ForegroundColor Yellow
                                        # The correct namespace for delete is the one where the alias is defined
                                        $deleteNs = $null
                                        if ($aliasObj.role -eq "Primary") {
                                            $deleteNs = $primaryNs
                                        } elseif ($aliasObj.role -eq "Secondary") {
                                            $deleteNs = $secondaryNs
                                        }
                                        if ($deleteNs) {
                                            az servicebus georecovery-alias delete --resource-group $resourceGroup --namespace-name $deleteNs --alias $aliasName
                                        } else {
                                            throw "Cannot delete GeoDR alias: namespace name is not available for alias $aliasName"
                                        }
                                    } catch {
                                        throw "Failed to delete GeoDR alias $aliasName. Error details: $_"
                                    }
                                }
                            }
                        }
                        az servicebus namespace delete --resource-group $resourceGroup --name $name
                        Write-Host "Service Bus namespace $name deleted." -ForegroundColor Green
                    } catch {
                        Write-Error "Failed to delete Service Bus namespace $name. Error details: $_"
                        $failedResources += $resourceId
                    }
                    continue
                }

                try {
                    Test-And-DeleteResource $checkCmd $deleteCmd $typeKey $name
                } catch {
                    Write-Error "Failed to delete resource $name of type $typeKey. Error details: $_"
                    $failedResources += $resourceId
                }
                if ($typeKey -eq "Microsoft.KeyVault/vaults") {
                    $location = $null
                    $deletedVaultInfo = az keyvault show-deleted --name $name 2>$null | ConvertFrom-Json
                    if ($deletedVaultInfo) {
                        $location = $deletedVaultInfo.properties.location
                        Purge-KeyVault $name $location
                    }
                }
            }

            # Only retry failed resources in the next attempt
            $remainingResources = @($failedResources)
            if ($remainingResources.Count -eq 0) {
                Write-Host "All general resources removed successfully." -ForegroundColor Green
                break
            }
            $attempt++
        }
        if ($remainingResources.Count -gt 0) {
            Write-Host ("The following general resources could not be removed after $retryCount attempts: " + ($remainingResources | ForEach-Object { ($_ -split "/")[-1] }) -join ", ") -ForegroundColor Red
        }
    } else {
        Write-Host "No general resources found" -ForegroundColor Gray
    }
}

function Remove-ResourceGroups {
    param($resourcegroups)
    Write-Host "`n--- Resource Groups ---" -ForegroundColor Yellow
    if ($resourcegroups.Count -gt 0) {
        $retryCount = [int]$env:RetryCount
        if (-not $retryCount -or $retryCount -le 0) { $retryCount = 1 }
        $remainingGroups = @($resourcegroups)
        $attempt = 1
        while ($remainingGroups.Count -gt 0 -and $attempt -le $retryCount) {
            Write-Host ("Attempt $attempt to delete resource groups: " + ($remainingGroups | ForEach-Object { ($_ -split "/")[-1] }) -join ", ") -ForegroundColor Yellow
            $failedGroups = @()
            foreach ($resourceId in $remainingGroups) {
                $resourceGroupName = ($resourceId -split "/")[-1]
                Write-Host ("Deleting " + $resourceGroupName) -ForegroundColor Yellow
                try {
                    az group delete --name $resourceGroupName --yes --verbose | Out-Null
                    # Wait and check if the resource group still exists
                    $exists = az group exists --name $resourceGroupName | ConvertFrom-Json
                    if (-not $exists) {
                        Write-Host ("Successfully deleted " + $resourceGroupName) -ForegroundColor Green
                    } else {
                        Write-Host ("Resource group " + $resourceGroupName + " still exists after delete attempt.") -ForegroundColor Red
                        $failedGroups += $resourceId
                    }
                } catch {
                    Write-Error ("Failed to delete " + $resourceGroupName + ". Error details: " + $_) -ForegroundColor Red
                    $failedGroups += $resourceId
                }
            }
            $remainingGroups = $failedGroups
            if ($remainingGroups.Count -eq 0) {
                Write-Host "All resource groups deleted successfully." -ForegroundColor Green
                break
            }
            $attempt++
        }
        if ($remainingGroups.Count -gt 0) {
            Write-Host ("The following resource groups could not be deleted after $retryCount attempts: " + ($remainingGroups | ForEach-Object { ($_ -split "/")[-1] }) -join ", ") -ForegroundColor Red
        }
    } else {
        Write-Host "No resource groups found" -ForegroundColor Gray
    }
}

# Main script logic
Authenticate-Azure $useLocalAuth
$config = Load-Config $jsonFilePath

# Validate environment and subscription
if (-not $config.$env:Environment) {
    Write-Error "Environment '$env:Environment' not found in configuration"
    Write-Host "Available environments: $($config.PSObject.Properties.Name -join ', ')" -ForegroundColor Yellow
    exit 1
}
if (-not $config.$env:Environment.$env:SubscriptionId) {
    Write-Error "Subscription ID '$env:SubscriptionId' not found in environment '$env:Environment'"
    Write-Host "Available subscription IDs for '$env:Environment': $($config.$env:Environment.PSObject.Properties.Name -join ', ')" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n=== Starting Resource Cleanup ===" -ForegroundColor Cyan
# If TryForAllSubscriptions is true, run for all subscriptions in the environment
if ($env:TryForAllSubscriptions -eq $true -or $env:TryForAllSubscriptions -eq "true") {
    Write-Host "\n=== Running resource cleanup for ALL subscriptions in environment: $env:Environment ===" -ForegroundColor Cyan
    $envSubscriptions = $config.$env:Environment.PSObject.Properties.Name
    foreach ($subId in $envSubscriptions) {
        Write-Host "\n--- Subscription: $subId ---" -ForegroundColor Yellow
        # Set subscription context for each subscription
        az account set --subscription $subId
        Write-Host "Set subscription context to: $subId (az CLI)" -ForegroundColor Green
        $subscriptionConfig = $config.$env:Environment.$subId
        Remove-VNetIntegration $subscriptionConfig.vnetintegration
        Remove-GeneralResources $subscriptionConfig.resources
        Remove-ResourceGroups $subscriptionConfig.resourcegroup
    }
} else {
    $subscriptionConfig = $config.$env:Environment.$env:SubscriptionId
    Remove-VNetIntegration $subscriptionConfig.vnetintegration
    Remove-GeneralResources $subscriptionConfig.resources
    Remove-ResourceGroups $subscriptionConfig.resourcegroup
}

Write-Host "`n=== End of Resource List ===" -ForegroundColor Cyan
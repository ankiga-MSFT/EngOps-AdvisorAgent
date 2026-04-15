###################################OVERVIEW####################################
# Script: CreateSearchIndex.ps1
# Purpose: Creates or updates an Azure Cognitive Search index with specified schema and configurations
# 

############################################################################

################# Instructions for Local Debugging################################
#
# To set environment variables for local debugging, use the following commands in PowerShell:
#
#  $env:SubscriptionId = "fa5349cd-ab3c-4232-88e5-8b842489a230"
#  $env:searchServiceNames = @('srch-icmglobal-test-ccan', 'srch-icmglobal-test-ecan') 
#  $env:indexNames =@('srsnapshot')
#  $env:indexDefinitionPath = './Config/SearchIndex'
#  $env:IdentityClientId = "170320f6-b396-401b-b7be-1048cb5bab4e"

# After setting the environment variables, run the script using:
# .\CreateSearchIndex.ps1
####################################################################################



# Hardcoded values for AllowIndexDowntime 
$AllowIndexDowntime = "false"


# Read values from environment variables
$SubscriptionId = $env:SubscriptionId
$SearchServiceNames =  ($env:searchServiceNames -split ",").ForEach({ $_.Trim() })
$IndexNames =  ($env:indexNames -split ",").ForEach({ $_.Trim() })
$IndexDefinitionPath = $env:indexRootDefinitionPath
$IdentityClientId = $env:IdentityClientId


# Convert string parameters to lowercase for comparison
$AllowIndexDowntime = $AllowIndexDowntime.ToLower()


# Initialize exit code
$exitCode = 0

# Constants
$ApiVersion = "2024-07-01"  # Updated to latest API version

function Connect-And-Get-AccessToken {
    param(
        [string] $IdentityClientId,
        [string] $SubscriptionId,
        [string] $SearchResourceName
    )

    try {
        $ResourceUrl = 'https://search.azure.com'
        #$ResourceUrl = "https://$SearchResourceName.search.windows.net" # Replace with your Azure Search resource's URL
        Write-Host "ResourceUrl: $ResourceUrl" -ForegroundColor Yellow
        if ([string]::IsNullOrEmpty($IdentityClientId)) {
            Write-Host "Using logged-in user credentials for authentication..."
            $context = Get-AzContext -ErrorAction SilentlyContinue

            if ($null -eq $context) {
                Write-Host "No Azure context found. Please run Connect-AzAccount before running this script locally." -ForegroundColor Yellow
                return $null
            }
            if ($SubscriptionId -and $context.Subscription.Id -ne $SubscriptionId) {
                Write-Host "Setting subscription context to: $SubscriptionId"
                Set-AzContext -SubscriptionId $SubscriptionId -ErrorAction Stop | Out-Null
            }
            Write-Host "Acquiring access token using logged-in user credentials..."
            $token = (Get-AzAccessToken -ResourceUrl $ResourceUrl).Token
            Write-Host "Successfully acquired access token using logged-in user credentials" -ForegroundColor Green
        } else {
            # Write-Host "Using Managed Identity for authentication..."
            Connect-AzAccount -Identity -AccountId $IdentityClientId -SubscriptionId $SubscriptionId -ErrorAction Stop | Out-Null
            Write-Host "Acquiring access token using Managed Identity..."
            $token = (Get-AzAccessToken -ResourceUrl $ResourceUrl).Token
            Write-Host "Access Token: $($token)" -ForegroundColor Yellow
            Write-Host "Successfully acquired access token using Managed Identity" -ForegroundColor Green
            # # Variables

            # # Get the Azure AD access token
            # $token = Get-AzAccessToken -AccountId $IdentityClientId -ResourceUrl $ResourceUrl

            # # Output the token and expiration time
            # Write-Host "Access Token: $($token.Token)"
            # Write-Host "Expires On: $($token.ExpiresOn)"
        }
        return $token
    } catch {
        Write-Host "Failed to connect or acquire access token: $_" -ForegroundColor Red
        return $null
    }
}

function Create-Or-Update-SearchIndex {
    param(
        [string] $SearchServiceName,
        [string] $IndexName,
        [string] $IndexDefinitionPath,
        [string] $AccessToken,
        [bool] $AllowDowntime
    )

    try {
        Write-Host "Loading index definition from $IndexDefinitionPath..."
        $IndexDefinitionPath="$IndexDefinitionPath\$IndexName.json"
        if (-not (Test-Path $IndexDefinitionPath)) {
            Write-Host "Index definition file not found at path: $IndexDefinitionPath" -ForegroundColor Red
            return $false
        }
        $indexDefinition = Get-Content $IndexDefinitionPath -Raw
        $allowDowntimeValue = $AllowDowntime.ToString().ToLower()
        $uri = "https://$SearchServiceName.search.windows.net/indexes('$IndexName')?api-version=$ApiVersion&allowIndexDowntime=$allowDowntimeValue"
        $headers = @{
            "Authorization" = "Bearer $AccessToken"
            "Content-Type" = "application/json"
        }
        Write-Host "Creating or updating index $IndexName (allowIndexDowntime=$AllowDowntime)" -ForegroundColor Yellow
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Put -Body $indexDefinition
        if ($AllowDowntime) {
            Write-Host "Index $IndexName created/updated with allowIndexDowntime=true. Note that this may have caused temporary service disruption." -ForegroundColor Green
        } else {
            Write-Host "Index $IndexName created/updated with allowIndexDowntime=false (safe update mode)." -ForegroundColor Green
        }
        return $true
    } catch {
        Write-Host "Failed to create/update index $IndexName $_" -ForegroundColor Red
        return $false
    }
}

# Main script execution
try {
    Write-Host "==========Azure Search Index Creation/Update Started==========" -ForegroundColor Cyan
    Write-Host "SubscriptionId: $SubscriptionId"
    Write-Host "SearchServiceNames: $SearchServiceNames"
    Write-Host "IndexName: $IndexName"
    Write-Host "IndexDefinitionPath: $IndexDefinitionPath"
    Write-Host "AllowIndexDowntime: $AllowIndexDowntime"
    Write-Host "API Version: $ApiVersion"

    # Get access token
    

    # Create or update the index for each search service
    foreach ($SearchServiceName in $SearchServiceNames) {
        foreach($IndexName in $IndexNames) {
            $accessToken = Connect-And-Get-AccessToken -IdentityClientId $IdentityClientId -SubscriptionId $SubscriptionId -SearchResourceName $SearchServiceName
            if ($null -eq $accessToken) {
                throw "Failed to acquire access token. Exiting."
            }
            Write-Host "Processing Search Service: $SearchServiceName" -ForegroundColor Cyan
            $success = Create-Or-Update-SearchIndex -SearchServiceName $SearchServiceName -IndexName $IndexName `
                                                -IndexDefinitionPath $IndexDefinitionPath -AccessToken $accessToken `
                                                -AllowDowntime ($AllowIndexDowntime -eq "true")
                                                
            if (-not $success) {
                Write-Host "Failed to create or update index for Search Service: $SearchServiceName" -ForegroundColor Red
            } else {
                Write-Host "Index operation completed successfully for Search Service: $SearchServiceName" -ForegroundColor Green
            }
        }
    }
}
catch {
    Write-Host "An error occurred: $_" -ForegroundColor Red
    $exitCode = 1
}
finally {
    Write-Host "==========Azure Search Index Creation/Update Completed==========" -ForegroundColor Cyan
    exit $exitCode
}



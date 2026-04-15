Write-Host "==========Azure functions artifact blob file upload Started!=========="

# Initialize exit code
$exitCode = 0

try {
    Import-Module Az
    Write-Host "Trying to connect to Azure Account"
    Write-Host "IdentityClientID" + $env:IdentityClientId
    Write-Host "SubscriptionId" + $env:SubscriptionId
    Write-Host "StorageName" + $env:StorageName
	Write-Host "Storage RGName" + $env:resourceGroupName
    Write-Host "ContainerName" + $env:ContainerName
    Write-Host "File to upload" + $env:FunctionAppArtifactZipFileName
    Write-Host "Blob File Name" + $env:FunctionAppArtifactBlobFileName
    Write-Host "Environment Name" + $env:EnvironmentName
    Write-Host "Current Application Subscription" + $env:CurrentApplicationSubscription

    $environmentSettingFolder=".\EnvironmentSettings"
     # Get the current directory
    $currentDirectory = Get-Location

    # Print the current directory
    Write-Host "Current Directory: $currentDirectory"

    # List all files and folders in the current directory
    Get-ChildItem -Path $currentDirectory -Recurse

    Write-Host "####################################################################"
    Connect-AzAccount -Identity -AccountId $env:IdentityClientId -SubscriptionId $env:CurrentApplicationSubscription -ErrorAction Stop

    try {
        $stmaccountexist = Get-AzStorageAccount -ResourceGroupName $env:resourceGroupName -Name $env:StorageName -ErrorAction Stop
        Write-Host "Storage account '$env:StorageName' exists in resource group '$env:resourceGroupName'."
    } catch {
        Write-Host "Storage account '$env:StorageName' does NOT exist in resource group '$env:resourceGroupName'."
        return
    }




    Write-Host "Connected to Azure Account"

	#################################### SDP SPECIFIC CHANGES FOR NETWORK ISOLATION###############################
	 Write-Host "--------------Running SDP Specific Changes Regarding Network isolation----------------------------"
	 Set-AzStorageAccount -ResourceGroupName  $env:resourceGroupName -Name $env:StorageName -PublicNetworkAccess "Enabled" -NetworkRuleSet @{
		DefaultAction = "Allow"
	 }
	  Write-Host "Set DefaultAction to Allow in NetworkRuleSet of StorageAccount"
	 Write-Host "--------------Ran SDP Specific Changes Regarding Network isolation----------------------------"
	 Start-Sleep -Seconds 120
	 ###############################################################################################################
    $storageContext = New-AzStorageContext -StorageAccountName $env:StorageName -UseConnectedAccount
    Write-Host "Connected to Storage account"

    # Check if the container exists
    $container = Get-AzStorageContainer -Name $env:ContainerName -Context $storageContext -ErrorAction SilentlyContinue

    # Create the container if it does not exist
    if ($null -eq $container) {
        New-AzStorageContainer -Name $env:ContainerName -Context $storageContext -Permission Off -ErrorAction Stop
        Write-Host "Container " + $env:ContainerName + " created successfully."
    } else {
        Write-Host "Container " + $env:ContainerName + " already exists."
    }

    Write-Host 'Uploading to blob storage, file: ' + $env:FunctionAppArtifactZipFileName

    # upload a file to the default account (inferred) access tier
    $Blob1HT = @{
      File             = $env:FunctionAppArtifactZipFileName # 'D:\App\FunctionApp.zip'
      Container        = $env:ContainerName # $ContainerName
      Blob             = $env:FunctionAppArtifactBlobFileName # "FunctionApp.zip"
      Context          = $storageContext 
      StandardBlobTier = 'Hot'
    }
    Set-AzStorageBlobContent @Blob1HT -Force -ErrorAction Stop
    # upload config files
    if (-not [string]::IsNullOrEmpty($env:EnvironmentName)) {
        $EnvironmentSettingFileName=$env:EnvironmentName+".environment.settings.json"
        $folderName = $env:FunctionAppArtifactZipFileName.Split(".")[0]
        $EnvironmentSettingFilePath = ".\EnvironmentSettings-$folderName\$EnvironmentSettingFileName"

        if (Test-Path -Path $EnvironmentSettingFilePath) {
            Write-Host "Environment file '$EnvironmentSettingFile' was found, uploading the file"
             $Blob2HT = @{
              File             = $EnvironmentSettingFilePath # 'D:\App\FunctionApp.zip'
              Container        = $env:ContainerName # $ContainerName
              Blob             = $EnvironmentSettingFileName # "FunctionApp.zip"
              Context          = $storageContext 
              StandardBlobTier = 'Hot'
            }
            Set-AzStorageBlobContent @Blob2HT -Force -ErrorAction Stop
            Write-Host "Environment file '$EnvironmentSettingFile' was uploaded"
        } else {
            Write-Host "Environment file '$EnvironmentSettingFileName' was not found"
        }
    }

    Write-Host 'Uploaded to blob storage, file: ' + $env:FunctionAppArtifactZipFileName
    Write-Host "####################################################################"
    Write-Host "==========Azure functions artifact blob file upload Complete!=========="
	#################################### SDP SPECIFIC CHANGES FOR NETWORK ISOLATION###############################
    Write-Host "--------------Running SDP Specific Changes Regarding Network isolation----------------------------"
    Set-AzStorageAccount -ResourceGroupName  $env:resourceGroupName -Name $env:StorageName -PublicNetworkAccess "Disabled" -NetworkRuleSet @{
    DefaultAction = "Deny"
    }
    Write-Host "Set DefaultAction to Deny in NetworkRuleSet of StorageAccount"
    Write-Host "--------------Ran SDP Specific Changes Regarding Network isolation----------------------------"
    ###############################################################################################################
}
catch {
    # Handle Error
	Write-Host "Error Occurred while trying Upload Artifact : $_"

    # Set exit code to 1 to indicate failure
	$exitCode = 1
} finally {
    # Return the exit code
    exit $exitCode
}
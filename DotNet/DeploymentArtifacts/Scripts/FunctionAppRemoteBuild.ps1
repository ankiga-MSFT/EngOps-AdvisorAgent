#$env:identityClientId ="1f339cee-c1f3-420d-b01d-db17c7a21f4c"
#$env:resourceGroupName= "rg-piiscrubber-ppe-westus3"
#$env:functionName ="fun-piiscrubber-ppe-westus3"
#$env:blobFileName ="PiiScrubber.zip"
#$env:functionSlotName ="staging"


Write-Host "==========Azure functions remote build Started!=========="

# Initialize exit code
$exitCode = 0
try {
	Write-Host "Trying to connect to Azure Account"
	Write-Host "IdentityClientID" + $env:identityClientId
	Write-Host "Storage RGName" + $env:resourceGroupName
	Write-Host "FunctionName" + $env:functionName
	Write-Host "BlobFileName" + $env:blobFileName
	Write-Host "SlotName": + $env:functionSlotName
    Write-Host "Current Application Subscription" + $env:CurrentApplicationSubscription

    #Get the current directory
    
    $currentDirectory = Get-Location

    # Print the current directory
    Write-Host "Current Directory: $currentDirectory"

    # List all files and folders in the current directory
    Get-ChildItem -Path $currentDirectory -Recurse
	
	Write-Host "##########################CONNECTING TO AZURE##########################################"
	az login --identity --username $env:identityClientId
	az account set --subscription $env:CurrentApplicationSubscription
	Write-Host "##########################CONNECTED  TO AZURE##########################################"
	
	Write-Host "#############UPLOADING ZIP FILE AND RUNNING REMOTE BUILD###############################"
	az functionapp deployment source config-zip -g $env:resourceGroupName --slot $env:functionSlotName -n $env:functionName --src $env:blobFileName --build-remote true --verbose
	Write-Host "#############UPLOADED ZIP FILE AND REMOTE BUILD SUCCEDED###############################"
	
	Write-Host "########################FETCHING REMOTE BUILD LOGS#####################################"
	az webapp log deployment show --name $env:functionName --resource-group $env:resourceGroupName
	Write-Host "########################DEPLOYMENT COMPLETED###########################################"
	}
catch 
    {
		# Handle Error
	Write-Host "Error Occurred while trying Upload Artifact : $_"
		throw $_
		# Set exit code to 1 to indicate failure
		$exitCode = 1
    } 
finally {
		# Return the exit code
		exit $exitCode
}	

#####Command for local testing###########
# pwsh FunctionAppRemoteBuild.ps1 -identityClientId "f2bbfa2b-6cd4-4224-ba48-c079130671b4" -resourceGroupName "rg-piiscrubber-ppe-eastus" -functionName "fun-piiscrubber-ppe-eastus" -blobFileName "PiiScrubber.zip" -functionSlotName "staging"
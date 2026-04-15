#$env:identityClientId ="6f8b246d-d690-4f9f-89fe-3e9e819eb435"
#$env:resourceGroupName= "rg-nrtglobal-test-eastus"
#$env:keyvaultName ="kv-nrtglbev2-test-eastus"
#$env:publicNetworkAccessState = "Disabled"
#$env:SubscriptionId = "fa5349cd-ab3c-4232-88e5-8b842489a230"


Write-Host "==========Keyvault public access state change started=========="

# Initialize exit code
$exitCode = 0
try {
	Write-Host "Trying to connect to Azure Account"
	Write-Host "IdentityClientID" + $env:identityClientId
	Write-Host "Keyvault RGName" + $env:resourceGroupName
	Write-Host "KeyVaultName" + $env:keyvaultName
	Write-Host "publicNetworkAccessState" + $env:publicNetworkAccessState
	Write-Host "SubscriptionId" + $env:SubscriptionId
    #Get the current directory
    
    $currentDirectory = Get-Location

    # Print the current directory
    Write-Host "Current Directory: $currentDirectory"

    # List all files and folders in the current directory
    Get-ChildItem -Path $currentDirectory -Recurse
	
	Write-Host "##########################CONNECTING TO AZURE##########################################"
	Connect-AzAccount -Identity -AccountId $env:identityClientId -SubscriptionId $env:SubscriptionId -ErrorAction Stop
	Write-Host "##########################CONNECTED  TO AZURE##########################################"
	
	Write-Host "#############Setting Keyvault publicNetworkAcess to $env:publicNetworkAccessState state###############################"
	Update-AzKeyVault -ResourceGroupName $env:resourceGroupName -VaultName $env:keyvaultName -PublicNetworkAccess  $env:publicNetworkAccessState
	Update-AzKeyVaultNetworkRuleSet -ResourceGroupName $env:resourceGroupName -VaultName $env:keyvaultName -Bypass AzureServices -DefaultAction Allow
	Start-Sleep -Seconds 60
	Write-Host "#############Setted Keyvault publicNetworkAcess to $env:publicNetworkAccessState state###############################"
	
	
	}
catch 
    {
		# Handle Error
	Write-Host "Error Occurred while trying to set keyvault public access : $_"
		throw $_
		# Set exit code to 1 to indicate failure
		$exitCode = 1
    } 
finally {
		# Return the exit code
		exit $exitCode
}	

#####Command for local testing###########
# $env:identityClientId= '6f8b246d-d690-4f9f-89fe-3e9e819eb435'
# $env:resourceGroupName= 'rg-nrtglobal-test-eastus'
# $env:keyvaultName= 'kv-nrtglbev2-test-eastus'
# $env:publicNetworkAccessState= 'Disabled '
# $env:SubscriptionId='fa5349cd-ab3c-4232-88e5-8b842489a230'

# .\KeyVaultPublicAccessChange.ps1



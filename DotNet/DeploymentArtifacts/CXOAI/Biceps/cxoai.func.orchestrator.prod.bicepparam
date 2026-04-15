using 'cxoai.func.orchestrator.bicep'

// replace ccan, canadacentral of global rg

var actuallocation='$location()'
var location = 'dynamiclocation'
var config_deploycxoaiinfra = 'false'
var env = 'prod'
var appname = 'cxoai'
var globalappname = 'caiglobal'
var globallocation = 'canadacentral'
var globallocationalias = 'ccan'
var secondarylocationalias = 'ecan'
var cloudtype = 'public'
var subscriptionId = '443efde9-a0c0-4d0e-8f52-63cdcd9e0931'
var config_failcxoaideployment = 'false'
param PfailDeployment =  '${config_failcxoaideployment}'
param CurrentApplicationSubscription = subscriptionId
param PdeployInfra= '${config_deploycxoaiinfra}'
var PservicePlanName = 'asp-${appname}-${env}-${location}'
var PfunctionName = 'fun-${appname}-${env}-${location}'
var PvnetName = 'vnet-${appname}-${env}-${location}'
var PnsgName = 'nsg-${appname}-${env}-${location}'
var PmuiName = 'mui-${appname}-${env}-${location}'
var PservicePlanSkuName = 'P2MV3'
var PservicePlanSkuTier = 'PremiumMV3'
var PservicePlanSkuCapacity = '1'
var PappInsightName = 'ai-${globalappname}-${env}-${globallocationalias}'
var PappInsightsResourceGroupName = 'rg-${globalappname}-${env}-${globallocation}'

var PstorageAccountResourceId = '/subscriptions/${subscriptionId}/resourceGroups/rg-${appname}-${env}-${actuallocation}/providers/Microsoft.Storage/storageAccounts/stm${appname}${env}${location}'

var PKeyvaultResourceId = '/subscriptions/${subscriptionId}/resourceGroups/rg-${globalappname}-${env}-${globallocation}/providers/Microsoft.KeyVault/vaults/kv-${globalappname}-${env}-${globallocationalias}'
var PstorageAccountName = 'stm${appname}${env}${location}'

var PcomputeRegionOverride = 'none'
var PgenevaCertSecretName = 'sdp-geneva-auth-certificate'
var PmonitoringConfigVersion = '1.3'
var PmonitoringGcsAccount = 'AzCXPSDP'
var PmonitoringGcsAuthId = '${env}.geneva.keyvault.cxpes.microsoft.com'
var PmonitoringGcsEnvironment = 'Diagnostics Prod'
var PmonitoringGcsNamespace = 'SDPProd'
var PmonitoringTenant = 'sdp-${cloudtype}-${env}'

var PpublicIpName = 'pip-tag-${appname}-${env}-${location}'
var PnatGatewayName = 'nat-${appname}-${env}-${location}'
var PnumberOfPublicIPs = '3'
//replace later to service tags
var PoutboundServiceTag= [{
                            ipTagType: 'FirstPartyUsage'
                            tag: '/AzureCXPSDPCXDPPROD'
                          }]

param PcreateCXOAIInfraInput={
PservicePlanName: PservicePlanName
PservicePlanSkuName: PservicePlanSkuName
PservicePlanSkuTier: PservicePlanSkuTier
PservicePlanSkuCapacity: PservicePlanSkuCapacity
PgenevaCertSecretName: PgenevaCertSecretName
PmonitoringConfigVersion: PmonitoringConfigVersion
PmonitoringGcsAccount: PmonitoringGcsAccount
PmonitoringGcsAuthId: PmonitoringGcsAuthId
PmonitoringGcsEnvironment: PmonitoringGcsEnvironment
PmonitoringGcsNamespace: PmonitoringGcsNamespace
PmonitoringTenant: PmonitoringTenant
PcomputeRegionOverride: PcomputeRegionOverride
PappInsightName: PappInsightName
PappInsightsResourceGroupName: PappInsightsResourceGroupName
PmuiName: PmuiName
PfunctionName: PfunctionName
PnsgName: PnsgName
PpublicIpName: PpublicIpName
PnatGatewayName: PnatGatewayName
PnumberOfPublicIPs: PnumberOfPublicIPs
PoutboundServiceTag: PoutboundServiceTag
PvnetName: PvnetName
PKeyvaultResourceId: PKeyvaultResourceId
PstorageAccountResourceId: PstorageAccountResourceId
PstorageAccountName: PstorageAccountName
}

param PeventHubNamespaceResourceId = '/subscriptions/${subscriptionId}/resourceGroups/rg-${globalappname}-${env}-${globallocation}/providers/Microsoft.EventHub/namespaces/ehns-${globalappname}-${env}-${globallocationalias}'
param PEventhubNameSpaceRoleDefinationIds = ['f526a384-b230-433a-b45c-95f59c4a2dec'] //event hub data owner
param PkeyVaultRoleDefinationIds = ['a4417e6f-fecd-4de8-b567-7b0420556985', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7']
param PstorageRoleDefinationIds = [
  '17d1049b-9a84-46fb-8f53-869881c3d3ab'
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
]
param PmuiStorageRoleDefinationIds = [
  'b24988ac-6180-42a0-ab88-20f7382dd24c'
  '17d1049b-9a84-46fb-8f53-869881c3d3ab'
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
]

param PCosmosDbAccountResourceId = '/subscriptions/${subscriptionId}/resourceGroups/rg-${globalappname}-${env}-${globallocation}/providers/Microsoft.DocumentDB/databaseAccounts/csms-${globalappname}-${env}-cm'
param PdeployCosmosResources = 'true'
param PdeployRoleAssignments = 'true'

param PdeploySearchServiceResources = 'true'
param PsearchServiceResourceIds = [
  '/subscriptions/${subscriptionId}/resourceGroups/rg-${globalappname}-${env}-${globallocation}/providers/Microsoft.Search/searchServices/srch-${globalappname}-${env}-${globallocationalias}'
  '/subscriptions/${subscriptionId}/resourceGroups/rg-${globalappname}-${env}-${globallocation}/providers/Microsoft.Search/searchServices/srch-${globalappname}-${env}-${secondarylocationalias}'
]
param PsearchServiceRoleDefinationIds = ['8ebe5a00-799e-43f5-93ac-243d3dce84a7']

param PreportsStorageAccountResourceId = '/subscriptions/${subscriptionId}/resourceGroups/rg-${globalappname}-${env}-${globallocation}/providers/Microsoft.Storage/storageAccounts/stmreports${env}${globallocationalias}'

param PdelayInSeconds = '60'
param PnoOfFunctionDeployedPerApp = '3' //replace to actual function count

// settings
param PAppEnviromentName = '${env}'
param PConfigurationStoreDatabase = 'CXOAI'
param PConfigurationStoreCollection = 'ConfigurationStore'
param PConfigurationStoreLeaseCollection = 'configurations-leases'
param PConfigurationStoreConnectionEndpoint = 'https://csms-${globalappname}-${env}-cm.documents.azure.com:443/'

//blob upload
param PBlobContainerName = 'deploymentpackages'
param PFunctionAppArtifactBlobFileName = 'CXOAI.zip'
param Pev2PackageName = 'package.zip'
param commandsToExecuteInShell = ['/bin/bash', '-c', 'pwsh FunctionAppArtifactBlobFileUpload.ps1']
param PShellMaxExecutionTime = 'PT5M'

param PstorageRGroupName = 'rg-${appname}-${env}-${actuallocation}'
param PoriginGroupName = 'cxoaiapi-origin-group'
param ProuteName = 'cxoaiapi-route'
param PAFDResourceId = '/subscriptions/${subscriptionId}/resourceGroups/rg-${globalappname}-${env}-${globallocation}/providers/Microsoft.Cdn/profiles/afd-${globalappname}-${env}-${globallocationalias}'
param PWafPolicyPatternMatch = '/Api/*'
param PoriginName = 'cxoaiapi-origin-${location}'

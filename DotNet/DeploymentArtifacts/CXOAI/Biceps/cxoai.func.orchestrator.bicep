metadata CurrentApplicationSubscription='__CURRENT_APPLICATION_SUBSCRIPTION__'
param CurrentApplicationSubscription string
metadata Plocation='__CURRENT_APPLICATION_LOCATION__'
metadata PsubscriptionOwnerMuiResourceId='__SUBSCRIPTION_OWNER_MUI_RESOURCE_ID__'

metadata PfailDeployment='__FAIL_DEPLOYMENT__'
param PfailDeployment string

module StopDeployment 'Templates/Core/Extension/ConfigurableStopByFailureDeployment.bicep'={
  name:'ConfigurableStopByFailureDeployment'
  params:{
    PfailDeployment: PfailDeployment
  }
  dependsOn:[]
}
metadata PdeployInfra='__DEPLOY_INFRA__'
param PdeployInfra string

metadata PcreateCXOAIInfraInput = '__CREATE_CXOAI_INFRA_INPUT__'
param PcreateCXOAIInfraInput object
param PcreateCXOAIInfraInput_SCHEMA object={
PservicePlanName: 'string'
PservicePlanSkuName: 'string'
PservicePlanSkuTier: 'string'
PservicePlanSkuCapacity: 'string'
PgenevaCertSecretName: 'string'
PmonitoringConfigVersion: 'string'
PmonitoringGcsAccount: 'string'
PmonitoringGcsAuthId: 'string'
PmonitoringGcsEnvironment: 'string'
PmonitoringGcsNamespace: 'string'
PmonitoringTenant: 'string'
PcomputeRegionOverride: 'string'
PappInsightName: 'string'
PappInsightsResourceGroupName: 'string'
PmuiName: 'string'
PfunctionName: 'string'
PnsgName: 'string'
PpublicIpName: 'string'
PnatGatewayName: 'string'
PnumberOfPublicIPs: 'string'
PoutboundServiceTag: 'array'
PvnetName: 'string'
PKeyvaultResourceId: 'string'
PstorageAccountResourceId: 'string'
PstorageAccountName: 'string'
}

module CreateCXOAIInfra 'Templates/CreateCXOAIInfra.bicep'={
  name:'CreateCXOAIInfra'
  params:{
    PdeployInfra:PdeployInfra
    PcreateCXOAIInfraInput:PcreateCXOAIInfraInput
  }
  dependsOn:[StopDeployment]
}

metadata PmuiStorageRoleDefinationIds ='__MUI_STORAGE_ROLE_DEFINATION_IDS__'
param PmuiStorageRoleDefinationIds array
module AppMuiStorageRbac 'Templates/Core/MuiStorageRbacAssignmentResourceDefination.bicep'= {
  name:'MuiStorageRbacAssignmentResourceDefination.bicep'
  params:{
     ProleDefinationIds:PmuiStorageRoleDefinationIds
     PsourceResourceName:CreateCXOAIInfra.outputs.App_MuiName
     PsourceResourceSubscriptionId:subscription().subscriptionId
     PsourceRGName:resourceGroup().name
     PtargetResourceId:CreateCXOAIInfra.outputs.App_StorageAccountResourceId
     PdeployInfra:PdeployInfra 
  }
  dependsOn:[CreateCXOAIInfra]
}


module AssignAppMuiRoles 'Templates/Core/MUIRbacResourceDefination.bicep'= {
  name:'MUIRoleAssignmentResourceDefination'
  params:{
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PmuiPrincipleId:CreateCXOAIInfra.outputs.CreateAppMui_userManagedIdentityPrincipleId
    PdeployInfra:PdeployInfra 
  }
  dependsOn:[CreateCXOAIInfra]
}


metadata PstorageRoleDefinationIds ='__STORAGE_ROLE_DEFINATION_IDS__'
param PstorageRoleDefinationIds array

metadata PdeployRoleAssignments='__DEPLOY_ROLE_ASSIGNMENTS__'
param PdeployRoleAssignments string

@description('Create storage Role Assignments for function app (system identity)')
module AssignStorageRoles 'Templates/Core/GeneralFunctionRbacAssignmentResourceDefination.bicep'={
  name:'AssignStorageRoles'
  params:{
    ProleDefinationIds:PstorageRoleDefinationIds
    PResourceId:CreateCXOAIInfra.outputs.App_StorageAccountResourceId
    PfunctionRGName:resourceGroup().name
    PfunctionSubscriptionId:subscription().subscriptionId
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PdeployRoleAssignments:PdeployRoleAssignments 
    PdeployInfra:PdeployInfra 
  }
  dependsOn:[CreateCXOAIInfra]
}


metadata PkeyVaultRoleDefinationIds ='__KEYVAULT_ROLE_DEFINATION_IDS__'
param PkeyVaultRoleDefinationIds array

@description('Create KeyVault Role Assignments for function app (system identity)')
module AssignKeyVaultRoles 'Templates/Core/GeneralFunctionRbacAssignmentResourceDefination.bicep'={
  name:'FuncKeyVaultRbacAssignmentResourceGroup'
  params:{
    ProleDefinationIds:PkeyVaultRoleDefinationIds
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PfunctionRGName:resourceGroup().name
    PfunctionSubscriptionId:subscription().subscriptionId
    PResourceId:CreateCXOAIInfra.outputs.App_KeyvaultResourceId 
    PdeployRoleAssignments:PdeployRoleAssignments  
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}

metadata PeventHubNamespaceResourceId = '__EVENTHUB_NAMESPACE_RESOURCE_ID__'
param PeventHubNamespaceResourceId string

metadata PEventhubNameSpaceRoleDefinationIds ='__EVENTHUBNAMESPACE_ROLE_DEFINATION_IDS__'
param PEventhubNameSpaceRoleDefinationIds array

@description('Create EventHub Namespace Role Assignments for function app (system identity)')
module AssignEventHubNameSpaceRoles 'Templates/Core/GeneralFunctionRbacAssignmentResourceDefination.bicep'={
  name:'FuncEventHubNameSpaceRbacAssignmentResourceGroup'
  params:{
    ProleDefinationIds:PEventhubNameSpaceRoleDefinationIds
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PfunctionRGName:resourceGroup().name
    PfunctionSubscriptionId:subscription().subscriptionId
    PResourceId:PeventHubNamespaceResourceId 
    PdeployRoleAssignments:PdeployRoleAssignments  
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}

metadata PCosmosDbAccountResourceId='__COSMOSDB_RESOURCE_ID__'
param PCosmosDbAccountResourceId string
metadata PdeployCosmosResources='__DEPLOY_COSMOS_RESOURCES__'
param PdeployCosmosResources string 
@description('Create Cosmos Sql Role Assignment for function app (system identity)')
module AssignCosmosDbRoles 'Templates/Core/FunctionCosmosDbRbacResourceDescription.bicep' = {
  name: 'FunctionCosmosDbRbacResourceDescription'
  params: {
    PfunctionName: CreateCXOAIInfra.outputs.FunctionName
    PfunctionRGName: resourceGroup().name
    PfunctionSubscriptionId: subscription().subscriptionId
    PcommerialCosmosDbAccountResourceId: PCosmosDbAccountResourceId
    PdeployCosmosResources:PdeployCosmosResources
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}


metadata PsearchServiceResourceIds='__SEARCH_SERVICE_RESOURCE_IDS__'
param PsearchServiceResourceIds array
metadata PdeploySearchServiceResources='__DEPLOY_SEARCH_SERVICE_RESOURCES__'
param PdeploySearchServiceResources string
metadata PsearchServiceRoleDefinationIds='__SEARCH_SERVICE_ROLE_DEFINATION_IDS__'
param PsearchServiceRoleDefinationIds array

@description('Create Search Service Role Assignments for function app (system identity)')
module AssignSearchServiceRbac 'Templates/MultiSearchServiceFunctionRbacResourceDefination.bicep'={
  name:'FuncSearchServiceRbacAssignmentResourceGroup'
  params:{
     PdeployRoleAssignments:PdeploySearchServiceResources
      PfunctionName:CreateCXOAIInfra.outputs.FunctionName
      PfunctionRGName:resourceGroup().name
      PfunctionSubscriptionId:subscription().subscriptionId
      PResourceIds:PsearchServiceResourceIds
      PRoleDefinationIds:PsearchServiceRoleDefinationIds
      PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}

metadata PreportsStorageAccountResourceId='__REPORTS_STORAGE_ACCOUNT_RESOURCE_ID__'
param PreportsStorageAccountResourceId string

@description('Create Reports Storage Role Assignments for function app (system identity)')
module AssignReportsStorageRoles 'Templates/Core/GeneralFunctionRbacAssignmentResourceDefination.bicep'={
  name:'AssignReportsStorageRoles'
  params:{
    ProleDefinationIds:PstorageRoleDefinationIds
    PResourceId:PreportsStorageAccountResourceId
    PfunctionRGName:resourceGroup().name
    PfunctionSubscriptionId:subscription().subscriptionId
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PdeployRoleAssignments:PdeployRoleAssignments 
    PdeployInfra:PdeployInfra 
  }
  dependsOn:[CreateCXOAIInfra]
}


@description('Create private endpoint (blob,table,queue,File) for function storage')
module CreateStoragePrivateEndpoints 'Templates/Core/FuncStoragePrivateEndpointResourceDefinition.bicep'={
  name:'CreateStoragePrivateEndpoints'
  params:{
    PstorageAccountId:CreateCXOAIInfra.outputs.App_StorageAccountResourceId
    PvnetName:CreateCXOAIInfra.outputs.DefaultVnetName
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}


@description('Create private endpoint for KeyVault')
module CreatekeyVaultPrivateEndpoints 'Templates/Core/FuncKeyvaultVnetPrivateEndpointResourceDefinition.bicep'={
  name:'keyVaultPrivateEndpointResourceDefinition'
  params:{
     PkeyVaultAccountId:CreateCXOAIInfra.outputs.App_KeyvaultResourceId
    PvnetName:CreateCXOAIInfra.outputs.DefaultVnetName
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}


@description('Create private endpoint for EventHub namespace')
module CreateEventHubNameSpacePrivateEndpoints 'Templates/Core/FuncEHsPrivateEndpointResourceDefinition.bicep'={
  name:'CreateEventHubNameSpacePrivateEndpoints'
  params:{
    PeventHubNamespaceAccountId:PeventHubNamespaceResourceId
    PvnetName:CreateCXOAIInfra.outputs.DefaultVnetName
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PdeployEventhubPrivateEndpoint:'false'
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}

@description('Create private endpoint for Cosmos DB')
module CreateCosmosDbPrivateEndpoints 'Templates/Core/FuncCosmosDbVnetPrivateEndpointResourceDefinition.bicep'={
  name:'FunctionCosmosDbVnetPrivateEndpointResourceDefinition'
  params:{
    PCosmosDbAccountResourceId:PCosmosDbAccountResourceId
    PvnetName:CreateCXOAIInfra.outputs.DefaultVnetName
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PdeployCosmosResources:PdeployCosmosResources
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateEventHubNameSpacePrivateEndpoints]
}


@description('Create private endpoint for Reports Storage')
module CreateReportsStoragePrivateEndpoints 'Templates/Core/FuncStoragePrivateEndpointResourceDefinition.bicep'={
  name:'CreateReportsStoragePrivateEndpoints'
  params:{
    PstorageAccountId:PreportsStorageAccountResourceId
    PvnetName:CreateCXOAIInfra.outputs.DefaultVnetName
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateStoragePrivateEndpoints]
}


@description('Stop the staging slot')
module StopStagingSlot 'Templates/Core/SlotStopResourceDefination.bicep'= {
  name:'AppSlotStopResourceDefination'
  params:{
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PmuiResourceId:CreateCXOAIInfra.outputs.CreateAppMui_userManagedIdentityResourceId
    PstorageAccountRGroupName:resourceGroup().name
    Pdeploy:PdeployInfra
  }
  dependsOn:[CreateCXOAIInfra]
}

/////////////////////////////////////////////////////////////////////////////


metadata GetAppMUIResourceDefination_outputs_userManagedIdentityResourceId='__GET_APP_MANAGED_USER_IDENTITY_RESOURCE_ID__'
metadata GetAppMUIResourceDefination_outputs_userManagedIdentityClientId='__GET_APP_MANAGED_USER_IDENTITY_CLIENT_ID__'
metadata GetAppMUIResourceDefination_outputs_userManagedIdentityPrincipleId='__GET_APP_MANAGED_USER_IDENTITY_PRINCIPLE_ID__'
module GetAppMui 'Templates/Core/GetUserManagedIdentityResourceDefination.bicep'={
  name:'GetAppMUIResourceDefination'
  params:{
    PuserManagedIdentityName:CreateCXOAIInfra.outputs.App_MuiName
    PusermanagedIdentityRGName:resourceGroup().name
  }
  dependsOn:[StopStagingSlot]
}



metadata BlobContainerName = '__APP_CODE_BLOB_CONTAINER_NAME__'
param PBlobContainerName string


metadata PstorageRGroupName ='__STORAGE_ACCOUNT_RESOURCE_GROUP_NAME__'
param PstorageRGroupName string

metadata PFunctionAppArtifactBlobFileName ='__FUNCTION_APP_ARTIFACT_BLOB_FILE_NAME__'
param PFunctionAppArtifactBlobFileName string

metadata Pev2PackageName ='__EV2_PACKAGE_NAME__'
param Pev2PackageName string

metadata commandsToExecuteInShell = '__COMMANDS_TO_EXECUTE_IN_SHELL__'
param commandsToExecuteInShell array

metadata PShellMaxExecutionTime ='__SHELL_MAX_EXECUTION_TIME__'
param PShellMaxExecutionTime string

metadata PAppEnviromentName ='__APP_ENVIRONMENT_NAME__'
param PAppEnviromentName string

metadata FunctionAppUploadArtifactFileShellDefinition='SHELL'
module UploadArtifactToBlob 'Templates/Core/Extension/FunctionAppUploadArtifactFileUploadDefinition.bicep'={
  name:'FunctionAppUploadArtifactFileShellDefinition'
  params:{
    ContainerName:PBlobContainerName
    StorageName:CreateCXOAIInfra.outputs.App_StorageAccountName   
    FunctionAppArtifactBlobFileName:PFunctionAppArtifactBlobFileName
    IdentityClientId:GetAppMui.outputs.userManagedIdentityClientId
    muiResourceId:GetAppMui.outputs.userManagedIdentityResourceId
    packageName: Pev2PackageName
    commandsToExecuteInShell: commandsToExecuteInShell
    FunctionAppArtifactZipFileName: PFunctionAppArtifactBlobFileName
    maxExecutionTime:PShellMaxExecutionTime
    resourceGroupName:PstorageRGroupName
    EnvironmentName:PAppEnviromentName
    CurrentApplicationSubscription:CurrentApplicationSubscription
  }
  dependsOn:[GetAppMui]
}


@description('Start function app staging slot')
module StartSlot 'Templates/Core/SlotStartResourceDefination.bicep'={
  name:'SlotStartResourceDefination'
  params:{
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PmuiResourceId:GetAppMui.outputs.userManagedIdentityResourceId
    PstorageAccountRGroupName:resourceGroup().name
  }
  dependsOn:[UploadArtifactToBlob]
}


metadata PConfigurationStoreDatabase='__CONFIGURATION_STORE_DATABASE__'
param PConfigurationStoreDatabase string
metadata PConfigurationStoreCollection='__CONFIGURATION_STORE_COLLECTION__'
param PConfigurationStoreCollection string
metadata PConfigurationStoreLeaseCollection='__CONFIGURATION_STORE_LEASE_COLLECTION__'
param PConfigurationStoreLeaseCollection string
metadata PConfigurationStoreConnectionEndpoint='__CONFIGURATION_STORE_CONNECTION_ENDPOINT__'
param PConfigurationStoreConnectionEndpoint string

@description('Set function Slot app settings')
module SetSlotSettings 'Templates/Core/SlotDeployResourceDefinition.bicep'= {
  name:'SlotDeployResourceDefination'
  params:{
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PstorageAccountName: CreateCXOAIInfra.outputs.App_StorageAccountName
    PappInsightConnectionString: CreateCXOAIInfra.outputs.GetAppInsights_AppInsightConnectionString
    PBlobContainerName:PBlobContainerName
    PFunctionAppArtifactBlobFileName:PFunctionAppArtifactBlobFileName
    PAppEnviromentName:PAppEnviromentName
    PConfigurationStoreDatabase:PConfigurationStoreDatabase
    PConfigurationStoreCollection:PConfigurationStoreCollection
    PConfigurationStoreLeaseCollection:PConfigurationStoreLeaseCollection
    PConfigurationStoreConnectionEndpoint:PConfigurationStoreConnectionEndpoint
    }
  dependsOn:[StartSlot,CreateCXOAIInfra]
}

metadata PdelayInSeconds ='__WAIT_DELAY_IN_SECONDS__'
param PdelayInSeconds string


@description('Wait for few minutes after deployment of code')
module WaitforSlotToDeploy 'Templates/Core/SlotWaitResourceDefination.bicep'= {
  name:'SlotWaitResourceDefination'
  params:{
    PdelayInSeconds:PdelayInSeconds
    PmuiResourceId:GetAppMui.outputs.userManagedIdentityResourceId
  }
  dependsOn:[SetSlotSettings]
}

metadata PnoOfFunctionDeployedPerApp = '__NO_OF_FUNCTION_DEPLOYED_PER_APP__'
param PnoOfFunctionDeployedPerApp string

@description('Validate function app deployment (match no of function active with no of function deployed)')
module ValidateSlot 'Templates/Core/SlotValidateResourceDefination.bicep'= {
  name:'SlotValidateResourceDefination'
  params:{
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
     PnoOfFunctionDeployedPerApp:PnoOfFunctionDeployedPerApp
    PmuiResourceId:GetAppMui.outputs.userManagedIdentityResourceId
     PstorageAccountRGroupName:resourceGroup().name
     PSubscriptionId:subscription().subscriptionId
  }
  dependsOn:[WaitforSlotToDeploy]
}


module SwapSlot 'Templates/Core/SlotSwapResourceDefination.bicep'={
  name:'SlotSwapResourceDefination'
  params:{
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PmuiResourceId:GetAppMui.outputs.userManagedIdentityResourceId
    PstorageAccountRGroupName:resourceGroup().name
  }
  dependsOn:[ValidateSlot]
}


module StopSlot 'Templates/Core/SlotStopResourceDefination.bicep'= {
  name:'SlotStopResourceDefination'
  params:{
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PmuiResourceId:GetAppMui.outputs.userManagedIdentityResourceId
    PstorageAccountRGroupName:resourceGroup().name
    Pdeploy:'false'
  }
  dependsOn:[SwapSlot]
}



metadata PAFDResourceId='__AZURE_FRONT_DOOR_RESOURCE_ID__'
param PAFDResourceId string
metadata PWafPolicyPatternMatch ='__PATTERN_MATCH__'
param PWafPolicyPatternMatch string
metadata PoriginGroupName='__ORIGIN_GROUP_NAMES__' 
param PoriginGroupName string
metadata PoriginName='__ORIGIN_NAME__'
param PoriginName string
metadata ProuteName='__ROUTE_NAME__'
param ProuteName string

module CreateAFDProfileOriginRouteDefination 'Templates/ScopedAFDProfileOriginRouteDefination.bicep'={
  name:'AFDProfileOriginRouteDefination'
  params:{
    PAFDResourceId:PAFDResourceId
    PfunctionName:CreateCXOAIInfra.outputs.FunctionName
    PWafPolicyPatternMatch:PWafPolicyPatternMatch
    PoriginGroupName:PoriginGroupName
    PoriginName:PoriginName
    ProuteName: ProuteName
  }
  dependsOn:[StopSlot]
}



//az deployment group create -g rg-cxoai-test-canadacentral -f cxoai.func.orchestrator.bicep -p cxoai.func.orchestrator.test.bicepparam

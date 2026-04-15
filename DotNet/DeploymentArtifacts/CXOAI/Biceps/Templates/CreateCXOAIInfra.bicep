param PdeployInfra string

param PcreateCXOAIInfraInput object

var PservicePlanName= PcreateCXOAIInfraInput.PservicePlanName
var PservicePlanSkuName= PcreateCXOAIInfraInput.PservicePlanSkuName
var PservicePlanSkuTier= PcreateCXOAIInfraInput.PservicePlanSkuTier
var PservicePlanSkuCapacity= PcreateCXOAIInfraInput.PservicePlanSkuCapacity
var PgenevaCertSecretName= PcreateCXOAIInfraInput.PgenevaCertSecretName
var PmonitoringConfigVersion= PcreateCXOAIInfraInput.PmonitoringConfigVersion
var PmonitoringGcsAccount= PcreateCXOAIInfraInput.PmonitoringGcsAccount
var PmonitoringGcsAuthId= PcreateCXOAIInfraInput.PmonitoringGcsAuthId
var PmonitoringGcsEnvironment= PcreateCXOAIInfraInput.PmonitoringGcsEnvironment
var PmonitoringGcsNamespace= PcreateCXOAIInfraInput.PmonitoringGcsNamespace
var PmonitoringTenant= PcreateCXOAIInfraInput.PmonitoringTenant
var PcomputeRegionOverride= PcreateCXOAIInfraInput.PcomputeRegionOverride
var PappInsightName= PcreateCXOAIInfraInput.PappInsightName
var PappInsightsResourceGroupName= PcreateCXOAIInfraInput.PappInsightsResourceGroupName
var PmuiName= PcreateCXOAIInfraInput.PmuiName
var PfunctionName= PcreateCXOAIInfraInput.PfunctionName
var PnsgName= PcreateCXOAIInfraInput.PnsgName
var PpublicIpName= PcreateCXOAIInfraInput.PpublicIpName
var PnatGatewayName= PcreateCXOAIInfraInput.PnatGatewayName
var PnumberOfPublicIPs= PcreateCXOAIInfraInput.PnumberOfPublicIPs
var PoutboundServiceTag= PcreateCXOAIInfraInput.PoutboundServiceTag
var PvnetName= PcreateCXOAIInfraInput.PvnetName
var PKeyvaultResourceId= PcreateCXOAIInfraInput.PKeyvaultResourceId
var PstorageAccountResourceId= PcreateCXOAIInfraInput.PstorageAccountResourceId
var PstorageAccountName= PcreateCXOAIInfraInput.PstorageAccountName



@description('Create Service Plans for each function app')
module CreateServicePlan 'Core/ServicePlanResourceDefinition.bicep'={
  name:'ServicePlanResourceDefinition'
  params:{
    PservicePlanName:PservicePlanName
    PservicePlanSkuName:PservicePlanSkuName
    PservicePlanSkuTier:PservicePlanSkuTier
    PservicePlanSkuCapacity:PservicePlanSkuCapacity
    PgenevaCertSecretName:PgenevaCertSecretName
    PgenevaCertVaultId:PKeyvaultResourceId
    PmonitoringConfigVersion:PmonitoringConfigVersion
    PmonitoringGcsAccount:PmonitoringGcsAccount
    PmonitoringGcsAuthId:PmonitoringGcsAuthId  
    PmonitoringGcsEnvironment:PmonitoringGcsEnvironment
    PmonitoringGcsNamespace:PmonitoringGcsNamespace
    PmonitoringRole:resourceGroup().location
    PmonitoringTenant:PmonitoringTenant
    PcomputeRegionOverride:PcomputeRegionOverride
    PdeployInfra:PdeployInfra
  } 
  dependsOn:[]
}



@description('Refer to existing AppInsight')
module GetAppInsights 'Core/AppInsightResourceDefinition.bicep'={
  name:'AppInsightResourceDefinition'
  params:{
    PappInsightName:PappInsightName
    PappInsightsResourceGroupName:PappInsightsResourceGroupName
  }
  dependsOn:[]
}



module CreateAppMui 'Core/MUIResourceDefination.bicep'={
  name:'AppMUIResourceDefination'
  params:{
     PmuiName:PmuiName
     PdeployInfra:PdeployInfra
  }
  dependsOn:[]
}



@description('Create azure function app')
module CreateFunction 'Core/FunctionResourceDefinition.bicep'={
  name:'FunctionResourceDefinition'
  params:{
    PfunctionName:PfunctionName
    PservicePlanName:PservicePlanName
    PmuiResourceId:CreateAppMui.outputs.userManagedIdentityResourceId
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateServicePlan,GetAppInsights]
}


@description('Set function app settings')
module SetFunctionSettings 'Core/FunctionSettingResourceDefinition.bicep'= {
  name:'FunctionSettingResourceDefination'
  params:{
    PfunctionName:PfunctionName
    PstorageAccountName: PstorageAccountName
    PappInsightConnectionString: GetAppInsights.outputs.appInsightConnectionString
    PdeployInfra:PdeployInfra
    }
  dependsOn:[CreateFunction,GetAppInsights]
}




@description('Create NSG for function app subnet')
module CreateNSG 'Core/NsgResourceDefinition.bicep'={
  name:'NsgResourceDefinition'
  params:{
    PnsgName:PnsgName
    PdeployInfra:PdeployInfra
  }
  dependsOn:[SetFunctionSettings]
}



@description('Create NAT Gateway for Subnet')
module NATGateway 'Core/NATGateway.bicep'={
  name:'NATGateWay'
  params:{
    PpublicIpName:PpublicIpName
    PnatGatewayName:PnatGatewayName
    PnumberOfPublicIPs:PnumberOfPublicIPs
    PoutboundServiceTag:PoutboundServiceTag
    PdeployInfra:PdeployInfra
  }
  dependsOn:[]
}



@description('Create VNet and assign to function app')
module AssignVNetToFunctions 'Core/FunctionVnetSettingResourceDefinition.bicep'={
  name:'FunctionVnetSettingResourceDefinition'
  params:{
    PvnetName:PvnetName
    PnsgName:PnsgName
    PfunctionName:PfunctionName 
    PnatGatewayId: NATGateway.outputs.natGatewayId
    PdeployInfra:PdeployInfra
  }
  dependsOn:[CreateNSG,NATGateway]
}

output CreateAppMui_userManagedIdentityResourceId string =CreateAppMui.outputs.userManagedIdentityResourceId
output CreateAppMui_userManagedIdentityClientId string = CreateAppMui.outputs.userManagedIdentityClientId
output CreateAppMui_userManagedIdentityPrincipleId string = CreateAppMui.outputs.userManagedIdentityPrincipleId
output FunctionName string= PcreateCXOAIInfraInput.PfunctionName
output DefaultVnetName string= PcreateCXOAIInfraInput.PvnetName
output DefaultMuiName string= PcreateCXOAIInfraInput.PmuiName
output GetAppInsights_AppInsightConnectionString string= GetAppInsights.outputs.appInsightConnectionString
output App_StorageAccountName string= PcreateCXOAIInfraInput.PstorageAccountName
output App_StorageAccountResourceId string= PcreateCXOAIInfraInput.PstorageAccountResourceId
output App_MuiName string= PcreateCXOAIInfraInput.PmuiName
output App_KeyvaultResourceId string= PcreateCXOAIInfraInput.PKeyvaultResourceId

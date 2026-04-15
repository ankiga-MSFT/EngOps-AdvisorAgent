param PfunctionName string
param PstorageAccountRGroupName string
param PmuiResourceId string
var scriptId = 'SwapDeploymentSlot-${PfunctionName}-deploy'
var subscriptionId = split(PmuiResourceId,'/')[2]
module SwapDeploymentSlot 'DeployScripts.Template.bicep'={
  name:scriptId
 params: {
  Pps_command:'Set-AzContext -SubscriptionId ${subscriptionId} \n Switch-AzWebAppSlot -ResourceGroupName ${PstorageAccountRGroupName} -Name ${PfunctionName} -SourceSlotName staging -DestinationSlotName production'
  PmuiResourceId:PmuiResourceId
  PScriptId:scriptId
}
}


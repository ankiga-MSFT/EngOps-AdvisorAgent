
param PfunctionName string
param PstorageAccountRGroupName string
param PmuiResourceId string
var subscriptionId = split(PmuiResourceId,'/')[2]
var scriptId = 'StartDeploymentSlot-${PfunctionName}-deploy'
module StartDeploymentSlot 'DeployScripts.Template.bicep'={
  name:scriptId
 params: {
  PmuiResourceId:PmuiResourceId
  Pps_command:'Set-AzContext -SubscriptionId ${subscriptionId} \n Start-AzWebAppSlot -ResourceGroupName ${PstorageAccountRGroupName} -Name ${PfunctionName} -Slot staging'
  PScriptId:scriptId
}
}



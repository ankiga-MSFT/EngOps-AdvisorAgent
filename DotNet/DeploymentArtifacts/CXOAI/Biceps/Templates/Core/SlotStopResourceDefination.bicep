
param PfunctionName string
param PstorageAccountRGroupName string
param PmuiResourceId string
param Pdeploy string
var deploy = bool(Pdeploy)
var subscriptionId = deploy? split(PmuiResourceId,'/')[2]:''
var scriptId = 'StopDeploymentSlot-${PfunctionName}'
module StopDeploymentSlot 'DeployScripts.Template.bicep'= if(deploy){
  name:scriptId
 params: {
 Pps_command:'Set-AzContext -SubscriptionId ${subscriptionId} \n Stop-AzWebAppSlot -ResourceGroupName ${PstorageAccountRGroupName} -Name ${PfunctionName} -Slot staging'
 PmuiResourceId:PmuiResourceId
 PScriptId:scriptId
}
}

//az functionapp start --slot  --name '' --resource-group ''  

param PfunctionName string
param PstorageAccountRGroupName string
param PSubscriptionId string
param PmuiResourceId string
param PnoOfFunctionDeployedPerApp string
var scriptVariables ='$functionAppName = "${PfunctionName}" \n $resourceGroupName = "${PstorageAccountRGroupName}" \n $expectedFunctionCount = ${PnoOfFunctionDeployedPerApp} \n $subscriptionId= "${PSubscriptionId}" \n $slotName = "staging" \n'
var fetchcountScript = '$jsoncontent=Invoke-AzRestMethod -Path "/subscriptions/${PSubscriptionId}/resourcegroups/${PstorageAccountRGroupName}/providers/Microsoft.Web/sites/${PfunctionName}/slots/staging/functions?api-version=2018-11-01" -Method GET \n'
var mainScript = ''' 
$resultObject = $jsoncontent.Content | ConvertFrom-Json 
 $functionCount = $resultObject.Value.length
 
if($functionCount -eq $expectedFunctionCount){
   Write-Output "Success: The number of functions ($functionCount) matches the expected number ($expectedFunctionCount)."
   
} 
else {
   throw "Error: The number of functions ($functionCount) does not match the expected number ($expectedFunctionCount). The Params are: PfunctionName - ($functionAppName), PstorageAccountRGroupName = ($resourceGroupName), PSubscriptionId = ($subscriptionId), PnoOfFunctionDeployedPerApp = ($expectedFunctionCount) \n resultObject : $resultObject"
   
} 
'''
var script='${scriptVariables}${fetchcountScript}${mainScript}'
var scriptId = 'ValidateDeploymentSlot-${PfunctionName}'

module ValidateDeploymentSlot 'DeployScripts.Template.bicep'={
  name:scriptId
 params: {
  Pps_command:script
   PmuiResourceId:PmuiResourceId
   PScriptId:scriptId
}
}

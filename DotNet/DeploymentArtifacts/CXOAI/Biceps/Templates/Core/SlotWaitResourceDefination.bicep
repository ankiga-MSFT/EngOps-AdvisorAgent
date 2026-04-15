
param PmuiResourceId string
param PdelayInSeconds string
var scriptId = 'WaitAfterDeploymentSlot-deploy'

module WaitAfterDeploymentSlot 'DeployScripts.Template.bicep'={
  name:scriptId
 params: {
  Pps_command:'Start-Sleep -Seconds ${PdelayInSeconds}'
 PmuiResourceId:PmuiResourceId
 PScriptId:scriptId
}
}


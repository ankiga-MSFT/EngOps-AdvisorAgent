param utcValue string = utcNow()
param Pps_command string
param PmuiResourceId string
param PScriptId string
resource runPowerShellInlineWithOutput 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'runPsScript-${guid(PScriptId)}'
  location: resourceGroup().location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${PmuiResourceId}': {}
    }
  }
  kind:  'AzurePowerShell'
  properties: {
    forceUpdateTag: utcValue
    azPowerShellVersion: '5.6.0'
    scriptContent: Pps_command
    timeout: 'PT30M'
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
     
  }
}

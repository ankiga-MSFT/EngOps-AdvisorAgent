param utcValue string = utcNow()
param PazCli_command string
param PmuiResourceId string
param PScriptId string
resource runPowerShellInlineWithOutput 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'runAzCliScript-${guid(PScriptId)}'
  location: resourceGroup().location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${PmuiResourceId}': {}
    }
  }
  kind:  'AzureCLI'
  properties: {
    forceUpdateTag: utcValue
    azCliVersion: '2.47.0'
    scriptContent: PazCli_command
    timeout: 'PT60M'
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
  }
}

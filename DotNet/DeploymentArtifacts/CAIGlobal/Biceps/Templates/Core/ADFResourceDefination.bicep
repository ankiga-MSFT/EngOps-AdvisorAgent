param PadfName string
param PdeployAdf string
param PmuiResourceId string
var deployAdf = bool(PdeployAdf)
resource adf 'Microsoft.DataFactory/factories@2018-06-01' = if(deployAdf) {
  name: PadfName
  location: resourceGroup().location
  properties: {
    publicNetworkAccess: 'Disabled'
  }
  identity: {
    type: 'SystemAssigned,UserAssigned' 
    userAssignedIdentities: { 
      '${PmuiResourceId}': {}
      }
  }
}

output adfResourceId string = deployAdf ? adf.id : ''
output adfClientId string = deployAdf ? adf.identity.principalId :''

param PmuiName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = if(deployInfra) {
  name: PmuiName
  location: resourceGroup().location
  properties:{
  isolationScope: 'Regional'
  }

}

output userManagedIdentityResourceId string = deployInfra ? userAssignedIdentity.id :''
output userManagedIdentityClientId string = deployInfra ? userAssignedIdentity.properties.clientId :''
output userManagedIdentityPrincipleId string = deployInfra ? userAssignedIdentity.properties.principalId :''

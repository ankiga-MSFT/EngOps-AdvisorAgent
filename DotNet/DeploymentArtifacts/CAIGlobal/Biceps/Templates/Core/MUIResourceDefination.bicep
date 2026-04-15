param PmuiName string


resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: PmuiName
  location: resourceGroup().location
  properties:{
  isolationScope: 'Regional'
  }

}

output userManagedIdentityResourceId string = userAssignedIdentity.id
output userManagedIdentityClientId string = userAssignedIdentity.properties.clientId
output userManagedIdentityPrincipleId string = userAssignedIdentity.properties.principalId

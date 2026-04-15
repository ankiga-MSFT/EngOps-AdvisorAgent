param PuserManagedIdentityName string
param PusermanagedIdentityRGName string

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: PuserManagedIdentityName
  scope: resourceGroup(PusermanagedIdentityRGName)
}

output userManagedIdentityResourceId string=userManagedIdentity.id
output userManagedIdentityClientId string=userManagedIdentity.properties.clientId
output userManagedIdentityPrincipleId string=userManagedIdentity.properties.principalId

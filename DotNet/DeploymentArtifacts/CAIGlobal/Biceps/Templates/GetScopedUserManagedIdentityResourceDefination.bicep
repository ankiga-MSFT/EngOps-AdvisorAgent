param PusermanagedIdentityResourceId string

var subscriptionId= split(PusermanagedIdentityResourceId, '/')[2]
var resourceGroupName= split(PusermanagedIdentityResourceId, '/')[4]
var userManagedIdentityName= split(PusermanagedIdentityResourceId, '/')[8]

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
  scope: resourceGroup(subscriptionId,resourceGroupName)
}

output userManagedIdentityResourceId string=userManagedIdentity.id
output userManagedIdentityClientId string=userManagedIdentity.properties.clientId
output userManagedIdentityPrincipleId string=userManagedIdentity.properties.principalId

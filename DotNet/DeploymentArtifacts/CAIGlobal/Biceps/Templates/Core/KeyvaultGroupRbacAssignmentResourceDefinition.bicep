param PappObjectId string
param ProleDefinationIds array 
param PdeployAppRoleAccess string
param PkeyvaultName string
var deployAppRoleAccess=bool(PdeployAppRoleAccess)

resource Keyvault 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing=if(deployAppRoleAccess) {
  name: PkeyvaultName
  scope: resourceGroup()
}

@batchSize(1)
resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = [for i in range(0,length(ProleDefinationIds)): if(deployAppRoleAccess) {
  name: guid('${PappObjectId}-${PkeyvaultName}-${ProleDefinationIds[i]}')
  scope: Keyvault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',ProleDefinationIds[i])
    principalId: PappObjectId
    principalType: 'ServicePrincipal'
  }
}
]




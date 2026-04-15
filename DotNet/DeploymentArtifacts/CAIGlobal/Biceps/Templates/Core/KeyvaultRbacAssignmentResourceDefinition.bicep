param PkeyvaultAccountName string
param PkeyvaultRoleDefinationIds  array
param PresourceManagedIdentityId string
resource appkeyvaultAccount 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing={
  name: PkeyvaultAccountName
  scope: resourceGroup()
}


resource rbackeyvault  'Microsoft.Authorization/roleAssignments@2020-10-01-preview' =[for roleDefinationId in PkeyvaultRoleDefinationIds: {
  name: guid('${appkeyvaultAccount.id}-${PresourceManagedIdentityId}',roleDefinationId)
  scope: appkeyvaultAccount
  properties: {
    principalId: PresourceManagedIdentityId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',roleDefinationId)
  }
}
]

param PsearchServiceAccountName string
param PsearchServiceRoleDefinationIds  array
param PresourceManagedIdentityId string
resource appsearchServiceAccount 'Microsoft.Search/searchServices@2024-06-01-Preview' existing={
  name: PsearchServiceAccountName
  scope: resourceGroup()
}


resource rbacsearchService  'Microsoft.Authorization/roleAssignments@2020-10-01-preview' =[for roleDefinationId in PsearchServiceRoleDefinationIds: {
  name: guid('${appsearchServiceAccount.id}-${PresourceManagedIdentityId}',roleDefinationId)
  scope: appsearchServiceAccount
  properties: {
    principalId: PresourceManagedIdentityId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',roleDefinationId)
  }
}
]

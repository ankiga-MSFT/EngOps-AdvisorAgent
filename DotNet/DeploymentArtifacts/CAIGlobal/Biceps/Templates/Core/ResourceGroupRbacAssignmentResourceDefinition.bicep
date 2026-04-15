param PresourceGroupRoleDefinationIds  array
param PresourceManagedIdentityId string


resource rbackeyvault  'Microsoft.Authorization/roleAssignments@2020-10-01-preview' =[for roleDefinationId in PresourceGroupRoleDefinationIds: {
  name: guid('${resourceGroup().id}-${PresourceManagedIdentityId}',roleDefinationId)
  scope: resourceGroup()
  properties: {
    principalId: PresourceManagedIdentityId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',roleDefinationId)
  }
}
]

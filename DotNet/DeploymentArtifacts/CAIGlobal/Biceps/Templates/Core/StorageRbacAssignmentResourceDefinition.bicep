param PstorageAccountName string
param PstorageRoleDefinationIds  array
param PresourceManagedIdentityId string
resource appStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing={
  name: PstorageAccountName
  scope: resourceGroup()
}


resource rbacstorage  'Microsoft.Authorization/roleAssignments@2020-10-01-preview' =[for roleDefinationId in PstorageRoleDefinationIds: {
  name: guid('${appStorageAccount.id}-${PresourceManagedIdentityId}',roleDefinationId)
  scope: appStorageAccount
  properties: {
    principalId: PresourceManagedIdentityId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',roleDefinationId)
  }
}
]

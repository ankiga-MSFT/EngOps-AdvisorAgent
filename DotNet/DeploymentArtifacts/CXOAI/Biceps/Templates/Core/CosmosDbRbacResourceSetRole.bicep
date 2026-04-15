param PprincipleId string
param PcosmosDbAccountName string 
param PcosmosDbDataRoleIds array 
param PappName string

@description('Cosmos DB account Resource Id')
param PcosmosDbAccountResourceId string
resource CosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2020-06-01-preview' = [for i in range(0,length(PcosmosDbDataRoleIds)): {
  name: '${PcosmosDbAccountName}/${guid('${PprincipleId}-${PappName}-${PcosmosDbDataRoleIds[i]}')}'
  properties: {
    roleDefinitionId: '${PcosmosDbAccountResourceId}/sqlRoleDefinitions/${PcosmosDbDataRoleIds[i]}'
    principalId: PprincipleId
    scope: PcosmosDbAccountResourceId
  }
}
]




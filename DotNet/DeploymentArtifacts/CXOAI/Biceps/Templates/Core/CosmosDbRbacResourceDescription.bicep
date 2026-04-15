param PfunctionResourceIdRef object
param PfunctionSlotResourceIdRef object
param PcosmosDbAccountName string 
param PcosmosDbDataRoleIds array 
param PfunctionName string

@description('Cosmos DB account Resource Id')
param PcosmosDbAccountResourceId string
var functionPrincipleId = PfunctionResourceIdRef.identity.principalId
resource CosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2020-06-01-preview' = [for i in range(0,length(PcosmosDbDataRoleIds)): {
  name: '${PcosmosDbAccountName}/${guid('${functionPrincipleId}-${PfunctionName}-${PcosmosDbDataRoleIds[i]}')}'
  properties: {
    roleDefinitionId: '${PcosmosDbAccountResourceId}/sqlRoleDefinitions/${PcosmosDbDataRoleIds[i]}'
    principalId: functionPrincipleId
    scope: PcosmosDbAccountResourceId
  }
}
]

var functionSlotPrincipleId = PfunctionSlotResourceIdRef.identity.principalId
resource SlotCosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2020-06-01-preview' = [for i in range(0,length(PcosmosDbDataRoleIds)): {
  name: '${PcosmosDbAccountName}/${guid('${functionSlotPrincipleId}-${PfunctionName}-${PcosmosDbDataRoleIds[i]}')}'
  properties: {
    roleDefinitionId: '${PcosmosDbAccountResourceId}/sqlRoleDefinitions/${PcosmosDbDataRoleIds[i]}'
    principalId: functionSlotPrincipleId
    scope: PcosmosDbAccountResourceId
  }
}
]


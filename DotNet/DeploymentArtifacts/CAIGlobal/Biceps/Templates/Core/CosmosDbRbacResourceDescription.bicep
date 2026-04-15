param PprincipleId string
param PcosmosDbAccountName string 
param PcosmosDbDataRoleIds array 
param PappName string
param PdeployRoleAssignment string
param PdeploySqlAssignment string
var deployRoleAssignment = bool(PdeployRoleAssignment)
var deploySqlAssignment = bool(PdeploySqlAssignment)
var cosmosDbAccountReadeRoleId= 'fbdf93bf-df7d-467e-a4d2-9458aa1360c8'

resource CosmoDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-12-01-preview' existing=  {
  name: PcosmosDbAccountName
}

resource CosmosAccountRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = if(deployRoleAssignment)  {
  name: guid('${PprincipleId}--${PcosmosDbAccountName}-${cosmosDbAccountReadeRoleId}')
  scope:CosmoDbAccount
  properties: {
    roleDefinitionId:  subscriptionResourceId('Microsoft.Authorization/roleDefinitions',cosmosDbAccountReadeRoleId)
    principalId: PprincipleId
	  principalType: 'ServicePrincipal'
  }
}


@description('Cosmos DB account Resource Id')
param PcosmosDbAccountResourceId string
resource CosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2020-06-01-preview' = [for i in range(0,length(PcosmosDbDataRoleIds)): if(deploySqlAssignment) {
  name: '${PcosmosDbAccountName}/${guid('${PprincipleId}-${PappName}-${PcosmosDbDataRoleIds[i]}')}'
  properties: {
    roleDefinitionId: '${PcosmosDbAccountResourceId}/sqlRoleDefinitions/${PcosmosDbDataRoleIds[i]}'
    principalId: PprincipleId
    scope: PcosmosDbAccountResourceId
  }
  dependsOn: [ CosmosAccountRoleAssignment ]
}
]




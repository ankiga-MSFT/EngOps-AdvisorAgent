@description('Cosmos DB account Resource Id')


param PresourceIdsPrincipleMappings array
param PdeployCosmosRoleAssignment string
var deployCosmosRoleAssignment = bool(PdeployCosmosRoleAssignment)

var jsonArray = PresourceIdsPrincipleMappings
var subscriptionIds = [for obj in jsonArray: deployCosmosRoleAssignment ? split(obj.cosmosDbAccountResourceId, '/')[2] : '']
var resourceGroupNames = [for obj in jsonArray: deployCosmosRoleAssignment ? split(obj.cosmosDbAccountResourceId, '/')[4] : '']
var CosmosDbAccountNames = [for obj in jsonArray: deployCosmosRoleAssignment ? split(obj.cosmosDbAccountResourceId, '/')[8] : '']



// var cosmosDbDataReaderRoleId = '00000000-0000-0000-0000-000000000001'
// var cosmosDbDataContributorRoleId = '00000000-0000-0000-0000-000000000002'

@batchSize(1)
module grantCosmosDbRBACToFunc 'Core/CosmosDbRbacResourceDescription.bicep' = [for (obj,i) in jsonArray: if(deployCosmosRoleAssignment) {
  name: 'FunctionCosmosDbRBAC-${guid(CosmosDbAccountNames[i],obj.appName,obj.roleDefinitionId)}'
  scope: resourceGroup(subscriptionIds[i],resourceGroupNames[i])
  params: {
    PprincipleId: obj.principleId
    PcosmosDbAccountName: CosmosDbAccountNames[i]
    PcosmosDbDataRoleIds: [
      obj.roleDefinitionId
    ]
    PappName: obj.appName
    PdeploySqlAssignment: obj.deploySqlAssignment
    PdeployRoleAssignment: obj.deployRoleAssignment
    PcosmosDbAccountResourceId: obj.cosmosDbAccountResourceId
  }
}
]


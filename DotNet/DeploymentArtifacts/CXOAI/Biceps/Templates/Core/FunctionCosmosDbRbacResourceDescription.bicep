@description('Cosmos DB account Resource Id')
param PcommerialCosmosDbAccountResourceId string
param PfunctionName string
param PfunctionRGName string
param PfunctionSubscriptionId string
param PdeployCosmosResources string 
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var deployCosmosResources =bool(PdeployCosmosResources) 

var cosmosDbDataReaderRoleId = '00000000-0000-0000-0000-000000000001'
var cosmosDbDataContributorRoleId = '00000000-0000-0000-0000-000000000002'
var CosmosDbAccountResourceGroup = split(PcommerialCosmosDbAccountResourceId, '/')[4]
var CosmosDbAccountName = split(PcommerialCosmosDbAccountResourceId, '/')[8]
var cosmosDbSubscriptionId = split(PcommerialCosmosDbAccountResourceId, '/')[2]
module grantCosmosDbRBACToFunc './CosmosDbRbacResourceDescription.bicep' = if(deployCosmosResources && deployInfra) {
  name: 'FunctionCosmosDbRBAC-${guid(CosmosDbAccountName,PfunctionName,cosmosDbDataContributorRoleId,cosmosDbDataReaderRoleId)}'
  scope: resourceGroup(cosmosDbSubscriptionId,CosmosDbAccountResourceGroup)
  params: {
    PfunctionResourceIdRef: reference(
      '/subscriptions/${PfunctionSubscriptionId}/resourceGroups/${PfunctionRGName}/providers/Microsoft.Web/sites/${PfunctionName}',
      '2019-08-01',
      'full'
    )

   PfunctionSlotResourceIdRef: reference(
    '/subscriptions/${PfunctionSubscriptionId}/resourceGroups/${PfunctionRGName}/providers/Microsoft.Web/sites/${PfunctionName}/slots/staging',
    '2019-08-01',
    'full'
  )

    

    // /subscriptions/fa5349cd-ab3c-4232-88e5-8b842489a230/resourceGroups/rg-policyeng-test-westus3/providers/Microsoft.Web/sites/fun-policyeng-test-westus3/slots/staging
   //  /subscriptions/${PfunctionSubscriptionId}/resourceGroups/${PfunctionRGName}/providers/Microsoft.Web/sites/${PfunctionName}/slots/staging

    PcosmosDbAccountName: CosmosDbAccountName
    PcosmosDbDataRoleIds: [
      cosmosDbDataReaderRoleId
      cosmosDbDataContributorRoleId
    ]
    PfunctionName: PfunctionName
    PcosmosDbAccountResourceId: PcommerialCosmosDbAccountResourceId
  }
}


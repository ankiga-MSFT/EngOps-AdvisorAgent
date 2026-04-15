param PfunctionResourceIdRef object
param PfunctionSlotResourceIdRef object
param PresourceName string
param ProleDefinationIds array
param PfunctionName string


resource searchService 'Microsoft.Search/searchServices@2024-06-01-Preview' existing = {
  name: PresourceName
}


@description('Storage account Resource Id')
var principleid = PfunctionResourceIdRef.identity.principalId
@batchSize(1)
resource searchServiceRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = [
  for i in range(0, length(ProleDefinationIds)): {
    name: guid('${principleid}-${PresourceName}-${PfunctionName}-${ProleDefinationIds[i]}')
    scope: searchService
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ProleDefinationIds[i])
      principalId: principleid
      principalType: 'ServicePrincipal'
    }
    dependsOn: [searchService]
  }
]



var slotprincipleid = PfunctionSlotResourceIdRef.identity.principalId
@batchSize(1)
resource SlotSearchServiceRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = [
  for i in range(0, length(ProleDefinationIds)): {
    name: guid('${slotprincipleid}-${PresourceName}-${PfunctionName}-${ProleDefinationIds[i]}')
    scope: searchService
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ProleDefinationIds[i])
      principalId: slotprincipleid
      principalType: 'ServicePrincipal'
    }
    dependsOn: [searchServiceRoleAssignment]
  }
]

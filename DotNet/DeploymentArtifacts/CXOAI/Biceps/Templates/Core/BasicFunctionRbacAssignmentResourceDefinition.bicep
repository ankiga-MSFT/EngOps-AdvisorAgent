param PfunctionResourceIdRef object
param PfunctionSlotResourceIdRef object
param PresourceName string
param ProleDefinationIds array
param PfunctionName string
@description('Storage account Resource Id')
var principleid = PfunctionResourceIdRef.identity.principalId
@batchSize(1)
resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = [
  for i in range(0, length(ProleDefinationIds)): {
    name: guid('${principleid}-${PresourceName}-${PfunctionName}-${ProleDefinationIds[i]}')
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ProleDefinationIds[i])
      principalId: principleid
      principalType: 'ServicePrincipal'
    }
  }
]

var slotprincipleid = PfunctionSlotResourceIdRef.identity.principalId
@batchSize(1)
resource SlotstorageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = [
  for i in range(0, length(ProleDefinationIds)): {
    name: guid('${slotprincipleid}-${PresourceName}-${PfunctionName}-${ProleDefinationIds[i]}')
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ProleDefinationIds[i])
      principalId: slotprincipleid
      principalType: 'ServicePrincipal'
    }
    dependsOn: [storageRoleAssignment]
  }
]

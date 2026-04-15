param PsourceResourceIdRef object
param PsourceResourceName string 
param ProleDefinationIds array 
param PtargetResourceName string
@description('Storage account Resource Id')

var principleid=PsourceResourceIdRef.properties.principalId
resource RoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = [for i in range(0,length(ProleDefinationIds)): {
  name: guid('${principleid}-${PsourceResourceName}-${PtargetResourceName}-${ProleDefinationIds[i]}')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',ProleDefinationIds[i])
    principalId: principleid
	principalType: 'ServicePrincipal'
  }
}
]






param PadfName string
param PresourceIdentityId string
param Pdeploy string
var deploy= bool(Pdeploy)
resource Adf 'Microsoft.DataFactory/factories@2018-06-01' existing=if(deploy) {
  name: PadfName
  scope: resourceGroup()
}

var adfRoleDefinationId  ='b24988ac-6180-42a0-ab88-20f7382dd24c'

resource rbacAdf  'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = if(deploy){
  name: guid('${Adf.id}-${PresourceIdentityId}',adfRoleDefinationId)
  scope: Adf
  properties: {
    principalId: PresourceIdentityId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',adfRoleDefinationId)
  }
}


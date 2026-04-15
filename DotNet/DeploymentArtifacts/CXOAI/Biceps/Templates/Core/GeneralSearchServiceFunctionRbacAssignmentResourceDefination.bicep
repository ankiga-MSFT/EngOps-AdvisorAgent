param PResourceId string
param ProleDefinationIds array
param PfunctionRGName string
param PfunctionSubscriptionId string
param PfunctionName string
param PdeployRoleAssignments string
var deployRoleAssignments=bool(PdeployRoleAssignments)
var resourceSubscriptionId = split(PResourceId, '/')[2]
var resourceResourceGroup = split(PResourceId, '/')[4]
var resourceName = split(PResourceId, '/')[8]
module grantStorageAccountRBACToFunc 'SearchServiceFunctionRbacAssignmentResourceDefinition.bicep'= if(deployRoleAssignments) {
  name:'ResourceRbacAssignment-${guid(resourceName,PfunctionName)}'
  scope:resourceGroup(resourceSubscriptionId,resourceResourceGroup)
  params:{
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
    PresourceName:resourceName
    ProleDefinationIds:ProleDefinationIds
    PfunctionName:PfunctionName
  }
}




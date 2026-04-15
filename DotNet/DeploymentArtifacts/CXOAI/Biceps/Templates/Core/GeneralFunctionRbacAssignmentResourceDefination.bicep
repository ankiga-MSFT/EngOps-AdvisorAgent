param PResourceId string
param ProleDefinationIds array
param PfunctionRGName string
param PfunctionSubscriptionId string
param PfunctionName string
param PdeployRoleAssignments string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var deployRoleAssignments=bool(PdeployRoleAssignments)
var resourceResourceGroup = split(PResourceId, '/')[4]
var resourceName = split(PResourceId, '/')[8]
var subscriptionid=split(PResourceId, '/')[2]
module grantStorageAccountRBACToFunc 'BasicFunctionRbacAssignmentResourceDefinition.bicep'= if(deployRoleAssignments && deployInfra)  {
  name:'ResourceRbacAssignment-${guid(resourceName,PfunctionName)}'
  scope:resourceGroup(subscriptionid,resourceResourceGroup)
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




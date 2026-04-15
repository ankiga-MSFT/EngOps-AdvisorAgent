param PtargetResourceId string
param ProleDefinationIds array
param PsourceRGName string
param PsourceResourceSubscriptionId string
param PsourceResourceName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var targetResourceResourceGroup = split(PtargetResourceId, '/')[4]
var targetResourceName = split(PtargetResourceId, '/')[8]
module grantStorageAccountRBACToFunc 'BaseRbacAssignmentResourceDefinition.bicep'= if(deployInfra) {
  name:'StorageRbacAssignment-${guid(targetResourceName,PsourceResourceName)}'
  scope:resourceGroup(targetResourceResourceGroup)
  params:{
    PsourceResourceIdRef: reference(
        '/subscriptions/${PsourceResourceSubscriptionId}/resourceGroups/${PsourceRGName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/${PsourceResourceName}',
        '2023-01-31',
        'full'
      )
     PtargetResourceName: targetResourceName
    ProleDefinationIds:ProleDefinationIds
    PsourceResourceName:PsourceResourceName
  }
}




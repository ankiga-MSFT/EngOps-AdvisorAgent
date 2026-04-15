param PRoleDefinationIds array
param PfunctionName string
param PfunctionRGName string
param PfunctionSubscriptionId string
param PResourceIds array 
param PdeployRoleAssignments string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var resourceGroups=[for (resourceId,i) in PResourceIds: split(resourceId,'/')[4] ]
var subscriptionids=[for (resourceId,i) in PResourceIds: split(resourceId,'/')[2] ]

@batchSize(1)
module multiRBACToFunc 'Core/GeneralSearchServiceFunctionRbacAssignmentResourceDefination.bicep'= [for (resourceId,i) in PResourceIds: if(deployInfra)  {
  name:'multiRBACToFunc-${guid(resourceId,PfunctionName)}'
  scope:resourceGroup(subscriptionids[i],resourceGroups[i])
  params:{
    PfunctionName:PfunctionName
    PResourceId:resourceId
    PfunctionRGName:PfunctionRGName
    PfunctionSubscriptionId:PfunctionSubscriptionId
    PdeployRoleAssignments:PdeployRoleAssignments
    ProleDefinationIds:PRoleDefinationIds
  }
}
]

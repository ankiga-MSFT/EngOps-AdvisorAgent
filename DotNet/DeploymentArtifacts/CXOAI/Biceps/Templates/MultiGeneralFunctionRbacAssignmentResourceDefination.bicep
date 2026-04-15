param PRoleDefinationIds array
param PfunctionName string
param PfunctionRGName string
param PfunctionSubscriptionId string
param PResourceIds array 
param PdeployRoleAssignments string
var resourceGroups=[for (resourceId,i) in PResourceIds: split(resourceId,'/')[4] ]
var subscriptionids=[for (resourceId,i) in PResourceIds: split(resourceId,'/')[2] ]

@batchSize(1)
module multiRBACToFunc 'Core/GeneralFunctionRbacAssignmentResourceDefination.bicep'= [for (resourceId,i) in PResourceIds:  {
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

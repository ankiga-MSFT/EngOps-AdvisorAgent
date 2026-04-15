
param PkustoClusterResourceId string

param PkustoResourceIdsPrincipleMappings array

param PdeployKustoRoleAssignment string
var deployKustoRoleAssignment = bool(PdeployKustoRoleAssignment)
var jsonArray = PkustoResourceIdsPrincipleMappings
var subscriptionId = deployKustoRoleAssignment ? split(PkustoClusterResourceId, '/')[2] : ''
var resourceGroupName = deployKustoRoleAssignment ? split(PkustoClusterResourceId, '/')[4] : ''
var clusterName = deployKustoRoleAssignment ? split(PkustoClusterResourceId, '/')[8] : ''

@batchSize(1)
module KustoRbacResourceDefination 'KustoRbacResourceDefination.bicep' = [for (obj,i) in jsonArray: if(deployKustoRoleAssignment){
  name: 'KustoRbacResourceDefination-${i}'
  scope: resourceGroup(subscriptionId,resourceGroupName)
  params: {
    PclusterName:clusterName
    PkustoDatabaseName :obj.kustoDatabaseName
    Pprincipleid :obj.principleid
    Ptenantid :obj.tenantid
    Prole :obj.role //Admin, viewer
    PprincipleType :obj.principleType //App, User, Group,
  }
}]



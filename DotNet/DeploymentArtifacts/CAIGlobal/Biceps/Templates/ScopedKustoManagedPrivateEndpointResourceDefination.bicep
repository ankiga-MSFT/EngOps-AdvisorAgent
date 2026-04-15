param pkustoManagedPrivateEndpointConnectionMapping array
param PdeployKustoManagedPrivateEndpoint string
var deployManagedPrivateEndpoint = bool(PdeployKustoManagedPrivateEndpoint)
var jsonArray = pkustoManagedPrivateEndpointConnectionMapping

param PkustoClusterResourceId string
var subscriptionId = deployManagedPrivateEndpoint ? split(PkustoClusterResourceId, '/')[2] : ''
var resourceGroupName = deployManagedPrivateEndpoint ?  split(PkustoClusterResourceId, '/')[4] : ''
var clusterName = deployManagedPrivateEndpoint ? split(PkustoClusterResourceId, '/')[8] : ''

@batchSize(1)
module ScopedKustoManagedPrivateEndpoints 'Core/KustoManagedPrivateEndpointResourceDefination.bicep'= [for (obj,i) in jsonArray: if (deployManagedPrivateEndpoint) {
  name: 'ScopedKustoManagedPrivateEndpoints-${i}'
  scope: resourceGroup(subscriptionId,resourceGroupName)
   params: {
    PKustoClusterName:clusterName
    pkustoPrivateEndpointConnectionMapping:obj
}
}]

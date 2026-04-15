param PkustoDataConnectionsMappings array
param PdeployKustoDataConnection string
var deployKustoDataConnection = bool(PdeployKustoDataConnection)
var jsonArray = PkustoDataConnectionsMappings

param PkustoClusterResourceId string
var subscriptionId = deployKustoDataConnection ? split(PkustoClusterResourceId, '/')[2] : ''
var resourceGroupName = deployKustoDataConnection ?  split(PkustoClusterResourceId, '/')[4] : ''
var clusterName =  deployKustoDataConnection ? split(PkustoClusterResourceId, '/')[8] : ''

@batchSize(1)
module ScopedKustoDataConnections 'Core/KustoDirectIngestionResourceDefination.bicep'= [for (obj,i) in jsonArray: if (deployKustoDataConnection) {
  name: 'ScopedKustoDataConnections-${i}'
  scope: resourceGroup(subscriptionId,resourceGroupName)
   params: {
    PKustoClusterName:clusterName
    pkustoDataConnectionMapping:obj
     PkustoResourceId: PkustoClusterResourceId
}
}]

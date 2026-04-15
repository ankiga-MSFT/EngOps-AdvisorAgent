param PKustoClusterName string
param pkustoPrivateEndpointConnectionMapping object
var obj = pkustoPrivateEndpointConnectionMapping


resource KustoCluster 'Microsoft.Kusto/Clusters@2024-04-13' existing = {
  name: PKustoClusterName
}


resource ClustersCosmosDbManagedPrivateEndpoint 'Microsoft.Kusto/Clusters/ManagedPrivateEndpoints@2024-04-13' = if(obj.kustoConnectionKind == 'CosmosDb') {
  parent: KustoCluster
  name: obj.managePrivateEndpointName
  properties: {
    privateLinkResourceId: obj.resourceId
    groupId: 'SQL'
    requestMessage: obj.message
  }
}

resource ClustersStorageManagedPrivateEndpoint 'Microsoft.Kusto/Clusters/ManagedPrivateEndpoints@2024-04-13' = if(obj.kustoConnectionKind == 'Storage')  {
  parent: KustoCluster
  name: obj.managePrivateEndpointName
  properties: {
    privateLinkResourceId: obj.resourceId
    groupId: 'blob'
    requestMessage: obj.message
  }
}

resource ClustersKustoManagedPrivateEndpoint 'Microsoft.Kusto/Clusters/ManagedPrivateEndpoints@2024-04-13' = if(obj.kustoConnectionKind == 'Kusto')  {
  parent: KustoCluster
  name: obj.managePrivateEndpointName
  properties: {
    privateLinkResourceId: obj.resourceId
    privateLinkResourceRegion: 'WestUS3'
    groupId: 'cluster'
    requestMessage: obj.message
  }
}

resource ClustersEventhubNamespaceManagedPrivateEndpoint 'Microsoft.Kusto/Clusters/ManagedPrivateEndpoints@2024-04-13' = if(obj.kustoConnectionKind == 'EventHubNamespace')  {
  parent: KustoCluster
  name: obj.managePrivateEndpointName
  properties: {
    privateLinkResourceId: obj.resourceId
    groupId: 'namespace'
    requestMessage: obj.message
  }
}






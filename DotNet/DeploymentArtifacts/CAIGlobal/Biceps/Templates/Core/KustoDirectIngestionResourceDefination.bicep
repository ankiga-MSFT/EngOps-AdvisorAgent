param PKustoClusterName string
param PkustoResourceId string
param pkustoDataConnectionMapping object
var obj = pkustoDataConnectionMapping


resource KustoCluster 'Microsoft.Kusto/Clusters@2024-04-13' existing = {
  name: PKustoClusterName
}


resource KustoClusterDatabase 'Microsoft.Kusto/Clusters/Databases@2024-04-13' existing= {
  parent: KustoCluster
  name: obj.kustoDatabaseName
}


resource KustoClusterDatabaseCosmosDataConnection 'Microsoft.Kusto/Clusters/Databases/DataConnections@2024-04-13' = if(obj.kustoConnectionKind == 'CosmosDb') {
  parent: KustoClusterDatabase
  name: obj.kustoDataConnectionName
  location: 'West US 3'
  kind: obj.kustoConnectionKind
  properties: {
    cosmosDbAccountResourceId: obj.cosmosDbAccountResourceId
    cosmosDbContainer: obj.cosmosDbContainer
    cosmosDbDatabase: obj.cosmosDbDatabase
    managedIdentityResourceId: PkustoResourceId
    mappingRuleName: obj.mappingRuleName
    retrievalStartDate: obj.retrievalStartDate
    tableName: obj.tableName
  }
  dependsOn: [
    KustoCluster
  ]
}

resource KustoClusterDatabaseEventHubDataConnection 'Microsoft.Kusto/Clusters/Databases/DataConnections@2024-04-13' = if(obj.kustoConnectionKind == 'EventHub') {
  parent: KustoClusterDatabase
  name: obj.kustoDataConnectionName
  location: 'West US 3'
  kind: obj.kustoConnectionKind
  properties: {
    consumerGroup: obj.eventhubConsumerGroup
    dataFormat: 'MULTIJSON'
    eventHubResourceId: obj.eventHubResourceId
    managedIdentityResourceId: PkustoResourceId
    mappingRuleName: obj.mappingRuleName
    retrievalStartDate: obj.retrievalStartDate
    tableName: obj.tableName
  }
  dependsOn: [
    KustoCluster
  ]
}









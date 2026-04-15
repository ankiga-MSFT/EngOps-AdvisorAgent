param PcosmosDbccountsName string 
param PcosmosdbDatabases array
param PcosmosdbDatabaseCollectionsThroughputMapping array
param PdeployCosmosDbAccount string
param PcosmosLocation string
param PcosmosGeoRepLocations array
param PCosmosBackupPolicyTier string
param PvectorEmbeddingCollections array = []
var deployCosmosDbAccount = bool(PdeployCosmosDbAccount)
module ScopedCosmosDbResourceDefination 'CosmosDbResourceDefination.bicep'= if (deployCosmosDbAccount) {
  name: 'ScopedCosmosDbResourceDefination'
   params: {
    PcosmosDbccountsName: PcosmosDbccountsName
    PcosmosdbDatabaseCollectionsThroughputMapping: PcosmosdbDatabaseCollectionsThroughputMapping
    PcosmosdbDatabases: PcosmosdbDatabases
    PCosmosLocation:PcosmosLocation      
    PcosmosGeoRepLocations: PcosmosGeoRepLocations 
    PCosmosBackupPolicyTier:PCosmosBackupPolicyTier
    PvectorEmbeddingCollections:PvectorEmbeddingCollections
}
}

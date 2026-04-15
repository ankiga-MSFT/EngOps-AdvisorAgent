param PcosmosDbccountsName string 
param PcosmosdbDatabases array
param PcosmosdbDatabaseCollectionsThroughputMapping array
param PcosmosGeoRepLocations array
param PCosmosLocation string
param PCosmosBackupPolicyTier string
param PvectorEmbeddingCollections array = []
resource CosmoDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-12-01-preview' =  {
  name: PcosmosDbccountsName
  location: PCosmosLocation
  tags: {
    defaultExperience: 'DocumentDB'
  }
  kind: 'GlobalDocumentDB'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publicNetworkAccess: 'Disabled'
    enableAutomaticFailover: true
    enableMultipleWriteLocations: false
    isVirtualNetworkFilterEnabled: true
    disableKeyBasedMetadataWriteAccess: false
    enableFreeTier: false
    enableAnalyticalStorage: true
    analyticalStorageConfiguration: {
      schemaType: 'WellDefined'
    }
    databaseAccountOfferType: 'Standard'
    enableMaterializedViews: false
    capacityMode: 'Provisioned'
    defaultIdentity: 'FirstPartyIdentity'
    networkAclBypass: 'None'
    disableLocalAuth: true
    enablePartitionMerge: false
    enablePerRegionPerPartitionAutoscale: false
    enableBurstCapacity: false
    enablePriorityBasedExecution: true
    defaultPriorityLevel: 'High'
    minimalTlsVersion: 'Tls12'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    locations: PcosmosGeoRepLocations
    cors: []
    capabilities: []
      
    backupPolicy: {
      type:'Continuous'
      continuousModeProperties: {
        tier:PCosmosBackupPolicyTier
      }
    }

    networkAclBypassResourceIds: []
    diagnosticLogSettings: {
      enableFullTextQuery: 'None'
    }
    capacity: {
      totalThroughputLimit: -1
    }
  }
}

@batchSize(1)
resource CosmoDbDatabases 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-12-01-preview' =[for database in PcosmosdbDatabases: {
  parent: CosmoDbAccount
  name: database
  properties: {
    resource: {
      id: database
    }
  }
}
]



resource CosmosBuildInSqlDataReaderRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2024-12-01-preview' = {
  parent: CosmoDbAccount
  name: '00000000-0000-0000-0000-000000000001'
  properties: {
    roleName: 'Cosmos DB Built-in Data Reader'
    type: 'BuiltInRole'
    assignableScopes: [
      CosmoDbAccount.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/executeQuery'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/readChangeFeed'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/read'
        ]
        notDataActions: []
      }
    ]
  }
}

resource CosmosBuildInSqlDataContributorRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2024-12-01-preview' = {
  parent: CosmoDbAccount
  name: '00000000-0000-0000-0000-000000000002'
  properties: {
    roleName: 'Cosmos DB Built-in Data Contributor'
    type: 'BuiltInRole'
    assignableScopes: [
      CosmoDbAccount.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/*'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/*'
        ]
        notDataActions: []
      }
    ]
  }
}

resource CosmosBuildInDataTableReaderRole 'Microsoft.DocumentDB/databaseAccounts/tableRoleDefinitions@2024-12-01-preview' = {
  parent: CosmoDbAccount
  name: '00000000-0000-0000-0000-000000000001'
  properties: {
    roleName: 'Cosmos DB Built-in Data Reader'
    type: 'BuiltInRole'
    assignableScopes: [
      CosmoDbAccount.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/executeQuery'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/readChangeFeed'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/entities/read'
        ]
        notDataActions: []
      }
    ]
  }
}

resource CosmosBuildInDataTableContributorRole 'Microsoft.DocumentDB/databaseAccounts/tableRoleDefinitions@2024-12-01-preview' = {
  parent: CosmoDbAccount
  name: '00000000-0000-0000-0000-000000000002'
  properties: {
    roleName: 'Cosmos DB Built-in Data Contributor'
    type: 'BuiltInRole'
    assignableScopes: [
      CosmoDbAccount.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/tables/*'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/*'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/entities/*'
        ]
        notDataActions: []
      }
    ]
  }
}


var jsonArray = PcosmosdbDatabaseCollectionsThroughputMapping

resource CosmoDbDatabasesCollections 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2025-04-15' =[for obj in jsonArray: {
  name: obj.collectionFullName
  properties: {
    resource: {
      id: obj.collectionName
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/${obj.partitionKey}'
        ]
        kind: 'Hash'
      }
      defaultTtl: obj.defaultTtl
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      
    }
  }
  dependsOn: [
    CosmoDbDatabases
  ]
}
]


resource CosmoDbDatabasesCollectionsThroughput 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/throughputSettings@2025-04-15' =[for obj in jsonArray: {
  name: '${obj.collectionFullName}/default'
  properties: {
    resource: {
      autoscaleSettings: {
        maxThroughput: obj.maxThroughput
      }
    }
  }
  dependsOn: [
    CosmoDbDatabasesCollections
  ]
}]

// Vector embedding enabled collections
var vectorArray = PvectorEmbeddingCollections

@batchSize(1)
resource CosmoDbVectorCollections 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2025-11-01-preview' = [for obj in vectorArray: {
  name: obj.collectionFullName
  properties: {
    resource: {
      id: obj.collectionName
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
          {
            path: '${obj.embeddingPath}/*'
          }
        ]
        fullTextIndexes: []
        vectorIndexes: [
          {
            path: obj.embeddingPath
            type: obj.vectorIndexType
            quantizationByteSize: obj.quantizationByteSize
          }
        ]
      }
      partitionKey: {
        paths: [
          '/${obj.partitionKey}'
        ]
        kind: 'Hash'
        version: 2
      }
      defaultTtl: obj.defaultTtl
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      vectorEmbeddingPolicy: {
        vectorEmbeddings: [
          {
            path: obj.embeddingPath
            dataType: obj.embeddingDataType
            dimensions: obj.embeddingDimensions
            distanceFunction: obj.embeddingDistanceFunction
          }
        ]
      }
      fullTextPolicy: {
        defaultLanguage: 'en-US'
        fullTextPaths: []
      }
      computedProperties: []
    }
  }
  dependsOn: [
    CosmoDbDatabases
  ]
}]

resource CosmoDbVectorCollectionsThroughput 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/throughputSettings@2025-04-15' = [for obj in vectorArray: {
  name: '${obj.collectionFullName}/default'
  properties: {
    resource: {
      autoscaleSettings: {
        maxThroughput: obj.maxThroughput
      }
    }
  }
  dependsOn: [
    CosmoDbVectorCollections
  ]
}]

//"[concat(parameters('databaseAccounts_cosmos_cxpes_test_sdp_cm_name'), '/RawData/CGAAtRiskModelSupportData')]",

// resource databaseAccounts_cosmos_cxpes_test_sdp_cm_name_CxobserveCopilot_default 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/throughputSettings@2024-12-01-preview' = {
//   parent: databaseAccounts_cosmos_cxpes_test_sdp_cm_name_CxobserveCopilot
//   name: 'default'
//   properties: {
//     resource: {
//       throughput: 400
//       autoscaleSettings: {
//         maxThroughput: 4000
//       }
//     }
//   }
//   dependsOn: [
//     CosmoDbDatabasesCollections
//   ]
// }




// resource CosmosDbAccountSqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-12-01-preview' = {
//   parent: CosmoDbAccount
//   name: '0009dbb2-9952-5735-a971-c96c9f8086ca'
//   properties: {
//     roleDefinitionId: databaseAccounts_cosmos_cxpes_test_sdp_cm_name_00000000_0000_0000_0000_000000000002.id
//     principalId: 'e327c8da-54b0-4deb-8d6f-e963dc1a1dfc'
//     scope: CosmoDbAccount.id
//   }
// }



// resource Microsoft_DocumentDB_databaseAccounts_tableRoleAssignments_databaseAccounts_cosmos_cxpes_test_sdp_cm_name_0009dbb2_9952_5735_a971_c96c9f8086ca 'Microsoft.DocumentDB/databaseAccounts/tableRoleAssignments@2024-12-01-preview' = {
//   parent: CosmoDbAccount
//   name: '0009dbb2-9952-5735-a971-c96c9f8086ca'
//   properties: {
//     roleDefinitionId: Microsoft_DocumentDB_databaseAccounts_tableRoleDefinitions_databaseAccounts_cosmos_cxpes_test_sdp_cm_name_00000000_0000_0000_0000_000000000002.id
//     principalId: 'e327c8da-54b0-4deb-8d6f-e963dc1a1dfc'
//     scope: CosmoDbAccount.id
//   }
// }




// resource CosmosDbDatabaseStoredProcedure 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/storedProcedures@2024-12-01-preview' = {
//   parent: databaseAccounts_cosmos_cxpes_test_sdp_cm_name_RawData_DynamicsSupportCases
//   name: 'test'
//   properties: {
//     resource: {
//       id: 'test'
//       body: '// SAMPLE STORED PROCEDURE\nfunction sample(prefix) {\n    var collection = getContext().getCollection();\n\n    // Query documents and take 1st item.\n    var isAccepted = collection.queryDocuments(\n        collection.getSelfLink(),\n        \'SELECT * FROM root r\',\n    function (err, feed, options) {\n        if (err) throw err;\n\n        // Check the feed and if empty, set the body to \'no docs found\', \n        // else take 1st element from feed\n        if (!feed || !feed.length) {\n            var response = getContext().getResponse();\n            response.setBody(\'no docs found\');\n        }\n        else {\n            var response = getContext().getResponse();\n            var body = { prefix: prefix, feed: feed[0] };\n            response.setBody(JSON.stringify(body));\n        }\n    });\n\n    if (!isAccepted) throw new Error(\'The query was not accepted by the server.\');\n}'
//     }
//   }
//   dependsOn: [
//     CosmoDbDatabasesCollections
//   ]
// }

# how to set up this project
1. create a secrets.json file, make sure you ignore it before commit, and add the following content to it:
```json
{
   "AzureEndpoint": "https://aoi-cxpes-test-llm-1.openai.azure.com/"
}```
2. make sure you have cognative contributor access the Azure OpenAI resource, and get the key from Azure Portal, then add it to secrets.json like below:
# reun below command from CXOAI folder in terminal
dotnet user-secrets init --project CXOAI.csproj
type secrets.json | dotnet user-secrets set --project CXOAI.csproj

# mem0 implementation cosmos as backend bicep template
```// Vector-enabled container for mem0-style long-term memory
resource MemoryFactsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: CosmoDbDatabases[0]  // your existing database
  name: 'memoryFacts'
  properties: {
    resource: {
      id: 'memoryFacts'
      partitionKey: {
        paths: ['/userId']
        kind: 'Hash'
        version: 2
      }
      indexingPolicy: {
        automatic: true
        includedPaths: [{ path: '/*' }]
        excludedPaths: [{ path: '/embedding/*' }]
        vectorIndexes: [
          {
            path: '/embedding'
            type: 'quantizedFlat'  // best for <10K vectors per partition (per-user facts)
          }
        ]
      }
      vectorEmbeddingPolicy: {
        vectorEmbeddings: [
          {
            path: '/embedding'
            dataType: 'float32'
            dimensions: 512            // text-embedding-3-small @ 512 dims
            distanceFunction: 'cosine'
          }
        ]
      }
      defaultTtl: -1  // no auto-expiry; managed by the memory store
    }
  }
}```


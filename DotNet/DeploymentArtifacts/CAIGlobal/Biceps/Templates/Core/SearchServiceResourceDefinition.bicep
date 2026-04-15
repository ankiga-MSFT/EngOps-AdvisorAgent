param PsearchServiceName string
param Psku string
param PreplicaCount string
param PpartitionCount string
param Plocation string 
var replicaCount=int(PreplicaCount)
var partitionCount=int(PpartitionCount)
resource searchService 'Microsoft.Search/searchServices@2024-06-01-Preview' = {
  name: PsearchServiceName
  location: Plocation
  sku: {
    name: Psku
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    replicaCount: replicaCount
    partitionCount: partitionCount
    hostingMode: 'default'
    disableLocalAuth: true
    publicNetworkAccess: 'enabled'
    networkRuleSet: {
      ipRules: []
      bypass: 'AzureServices'
    }
    encryptionWithCmk: {
      enforcement: 'Unspecified'
    }
    // authOptions: {
    //   aadOrApiKey: {
    //     aadAuthFailureMode: 'http401WithBearerChallenge'
    //   }
    // }
    disabledDataExfiltrationOptions: []
    semanticSearch: 'standard'
  }
}


param PstorageSystemTopicsName string
param PstorageResourceId string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
resource PstorageEventGridSystemTopicsName 'Microsoft.EventGrid/systemTopics@2024-06-01-preview' = if (deployInfra) {
  name: PstorageSystemTopicsName
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    source: PstorageResourceId
    topicType: 'microsoft.storage.storageaccounts'
  }
}

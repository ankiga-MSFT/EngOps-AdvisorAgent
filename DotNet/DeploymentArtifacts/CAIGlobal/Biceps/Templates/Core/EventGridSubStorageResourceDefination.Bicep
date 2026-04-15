param PcontainerName string
param PeventGridSubscriptionName string
param PfunctionAppName string
param PfunctionName string
param PeventGridTopicName string
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' existing = {
  name: PcontainerName
}

resource systemTopic 'Microsoft.EventGrid/systemTopics@2023-12-15-preview' = {
  name: PeventGridTopicName
  location: resourceGroup().location
  properties: {
    source: blobContainer.id
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
  identity: {
    type: 'SystemAssigned'
  }
}


resource eventSubscription  'Microsoft.EventGrid/eventSubscriptions@2022-06-15' = {
  name: PeventGridSubscriptionName
  scope: systemTopic
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        resourceId: resourceId('Microsoft.Web/sites/functions', PfunctionAppName, PfunctionName)
      }
    }
    filter: {
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
      ]
    }
  }
}
 var RoleIdForAzureFunctionContributor= '4d97b98b-1d4f-4787-a291-c67834d212e7'
 //var functionAppResourceId = resourceId('Microsoft.Web/sites', PfunctionAppName)
 var functionResourceId = resourceId('Microsoft.Web/sites/functions', PfunctionAppName, PfunctionName)
 var functionName = last(split(functionResourceId, '/'))

 resource functionResource 'Microsoft.Web/sites@2021-02-01' existing = {
  name: functionName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(systemTopic.id, 'Azure Function Contributor')
  scope: functionResource
    properties: {
    roleDefinitionId: RoleIdForAzureFunctionContributor
    principalId: systemTopic.identity.principalId
  }
}



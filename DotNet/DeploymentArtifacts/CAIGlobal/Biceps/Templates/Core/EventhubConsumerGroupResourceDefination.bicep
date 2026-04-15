param PeventHubNamespaceName string
param PeventHubName string
param PdefaultConsumerGroupNames array

resource EventHubNamespace 'Microsoft.EventHub/namespaces@2024-05-01-preview' existing=  {
  name: PeventHubNamespaceName
}


resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' existing =  {
  parent: EventHubNamespace
  name: PeventHubName
}
@batchSize(1)
resource consumerGroups 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2021-11-01' = [for (consumergroup,i) in PdefaultConsumerGroupNames: {
  parent: eventHub
  name: consumergroup
  properties: {}
}]

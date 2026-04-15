param PeventHubNameResourceIdsConsumerMappings array
param PdeployEventhubConsumerGroup string
var deployeventhubConsumerGroup = bool(PdeployEventhubConsumerGroup)
var jsonArray = PeventHubNameResourceIdsConsumerMappings
var subscriptionIds =   [for obj in jsonArray: deployeventhubConsumerGroup ? split(obj.eventhubResourceId, '/')[2] : '']
var resourceGroupNames = [for obj in jsonArray:deployeventhubConsumerGroup ?  split(obj.eventhubResourceId, '/')[4]: '']
var eventhubNamespaces = [for obj in jsonArray:deployeventhubConsumerGroup ?  split(obj.eventhubResourceId, '/')[8]: '']
var eventhubs = [for obj in jsonArray: deployeventhubConsumerGroup ? split(obj.eventhubResourceId, '/')[10]: '']

@batchSize(1)
module ScopedEventhubConsumerGroup 'Core/EventhubConsumerGroupResourceDefination.bicep'= [for (obj,i) in jsonArray: if (deployeventhubConsumerGroup) {
  name: 'ScopedEventhubConsumerGroup-${i}'
  scope: resourceGroup(subscriptionIds[i],resourceGroupNames[i])
   params: {
    PeventHubNamespaceName: eventhubNamespaces[i]
    PeventHubName: eventhubs[i]
    PdefaultConsumerGroupNames: [obj.consumergroupName]
}
}]

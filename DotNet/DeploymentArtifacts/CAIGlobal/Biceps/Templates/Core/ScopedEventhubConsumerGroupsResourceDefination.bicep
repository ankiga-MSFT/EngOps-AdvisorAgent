param PeventHubNamespaceName string
param PeventHubName string
param PdefaultConsumerGroupNames array
param PscopedEventhubSubscriptionId string
param PscoperEventhubResourceGroupName string
param PdeployCornerstoneEventhubConsumerGroup string
var deployCornerstoneEventhubConsumerGroup=bool(PdeployCornerstoneEventhubConsumerGroup)
module ScopedEventhubConsumerGroupsResourceDefination 'EventhubConsumerGroupResourceDefination.bicep'= if (deployCornerstoneEventhubConsumerGroup) {
  name: 'ScopedEventhubConsumerGroupsResourceDefination'
  scope: resourceGroup(PscopedEventhubSubscriptionId,PscoperEventhubResourceGroupName)
   params: {
    PeventHubNamespaceName: PeventHubNamespaceName
    PeventHubName: PeventHubName
    PdefaultConsumerGroupNames: PdefaultConsumerGroupNames
}
}

param PeventHubNamespaceName string
param PeventHubNames array
param PeventHubNamespaceSkuName string = 'Premium'
param PeventHubNamespaceSkuTier string = 'Premium'
param PeventHubNamespaceSkuCapacity string 
param PpartitionCount string
param PmessageRetentionInDays string
var messageRetentionInDays = int(PmessageRetentionInDays)
var partitionCount = int(PpartitionCount)
var capacity = int(PeventHubNamespaceSkuCapacity)
param PdefaultConsumerGroupNames array

resource EventHubNamespace 'Microsoft.EventHub/namespaces@2024-05-01-preview' = {
  name: PeventHubNamespaceName
  location: resourceGroup().location
  sku: {
    name: PeventHubNamespaceSkuName
    tier: PeventHubNamespaceSkuTier
    capacity: capacity
  }
  
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    geoDataReplication: {
      maxReplicationLagDurationInSeconds: 0
      locations: [
        {
          locationName: resourceGroup().location
          roleType: 'Primary'
        }
      ]
    }
    isAutoInflateEnabled: false
    maximumThroughputUnits: 0
    zoneRedundant:true
    publicNetworkAccess: 'Disabled'
    disableLocalAuth:true
  }

}

resource EventHubNamespaceNetworkRules 'Microsoft.EventHub/namespaces/networkrulesets@2024-05-01-preview' = {
  parent: EventHubNamespace
  name: 'default'
  properties: {
    publicNetworkAccess: 'Disabled'
    defaultAction: 'Deny'
    virtualNetworkRules: []
    ipRules: []
    trustedServiceAccessEnabled: true
  }
}

resource eventHubs 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = [for eventHubName in PeventHubNames: {
  parent: EventHubNamespace
  name: eventHubName
  properties: {
    partitionCount: partitionCount
    messageRetentionInDays: messageRetentionInDays
  }
}]

@batchSize(1)
module eventhubConsumerGroups 'EventhubConsumerGroupResourceDefination.bicep' = [for (eventHubName,i) in PeventHubNames: {
  name: 'eventhubConsumerGroups-${eventHubName}'
  params: {
    PeventHubNamespaceName: EventHubNamespace.name
    PeventHubName: eventHubName
    PdefaultConsumerGroupNames: PdefaultConsumerGroupNames
  }
  dependsOn:eventHubs
}]

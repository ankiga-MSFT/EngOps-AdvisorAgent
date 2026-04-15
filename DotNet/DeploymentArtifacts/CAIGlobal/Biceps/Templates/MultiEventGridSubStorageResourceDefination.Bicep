param PcontainerNames array
param PeventGridSubscriptionNames array
param PeventGridTopicNames array
param PfunctionAppNames array
param PfunctionNames array
module CreateEventgridSubscriptions 'Core/EventGridSubStorageResourceDefination.bicep'=[for (containerName,i) in PcontainerNames: {
  name:'EventGrid-${containerName}'
  params:{
    PcontainerName:containerName
     PeventGridSubscriptionName:PeventGridSubscriptionNames[i]
      PeventGridTopicName:PeventGridTopicNames[i]
       PfunctionAppName:PfunctionAppNames[i]
       PfunctionName:PfunctionNames[i]
  }
}
]

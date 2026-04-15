param PstorageAccountName string
param PstorageQueueNames array




resource appStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing= {
  name: PstorageAccountName
}

resource appStorageQueueServices 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' existing = {
  name: 'default'
  parent: appStorageAccount
}

resource appStorageAccountqueueservice 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = [for queueName in PstorageQueueNames :{
  name: queueName
  parent: appStorageQueueServices
}
]



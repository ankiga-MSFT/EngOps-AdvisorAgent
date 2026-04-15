param PstorageAccountName string
param PcontainerNames array

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing= {
  name: PstorageAccountName
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' existing = {
  name: 'default'
  parent: storageAccount
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' =[for containerName in PcontainerNames: {
  name: containerName
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}]



param PstorageAccountName string
param PstorageSkuName string
param PstorageKind string
param PstorageSubnetName string  = 'storage'
param PvnetName string
param PdeployStorageAccount string
var deployICMIngestStorage = bool(PdeployStorageAccount)

var newNetworkRulesStorageSubnet=[{
  id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName , PstorageSubnetName)
  action: 'Allow'
}
]


resource appStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01'= if(deployICMIngestStorage) {
  name: PstorageAccountName
  location: resourceGroup().location
  kind: PstorageKind
  sku: {
    name: PstorageSkuName
  }
  properties:{
    publicNetworkAccess: 'Disabled'
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess:false
    allowSharedKeyAccess:false
    accessTier: 'Hot'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      virtualNetworkRules:newNetworkRulesStorageSubnet
    }
  }
  
}

resource appStorageAccountblobservice 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = if (deployICMIngestStorage) {
  name: 'default'
  parent: appStorageAccount
}

resource appStorageAccounttableservice 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = if(deployICMIngestStorage) {
  name: 'default'
  parent: appStorageAccount
}

resource appStorageAccountqueueservice 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = if(deployICMIngestStorage) {
  name: 'default'
  parent: appStorageAccount
}

resource appStorageAccountfileservice 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = if(deployICMIngestStorage) {
  name: 'default'
  parent: appStorageAccount
}

param PstorageSystemTopicsName string
module CreateStorageSystemTopic 'StorageSystemTopicResourceDefination.bicep'= if(deployICMIngestStorage) {
  name:'StorageSystemTopicResourceDefination'
  params:{
    PstorageResourceId:appStorageAccount.id
    PstorageSystemTopicsName:PstorageSystemTopicsName
  }
  dependsOn:[appStorageAccountblobservice,appStorageAccounttableservice,appStorageAccountqueueservice,appStorageAccountfileservice]
}

output storageAccountId string = deployICMIngestStorage ? appStorageAccount.id : 'Resource Deploy flag is false'
output storageAccountBlobServiceId string = deployICMIngestStorage ? appStorageAccountblobservice.id : 'Resource Deploy flag is false'

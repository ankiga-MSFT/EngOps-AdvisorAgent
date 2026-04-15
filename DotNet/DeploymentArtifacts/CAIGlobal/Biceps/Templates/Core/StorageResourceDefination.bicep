param PstorageAccountName string
param PstorageSkuName string
param PstorageKind string
param PstorageSubnetName string  = 'storage'
param PvnetName string = ''
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var hasVnet = PvnetName != ''
var newNetworkRulesStorageSubnet= hasVnet ? [{
  id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName , PstorageSubnetName)
  action: 'Allow'
}] : []


resource appStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01'= if(deployInfra) {
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

resource appStorageAccountblobservice 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' =if(deployInfra) {
  name: 'default'
  parent: appStorageAccount
}

resource appStorageAccounttableservice 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' =if(deployInfra) {
  name: 'default'
  parent: appStorageAccount
}

resource appStorageAccountqueueservice 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' =if(deployInfra) {
  name: 'default'
  parent: appStorageAccount
}

resource appStorageAccountfileservice 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' =if(deployInfra) {
  name: 'default'
  parent: appStorageAccount
}



output storageAccountId string = deployInfra ? appStorageAccount.id:''
output storageAccountBlobServiceId string = deployInfra ? appStorageAccountblobservice.id :''
output storageAccountName string = deployInfra ? appStorageAccount.name : ''

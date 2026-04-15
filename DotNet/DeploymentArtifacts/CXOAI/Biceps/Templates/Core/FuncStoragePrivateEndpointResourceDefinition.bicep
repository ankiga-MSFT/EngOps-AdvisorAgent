param PstorageAccountId string
param PvnetName string
param PfunctionName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var storageAccountName = split(PstorageAccountId, '/')[8]
var storageSuffix=environment().suffixes.storage
var PprivateEndpointStorageSubnetName  = 'storage'
var PprivateStorageFileDnsZoneName  ='privatelink.file.${storageSuffix}'
var PprivateStorageBlobDnsZoneName  ='privatelink.blob.${storageSuffix}'
var PprivateStorageQueueDnsZoneName  ='privatelink.queue.${storageSuffix}'
var PprivateStorageTableDnsZoneName  ='privatelink.table.${storageSuffix}'
var PprivateEndpointStorageFileName  ='${replace(storageAccountName,'-','')}.${replace(PfunctionName,'-','')}-file-endpoint'
var PprivateEndpointStorageBlobName  ='${replace(storageAccountName,'-','')}.${replace(PfunctionName,'-','')}-blob-endpoint'
var PprivateEndpointStorageTableName  ='${replace(storageAccountName,'-','')}.${replace(PfunctionName,'-','')}-table-endpoint'
var PprivateEndpointStorageQueueName  ='${replace(storageAccountName,'-','')}.${replace(PfunctionName,'-','')}-queue-endpoint'
var Plocation  = resourceGroup().location


resource appvnet 'Microsoft.Network/virtualNetworks@2022-05-01' existing = if(deployInfra) {
  name: PvnetName
}




resource privateStorageFileDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if(deployInfra) {
  name: PprivateStorageFileDnsZoneName
  location: 'global'
}


resource privateStorageBlobDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if(deployInfra) {
  name: PprivateStorageBlobDnsZoneName
  location: 'global'
}


resource privateStorageQueueDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if(deployInfra) {
  name: PprivateStorageQueueDnsZoneName
  location: 'global'
}


resource privateStorageTableDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if(deployInfra) {
  name: PprivateStorageTableDnsZoneName
  location: 'global'
}

resource privateStorageFileDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if(deployInfra) {
  parent: privateStorageFileDnsZone
  name: '${PvnetName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: appvnet.id
    }
  }
  dependsOn: [
    appvnet
  ]
}

resource privateStorageBlobDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if(deployInfra) {
  parent: privateStorageBlobDnsZone
  name: '${PvnetName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: appvnet.id
    }
  }
  dependsOn: [
    appvnet
  ]
}

resource privateStorageQueueDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if(deployInfra) {
  parent: privateStorageQueueDnsZone
  name: '${PvnetName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: appvnet.id
    }
  }
  dependsOn: [
    appvnet
  ]
}

resource privateStorageTableDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if(deployInfra) {
  parent: privateStorageTableDnsZone
  name: '${PvnetName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: appvnet.id
    }
  }
  dependsOn: [
    appvnet
  ]
}






resource privateEndpointStorageFile 'Microsoft.Network/privateEndpoints@2022-05-01' = if(deployInfra) {
  name: PprivateEndpointStorageFileName
  location: Plocation
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PprivateEndpointStorageSubnetName)
    }
    customNetworkInterfaceName: '${PprivateEndpointStorageFileName}-nic'
    privateLinkServiceConnections: [
      {
        name: PprivateEndpointStorageFileName
        properties: {
          privateLinkServiceId: PstorageAccountId
          groupIds: [
            'file'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appvnet
  ]
}


resource privateEndpointStorageBlob 'Microsoft.Network/privateEndpoints@2022-05-01' = if(deployInfra) {
  name: PprivateEndpointStorageBlobName
  location: Plocation
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PprivateEndpointStorageSubnetName)
    }
    customNetworkInterfaceName: '${PprivateEndpointStorageBlobName}-nic'
    privateLinkServiceConnections: [
      {
        name: PprivateEndpointStorageBlobName
        properties: {
          privateLinkServiceId: PstorageAccountId
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appvnet
  ]
}


resource privateEndpointStorageTable 'Microsoft.Network/privateEndpoints@2022-05-01' = if(deployInfra) {
  name: PprivateEndpointStorageTableName
  location: Plocation
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PprivateEndpointStorageSubnetName)
    }
    customNetworkInterfaceName: '${PprivateEndpointStorageTableName}-nic'
    privateLinkServiceConnections: [
      {
        name: PprivateEndpointStorageTableName
        properties: {
          privateLinkServiceId: PstorageAccountId
          groupIds: [
            'table'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appvnet
  ]
}


resource privateEndpointStorageQueue 'Microsoft.Network/privateEndpoints@2022-05-01' = if(deployInfra) {
  name: PprivateEndpointStorageQueueName
  location: Plocation
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PprivateEndpointStorageSubnetName)
    }
    customNetworkInterfaceName: '${PprivateEndpointStorageQueueName}-nic'
    privateLinkServiceConnections: [
      {
        name: PprivateEndpointStorageQueueName
        properties: {
          privateLinkServiceId: PstorageAccountId
          groupIds: [
            'queue'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appvnet
  ]
}




resource privateEndpointStorageFilePrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = if(deployInfra) {
  parent: privateEndpointStorageFile
  name: '${PprivateEndpointStorageFileName}-dns'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: PprivateStorageFileDnsZoneName
        properties: {
          privateDnsZoneId: privateStorageFileDnsZone.id
        }
      }
    ]
  }

}


resource privateEndpointStorageBlobPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = if(deployInfra) {
  parent: privateEndpointStorageBlob
  name: '${PprivateEndpointStorageBlobName}-dns'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: PprivateStorageBlobDnsZoneName
        properties: {
          privateDnsZoneId: privateStorageBlobDnsZone.id
        }
      }
    ]
  }

}


resource privateEndpointStorageTablePrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = if(deployInfra) {
  parent: privateEndpointStorageTable
  name: '${PprivateEndpointStorageTableName}-dns'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: PprivateStorageTableDnsZoneName
        properties: {
          privateDnsZoneId: privateStorageTableDnsZone.id
        }
      }
    ]
  }

}


resource privateEndpointStorageQueuePrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = if(deployInfra) {
  parent: privateEndpointStorageQueue
  name: '${PprivateEndpointStorageQueueName}-dns'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: PprivateStorageQueueDnsZoneName
        properties: {
          privateDnsZoneId: privateStorageQueueDnsZone.id
        }
      }
    ]
  }

}



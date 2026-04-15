param PCosmosDbAccountResourceId string
param PvnetName string
param PfunctionName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
param PdeployCosmosResources string
var deployCosmosResources =bool(PdeployCosmosResources)
var CosmosDbAccountName = split(PCosmosDbAccountResourceId, '/')[8]
var PprivateEndpointStorageSubnetName  = 'storage'
var PprivateCosmosDnsZoneName  ='privatelink.documents.azure.com' 
var PprivateEndpointCosmosName  ='${replace(CosmosDbAccountName,'-','')}.${replace(PfunctionName,'-','')}-endpoint'
var Plocation  = resourceGroup().location


resource appvnet 'Microsoft.Network/virtualNetworks@2022-05-01' existing = if(deployCosmosResources && deployInfra) {
  name: PvnetName
}

resource privateCosmosDbDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if(deployCosmosResources && deployInfra){
  name: PprivateCosmosDnsZoneName
  location: 'global'
}

resource privateCosmosDbDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if(deployCosmosResources && deployInfra){
  parent: privateCosmosDbDnsZone
  name: '${PvnetName}-Link'
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




resource privateEndpointCosmosDb 'Microsoft.Network/privateEndpoints@2022-05-01' = if(deployCosmosResources && deployInfra) {
  name: PprivateEndpointCosmosName
  location: Plocation
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PprivateEndpointStorageSubnetName)
    }
    customNetworkInterfaceName: '${PprivateEndpointCosmosName}-nic'

    privateLinkServiceConnections: [
      {
        name: PprivateEndpointCosmosName
        properties: {
          privateLinkServiceId: PCosmosDbAccountResourceId
          groupIds: [
            'sql'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appvnet
  ]
}

resource privateEndpointCosmosDbPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = if(deployCosmosResources && deployInfra) {
  parent: privateEndpointCosmosDb
  name: '${PprivateEndpointCosmosName}-dns'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: PprivateCosmosDnsZoneName
        properties: {
          privateDnsZoneId: privateCosmosDbDnsZone.id
        }
      }
    ]
  }
  dependsOn: [
    privateCosmosDbDnsZoneLink
  ]
}







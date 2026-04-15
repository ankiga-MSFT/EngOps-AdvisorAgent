param PeventHubNamespaceAccountId string
param PvnetName string
param PfunctionName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
param PdeployEventhubPrivateEndpoint string
var deployEventhubPrivateEndpoint=bool(PdeployEventhubPrivateEndpoint)
var eventHubNamespaceAccountName = split(PeventHubNamespaceAccountId, '/')[8]
var PprivateEndpointeventHubNamespaceSubnetName  = 'storage'
var PprivateeventHubNamespaceDnsZoneName  ='privatelink.servicebus.windows.net'
var PprivateEndpointeventHubNamespaceName  ='${replace(eventHubNamespaceAccountName,'-','')}.${replace(PfunctionName,'-','')}-endpoint'
var Plocation  = resourceGroup().location

resource appvnet 'Microsoft.Network/virtualNetworks@2022-05-01' existing = if(deployEventhubPrivateEndpoint && deployInfra) {
  name: PvnetName
}



resource privateeventHubNamespaceDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if(deployEventhubPrivateEndpoint && deployInfra) {
  name: PprivateeventHubNamespaceDnsZoneName
  location: 'global'
}


resource privateeventHubNamespaceDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if(deployEventhubPrivateEndpoint && deployInfra) {
  parent: privateeventHubNamespaceDnsZone
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






resource privateEndpointeventHubNamespace 'Microsoft.Network/privateEndpoints@2022-05-01' = if(deployEventhubPrivateEndpoint && deployInfra) {
  name: PprivateEndpointeventHubNamespaceName
  location: Plocation
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PprivateEndpointeventHubNamespaceSubnetName)
    }
    customNetworkInterfaceName: '${PprivateEndpointeventHubNamespaceName}-nic'
    privateLinkServiceConnections: [
      {
        name: PprivateEndpointeventHubNamespaceName
        properties: {
          privateLinkServiceId: PeventHubNamespaceAccountId
          groupIds: [
            'namespace'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appvnet
  ]
}

resource privateEndpointeventHubNamespacePrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = if(deployEventhubPrivateEndpoint && deployInfra) {
  parent: privateEndpointeventHubNamespace
  name: '${PprivateEndpointeventHubNamespaceName}-dns'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: PprivateeventHubNamespaceDnsZoneName
        properties: {
          privateDnsZoneId: privateeventHubNamespaceDnsZone.id
        }
      }
    ]
  }

}




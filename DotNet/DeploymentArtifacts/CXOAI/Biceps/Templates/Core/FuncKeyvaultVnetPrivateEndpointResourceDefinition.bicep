param PkeyVaultAccountId string
param PvnetName string
param PfunctionName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var keyVaultAccountName = split(PkeyVaultAccountId, '/')[8]
var PprivateEndpointkeyVaultSubnetName  = 'storage'
var PprivatekeyVaultDnsZoneName  ='privatelink.vaultcore.azure.net' 
var PprivateEndpointkeyVaultName  ='${replace(keyVaultAccountName,'-','')}.${replace(PfunctionName,'-','')}-endpoint'
var Plocation  = resourceGroup().location


resource appvnet 'Microsoft.Network/virtualNetworks@2022-05-01' existing = if(deployInfra) {
  name: PvnetName
}



resource privatekeyVaultDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if(deployInfra) {
  name: PprivatekeyVaultDnsZoneName
  location: 'global'
}



resource privatekeyVaultDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if(deployInfra) {
  parent: privatekeyVaultDnsZone
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






resource privateEndpointkeyVault 'Microsoft.Network/privateEndpoints@2022-05-01' = if(deployInfra) {
  name: PprivateEndpointkeyVaultName
  location: Plocation
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PprivateEndpointkeyVaultSubnetName)
    }
    customNetworkInterfaceName: '${PprivateEndpointkeyVaultName}-nic'
    privateLinkServiceConnections: [
      {
        name: PprivateEndpointkeyVaultName
        properties: {
          privateLinkServiceId: PkeyVaultAccountId
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appvnet
  ]
}

resource privateEndpointkeyVaultPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = if(deployInfra) {
  parent: privateEndpointkeyVault
  name: '${PprivateEndpointkeyVaultName}-dns'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: PprivatekeyVaultDnsZoneName
        properties: {
          privateDnsZoneId: privatekeyVaultDnsZone.id
        }
      }
    ]
  }
  dependsOn: [
    privatekeyVaultDnsZone
  ]
}



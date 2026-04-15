param Plocation string =resourceGroup().location
param PkustoClusterName string
param PvnetName string
param PsubnetName string='storage'
param PvnetResourceGroupName string
param PvnetResourceId string
// Reference the existing Kusto cluster

resource kustoCluster 'Microsoft.Kusto/clusters@2021-08-27' existing = {
  name: PkustoClusterName
}

// Create Private Endpoint for Kusto cluster
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2021-02-01' = {
  name: '${PkustoClusterName}-pe'
  location: Plocation
  properties: {
    subnet: {
      id: resourceId(PvnetResourceGroupName,'Microsoft.Network/virtualNetworks/subnets', PvnetName, PsubnetName)
    }
    privateLinkServiceConnections: [
      {
        name: '${PkustoClusterName}-plsc'
        properties: {
          privateLinkServiceId: kustoCluster.id
          groupIds: [
            'cluster'
          ]
          requestMessage: 'Please approve this connection.'
        }
      }
    ]
  }
}

// Approve Private Endpoint Connection for Kusto cluster
resource privateEndpointConnection 'Microsoft.Kusto/clusters/privateEndpointConnections@2021-08-27' = {
  name: '${PkustoClusterName}-pe-${uniqueString(kustoCluster.id)}'
  parent: kustoCluster
  properties: {
    privateLinkServiceConnectionState: {
      status: 'Approved'
      description: 'Approved by Bicep deployment'
    }
  }
}

// Create Private DNS Zone
resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: '${PvnetName}-privatelink.kusto.windows.net'
  location: 'global'
}

resource virtualNetworkLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: '${PvnetName}-vnetlink'
  location: 'global'
  parent: privateDnsZone
  properties: {
    virtualNetwork: {
      id: PvnetResourceId //resourceId(toLower(PvnetResourceGroupName),'Microsoft.Network/virtualNetworks', toLower(PvnetName))
    }
    registrationEnabled: false
  }
}

// Link Private Endpoint to DNS Zone
resource privateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2022-05-01' = {
  name: '${PkustoClusterName}-pdzg'
  parent: privateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink'
        properties: {
          privateDnsZoneId: privateDnsZone.id
        }
      }
    ]
  }
}

// Create Virtual Network Link to Private DNS Zone

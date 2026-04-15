param PvnetName string
param PnsgName string
param PfunctionName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
// param PnatName string
// param PipName string
var PfunctionSubnetName  = 'default'
var PprivateEndpointStorageSubnetName  = 'storage'
var Plocation  = resourceGroup().location

metadata PnatGatewayId='__NAT_GATEWAY_ID__'
param PnatGatewayId string

resource nsg 'Microsoft.Network/networkSecurityGroups@2022-07-01' existing= if(deployInfra) {
  name: PnsgName 
}


// resource publicIPAddress 'Microsoft.Network/publicIPAddresses@2019-04-01' =  {
//   name: PipName 
//   location: Plocation
//   sku: {
//     name: 'Standard'
//   }
//   properties: {
//     publicIPAllocationMethod: 'Static'
    
//   }
// }


// resource nat 'Microsoft.Network/natGateways@2024-01-01' =  {
//   name: PnatName 
//   location: Plocation
//   sku: {
//     name: 'Standard'
//   }
//   properties: {
//     idleTimeoutInMinutes: 4
//     publicIpAddresses: [
//       {
//         id: publicIPAddress.id
//       }
//     ]
//   }
 
// }


resource appvnet 'Microsoft.Network/virtualNetworks@2022-05-01' =  if(deployInfra) {
  name: PvnetName 
  location: Plocation
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: PfunctionSubnetName
        properties: {
          addressPrefixes: [
            '10.0.0.0/24'
          ]
          networkSecurityGroup: {
            id: nsg.id
          }
          natGateway: {
            id: PnatGatewayId
          }
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
              locations: [
                'eastus'
                'westus'
                'westus3'
              ]
            }
          ]
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
              type: 'Microsoft.Network/virtualNetworks/subnets/delegations'
            }
          ]
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
          defaultOutboundAccess: false
        }
      }
      {
        name: PprivateEndpointStorageSubnetName
        properties: {
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
          defaultOutboundAccess: false
          addressPrefixes: ['10.0.1.0/24']
          networkSecurityGroup: {
            id: nsg.id
          }
          natGateway: {
            id: PnatGatewayId
          }
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
              locations: [
                'eastus'
                'westus'
                'westus3'
              ]
            }
            {
              service: 'Microsoft.KeyVault'
            }
            { 
              service: 'Microsoft.EventHub'
            }
          ]
        }
      }
    ]
  }
  #disable-next-line no-unnecessary-dependson
dependsOn:[nsg]
}




resource appFunction 'Microsoft.Web/sites@2023-12-01' existing= if(deployInfra)  {
  name: PfunctionName 
    scope: resourceGroup()
}



resource functionNetworkConfig 'Microsoft.Web/sites/networkConfig@2022-03-01' = if(deployInfra) {
  parent: appFunction
  name: 'virtualNetwork'
  properties: {
    subnetResourceId: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName , PfunctionSubnetName)
    swiftSupported: true
  }
  dependsOn: [
    appvnet
  ]
}

resource slotNetworkConfig 'Microsoft.Web/sites/slots@2021-02-01' = if(deployInfra) {
  name: 'staging'
  parent: appFunction
  location: Plocation
  properties: {
    virtualNetworkSubnetId: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PfunctionSubnetName)
  }
  dependsOn: [
    appvnet
  ]
}

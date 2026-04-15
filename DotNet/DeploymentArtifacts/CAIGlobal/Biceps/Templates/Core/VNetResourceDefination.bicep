
param PvnetName string
param Plocation string
param PsubnetName string='storage'
param PnsgName string
metadata PnatGatewayId='__NAT_GATEWAY_ID__'
param PnatGatewayId string
resource nsg 'Microsoft.Network/networkSecurityGroups@2022-07-01' existing = {
  name: PnsgName
}


resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
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
        name: PsubnetName
        properties: {
          addressPrefix: '10.0.0.0/24'
          defaultOutboundAccess: false
          networkSecurityGroup: {
            id: nsg.id
          }
          natGateway: {
            id: PnatGatewayId
          }
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
            }
            {
              service: 'Microsoft.KeyVault'
            }
            { 
              service: 'Microsoft.EventHub'
            }
            // {
            //   service: 'Microsoft.DataFactory'
            // }
          ]
        }
      }
    ]
  }
  dependsOn: [
    #disable-next-line no-unnecessary-dependson
    nsg
  ]
}

output vnetResourceId string = vnet.id

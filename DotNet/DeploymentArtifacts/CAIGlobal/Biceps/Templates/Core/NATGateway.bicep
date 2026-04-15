param PnatGatewayName string
var location  = resourceGroup().location
param PpublicIpName string
param PnumberOfPublicIPs string
param PoutboundServiceTag array

var vnumberOfPublicIPs = int(PnumberOfPublicIPs)

resource PpublicIpName_publicIpAddressesCopy 'Microsoft.Network/publicIPAddresses@2024-07-01' = [
  for i in range(0, vnumberOfPublicIPs): {
    name: '${PpublicIpName}${i}'
    location: location
    properties: {
      publicIPAllocationMethod: 'Static'
      ipTags: PoutboundServiceTag
    }
    sku: {
      name: 'Standard'
    }
  }
]

resource natGateway 'Microsoft.Network/natGateways@2024-07-01' = {
  name: PnatGatewayName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
  idleTimeoutInMinutes: 4
  publicIpAddresses: [
    for j in range(0, vnumberOfPublicIPs): {
        id: resourceId('Microsoft.Network/publicIPAddresses', '${PpublicIpName}${j}')
      }
    ]
  }
  dependsOn: [
    PpublicIpName_publicIpAddressesCopy
  ]
}

output natGatewayId string = natGateway.id

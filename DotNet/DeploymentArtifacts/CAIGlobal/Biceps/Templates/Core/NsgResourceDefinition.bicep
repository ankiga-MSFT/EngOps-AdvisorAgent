param PnsgName string
var Plocation  = resourceGroup().location

resource nsg 'Microsoft.Network/networkSecurityGroups@2022-07-01' = {
  name: PnsgName 
  location: Plocation
}


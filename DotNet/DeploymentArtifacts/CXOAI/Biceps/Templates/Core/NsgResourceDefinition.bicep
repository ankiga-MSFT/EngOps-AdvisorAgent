param PnsgName string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
var Plocation  = resourceGroup().location

resource nsg 'Microsoft.Network/networkSecurityGroups@2022-07-01' = if(deployInfra) {
  name: PnsgName
  location: Plocation
}


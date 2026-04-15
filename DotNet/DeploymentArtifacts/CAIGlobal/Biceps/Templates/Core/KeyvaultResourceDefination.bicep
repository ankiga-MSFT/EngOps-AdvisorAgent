param PkeyVaultName string
param Plocation string
param PKeyVaultSkuName string = 'standard'
param PKeyVaultSkuFamily string = 'A'
param PvnetName string = ''
param PsubnetName string='storage'
var hasVnet = PvnetName != ''
resource keyVault 'Microsoft.KeyVault/vaults@2021-11-01-preview' = {
  name: PkeyVaultName
  location: Plocation
  properties: {
    publicNetworkAccess: 'Disabled'
    enableRbacAuthorization: true
    tenantId: subscription().tenantId
    sku: {
      family: PKeyVaultSkuFamily
      name: PKeyVaultSkuName
    }
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      virtualNetworkRules: hasVnet ? [
        {
          id: resourceId('Microsoft.Network/virtualNetworks/subnets', PvnetName, PsubnetName)
        }
      ] : []
  }
  
  }
}
output keyVaultName string = keyVault.name

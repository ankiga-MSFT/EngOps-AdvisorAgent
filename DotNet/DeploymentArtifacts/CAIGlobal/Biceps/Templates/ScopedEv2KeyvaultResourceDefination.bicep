param PkeyVaultName string
param Plocation string
param PresourceGroup string
param Pdeploy string

var deploy = bool(Pdeploy)

module CreateEv2Keyvault 'Core/KeyvaultResourceDefination.bicep' = if (deploy) {
  name: 'CreateEv2Keyvault'
  scope: resourceGroup(PresourceGroup)
  params: {
    Plocation: Plocation
    PkeyVaultName: PkeyVaultName
  }
}

output keyVaultName string = deploy ? CreateEv2Keyvault.outputs.keyVaultName : PkeyVaultName

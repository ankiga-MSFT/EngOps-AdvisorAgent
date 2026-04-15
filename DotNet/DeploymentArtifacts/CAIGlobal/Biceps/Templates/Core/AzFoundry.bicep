param PfoundryAccountName string
param Plocation string
param PmodelDeployments array = []
param PcreateProject bool = false
param PprojectName string = '${PfoundryAccountName}-project'
param PpublicNetworkAccess string = 'Enabled'

// PmodelDeployments schema (each item):
// {
//   name: 'deployment-name',
//   modelName: 'gpt-5.2',
//   format: 'OpenAI',
//   version: '2025-12-11',          // optional
//   skuName: 'GlobalStandard',      // optional, defaults to GlobalStandard
//   capacity: 150,
//   versionUpgradeOption: 'OnceNewDefaultVersionAvailable', // optional
//   raiPolicyName: 'Microsoft.DefaultV2' // optional
// }

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: PfoundryAccountName
  location: Plocation
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: PfoundryAccountName
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
    allowProjectManagement: PcreateProject
    publicNetworkAccess: PpublicNetworkAccess
    disableLocalAuth: true
  }
}

resource foundryDefender 'Microsoft.CognitiveServices/accounts/defenderForAISettings@2025-04-01-preview' = {
  parent: foundryAccount
  name: 'Default'
  properties: {
    state: 'Disabled'
  }
}

// If callers provide PmodelDeployments array, create deployments from it.
@batchSize(1)
resource modelDeployments 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = [for model in PmodelDeployments: {
  parent: foundryAccount
  name: model.name
  sku: {
    name: (model.skuName != '') ? model.skuName : 'GlobalStandard'
    capacity: model.capacity
  }
  properties: {
    model: {
      format: model.format
      name: model.modelName
      version: contains(model, 'version') ? model.version : null
    }
    versionUpgradeOption: contains(model, 'versionUpgradeOption') ? model.versionUpgradeOption : 'NoAutoUpgrade'
    currentCapacity: model.capacity
    raiPolicyName: contains(model, 'raiPolicyName') ? model.raiPolicyName : 'Microsoft.DefaultV2'
  }
  dependsOn: [
    foundryDefender
  ]
}]

resource foundryProject 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = if (PcreateProject) {
  parent: foundryAccount
  name: PprojectName
  location: Plocation
  kind: 'AIServices'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}


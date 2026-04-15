param PopenAIAccountName string
param Plocation string

resource accounts_aoi_cxpes_test_llm_1_name_resource 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: PopenAIAccountName
  location: Plocation
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: PopenAIAccountName
    networkAcls: {
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
    publicNetworkAccess: 'Enabled'
    disableLocalAuth:true
  }
}

resource accounts_aoi_cxpes_test_llm_1_name_Default 'Microsoft.CognitiveServices/accounts/defenderForAISettings@2025-04-01-preview' = {
  parent: accounts_aoi_cxpes_test_llm_1_name_resource
  name: 'Default'
  properties: {
    state: 'Disabled'
  }
}


resource accounts_aoi_cxpes_test_llm_1_name_gpt_4o 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: accounts_aoi_cxpes_test_llm_1_name_resource
  name: 'gpt-4o'
  sku: {
    name: 'GlobalStandard'
    capacity: 450
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
    currentCapacity: 450
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [
    accounts_aoi_cxpes_test_llm_1_name_Default
  ]
}

resource accounts_aoi_cxpes_test_llm_1_name_gpt_4o_mini 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: accounts_aoi_cxpes_test_llm_1_name_resource
  name: 'gpt-4o-mini'
  sku: {
    name: 'GlobalStandard'
    capacity: 2000
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o-mini'
      version: '2024-07-18'
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
    currentCapacity: 2000
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [
    accounts_aoi_cxpes_test_llm_1_name_gpt_4o
  ]
}


resource accounts_aoi_cxpes_test_llm_1_name_text_embedding_3_large 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: accounts_aoi_cxpes_test_llm_1_name_resource
  name: 'text-embedding-3-large'
  sku: {
    name: 'Standard'
    capacity: 350
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
    currentCapacity: 350
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [
    accounts_aoi_cxpes_test_llm_1_name_gpt_4o_mini
  ]
}

resource accounts_aoi_cxpes_test_llm_1_name_text_embedding_ada_002 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: accounts_aoi_cxpes_test_llm_1_name_resource
  name: 'text-embedding-ada-002'
  sku: {
    name: 'Standard'
    capacity: 240
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-ada-002'
      version: '2'
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
    currentCapacity: 240
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [
    accounts_aoi_cxpes_test_llm_1_name_text_embedding_3_large
  ]
}

resource accounts_aoi_cxpes_test_llm_1_name_o3_mini 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: accounts_aoi_cxpes_test_llm_1_name_resource
  name: 'o3-mini'
  sku: {
    name: 'GlobalStandard'
    capacity: 500
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'o3-mini'
      version: '2025-01-31'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: 500
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [
    accounts_aoi_cxpes_test_llm_1_name_text_embedding_ada_002
  ]
}

resource accounts_aoi_cxpes_test_llm_1_name_gpt_4_1 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: accounts_aoi_cxpes_test_llm_1_name_resource
  name: 'gpt-4.1'
  sku: {
    name: 'GlobalStandard'
    capacity: 150
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1'
      version: '2025-04-14'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: 150
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [
    accounts_aoi_cxpes_test_llm_1_name_o3_mini
  ]
}

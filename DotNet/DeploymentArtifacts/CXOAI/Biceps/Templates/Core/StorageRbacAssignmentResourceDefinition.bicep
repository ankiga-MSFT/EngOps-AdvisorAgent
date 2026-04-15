param PstorageAccountName string
param ProleDefinationIds  array
param PfunctionName string

resource appStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing={
  name: PstorageAccountName
  scope: resourceGroup()
}


resource appFunction 'Microsoft.Web/sites@2023-12-01' existing= {
  name: PfunctionName
}



resource functionrbacstorage  'Microsoft.Authorization/roleAssignments@2020-10-01-preview' =[ for i in range(0,length(ProleDefinationIds)) :  {
  name: guid('${appStorageAccount.id}-${PfunctionName}-${ProleDefinationIds[i]}')
  scope: appStorageAccount
  properties: {
    principalId: appFunction.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',ProleDefinationIds[i])
  }
}
]

resource functionAppSlot 'Microsoft.Web/sites/slots@2021-02-01' existing = {
  parent: appFunction
  name: 'staging'
}

resource slotrbacstorage  'Microsoft.Authorization/roleAssignments@2020-10-01-preview' =[ for i in range(0,length(ProleDefinationIds)) :  {
  name: guid('${functionAppSlot.id}-${PfunctionName}-${ProleDefinationIds[i]}')
  scope: appStorageAccount
  properties: {
    principalId: functionAppSlot.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',ProleDefinationIds[i])
  }
}
]

///code for creating role assignment for storage account using nested for loop




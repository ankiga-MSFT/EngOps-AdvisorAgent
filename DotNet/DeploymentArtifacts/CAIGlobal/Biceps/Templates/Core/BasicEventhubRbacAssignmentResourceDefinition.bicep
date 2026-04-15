param Peventhubnamespace string 
param Peventhub string 
param ProleDefinationId string 
param principleid string

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2021-06-01-preview' existing = {
  name: Peventhubnamespace
}

resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2021-06-01-preview' existing= {
  parent: eventHubNamespace
  name: Peventhub
}


resource RoleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' =  {
  name: guid('${principleid}-${Peventhubnamespace}-${Peventhub}-${ProleDefinationId}')
  scope:eventHub
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',ProleDefinationId)
    principalId: principleid
	principalType: 'ServicePrincipal'
  }
}







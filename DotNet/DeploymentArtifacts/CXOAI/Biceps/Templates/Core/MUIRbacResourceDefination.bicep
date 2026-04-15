@description('Name of function app.')
param PfunctionName string
param PmuiPrincipleId string
@description('MUI name')
param PdeployInfra string
var deployInfra = bool(PdeployInfra)

var websiteContributorRoleId = 'de139f84-1756-47ae-9be6-808fbbe84772'

resource appFunction 'Microsoft.Web/sites@2023-12-01' existing = if(deployInfra) {
  name: PfunctionName
}


resource MsiContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if(deployInfra) {
  scope: appFunction
  name: guid('${subscription().id}-${websiteContributorRoleId}-${PmuiPrincipleId}-${PfunctionName} ')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', websiteContributorRoleId)
    principalId: PmuiPrincipleId
    principalType: 'ServicePrincipal'
  }
}


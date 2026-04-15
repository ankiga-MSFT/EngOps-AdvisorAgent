param PservicePlanName string
param PfunctionName string
param PmuiResourceId string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
resource appServicePlan 'Microsoft.Web/serverfarms@2021-03-01'= if(deployInfra) {
  name: PservicePlanName
  location: resourceGroup().location
}



resource appFunction 'Microsoft.Web/sites@2023-12-01' = if(deployInfra) {
  name: PfunctionName
  location: resourceGroup().location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${PmuiResourceId}':{}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    reserved: true
    vnetRouteAllEnabled: true
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    //containerSize: 1536
    siteConfig: {
      alwaysOn: true
      healthCheckPath: 'api/HealthCheckFunction'
      autoHealEnabled: true
    }
    
  }
  
}
var allowCors= ['https://ms.portal.azure.com']

resource appFunctionSiteConfig 'Microsoft.Web/sites/config@2023-12-01' = if(deployInfra) {
  parent: appFunction
  name: 'web'
  properties: {
    cors: {
      allowedOrigins: allowCors
      supportCredentials: true
    }
    publicNetworkAccess: 'Enabled'
    netFrameworkVersion: 'v8.0'
    use32BitWorkerProcess: false
    alwaysOn: true
  }
}

// resource appFunctionAuthSettingConfig 'Microsoft.Web/sites/config@2023-12-01' = {
// parent: appFunction
// name: 'authsettingsV2'
//  properties:{
//   globalValidation:{
//     unauthenticatedClientAction: 'RedirectToLoginPage'
//   }
//   platform:{
//     enabled: true
//   }
  
//   // identityProviders:{
//   //      azureActiveDirectory:{
//   //       enabled: true 
//   //       registration: {
//   //         clientId: PaadAppClientId
//   //          openIdIssuer: 'https://${environment().authentication.loginEndpoint}/${subscription().tenantId}/v2.0'
//   //       }
//   //     }
//   //   }
// }
// }

resource functionAppSlot 'Microsoft.Web/sites/slots@2021-02-01' = if(deployInfra) {
  parent: appFunction
  name: 'staging'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${PmuiResourceId}':{}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    reserved: true
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    //containerSize: 1536
    siteConfig: {
      alwaysOn: true
      cors: {
        allowedOrigins: allowCors
        supportCredentials: true
      }
    }
  }
}

// resource appFunctionSlotAuthSettingConfig 'Microsoft.Web/sites/slots/config@2023-12-01' = {
// parent: functionAppSlot
// name: 'authsettingsV2'
//  properties:{
//   globalValidation:{
//     unauthenticatedClientAction: 'RedirectToLoginPage'
//   }
//   platform:{
//     enabled: true
//   }
  
//   // identityProviders:{
//   //      azureActiveDirectory:{
//   //       enabled: true 
//   //       registration: {
//   //         clientId: PaadAppClientId
//   //          openIdIssuer: 'https://${environment().authentication.loginEndpoint}/${subscription().tenantId}/v2.0'
//   //       }
//   //     }
//   //   }
// }
// }





output functionId string = deployInfra ? appFunction.id :''
output functionManagedIdentityId string =  deployInfra ? appFunction.identity.principalId :''
output functionAppSlotIdentityId string = deployInfra ? functionAppSlot.identity.principalId :''

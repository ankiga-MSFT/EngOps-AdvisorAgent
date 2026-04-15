@secure()
param PfunctionName string
param PstorageAccountName string
param PappInsightConnectionString string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)

var allowCors= ['https://ms.portal.azure.com']


var BASE_APPSETTINGS  =  [
  { name: 'WEBSITE_CONTENTOVERVNET', value: '1', slotSetting: false }
  { name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES', value:  '10', slotSetting:false}
  { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4', slotSetting: false }
  { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated', slotSetting: false }
  { name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED', value:  '1', slotSetting: false }
  { name: 'WEBSITE_ADD_SITENAME_BINDINGS_IN_APPHOST_CONFIG', value: 'true', slotSetting: false }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value:  PappInsightConnectionString, slotSetting:false}
  { name: 'AzureWebJobsStorage__accountName', value:  PstorageAccountName, slotSetting:false } 
  
]

var PRODUCTION_SLOT_SETTINGS = [
  { name: 'cxoaiEnvironment', value: 'PROD' , slotSetting:false}
]


var STAGING_SLOT_SETTINGS = [
  { name: 'cxoaiEnvironment', value: 'STAGE' , slotSetting:false}
]

var PRODUCTION_SETTINGS=concat(BASE_APPSETTINGS,PRODUCTION_SLOT_SETTINGS)

var STAGING_SETTINGS=concat(BASE_APPSETTINGS,STAGING_SLOT_SETTINGS)

resource appFunction 'Microsoft.Web/sites@2023-12-01' existing= if(deployInfra) {
  name: PfunctionName
}


resource appFunctionSiteConfig 'Microsoft.Web/sites/config@2023-12-01' = if(deployInfra) {
  parent: appFunction
  name: 'web'
  properties: {
    cors: {
      allowedOrigins:allowCors
      supportCredentials: true
    }
    appSettings: PRODUCTION_SETTINGS
    publicNetworkAccess: 'Enabled'
    alwaysOn: true
    use32BitWorkerProcess: false	

  }
}








// resource productionSlotAppSettings 'Microsoft.Web/sites/slots/config@2022-09-01'  = {
//   name: '${PfunctionName}/production/appsettings'
//   properties: [for setting in PRODUCTION_SETTINGS: {
//     name: setting.name
//     value: setting.value
//     slotSetting: setting.slotSetting
//   }]
// }




resource functionAppStagingSlot 'Microsoft.Web/sites/slots@2021-02-01'  = if(deployInfra) {
  name: 'staging'
  parent: appFunction
  kind: 'functionapp'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    reserved: true
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      alwaysOn: true
      appSettings: STAGING_SETTINGS
      cors: {
        allowedOrigins: allowCors
        supportCredentials: true
      }
    }
  }
}




// resource SlotName_MSDeploy 'Microsoft.Web/sites/slots/extensions@2018-02-01' = {
//   parent: functionAppSlot
//   name: 'MSDeploy'
//   properties: {
//     packageUri: PmsdeployPackageUrl
//     appOffline: true
//   }
// }


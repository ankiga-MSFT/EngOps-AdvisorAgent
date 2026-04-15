param PfunctionName string
param PstorageAccountName string
param PappInsightConnectionString string
param PBlobContainerName string
param PFunctionAppArtifactBlobFileName string
param PAppEnviromentName string
param PConfigurationStoreDatabase string
param PConfigurationStoreCollection string
param PConfigurationStoreLeaseCollection string
param PConfigurationStoreConnectionEndpoint string
var storageEndpointSuffix = environment().suffixes.storage

var msdeployPackageUrl='https://${PstorageAccountName}.blob.${storageEndpointSuffix}/${PBlobContainerName}/${PFunctionAppArtifactBlobFileName}'

var allowCors= ['https://ms.portal.azure.com']
var storageBlobUrl = 'https://${PstorageAccountName}.blob.${storageEndpointSuffix}/'
var BASE_APPSETTINGS  =  [
  { name: 'ApplicationName', value:  PfunctionName, slotSetting:false}
  { name: 'AppEnvironmentName', value:  PAppEnviromentName, slotSetting:false}
  { name: 'AppLocationName', value:  resourceGroup().location, slotSetting:false}
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value:  PappInsightConnectionString, slotSetting:false}
  { name: 'AzureWebJobsStorage__accountName', value:  PstorageAccountName, slotSetting:false } 
  { name: 'WEBSITE_RUN_FROM_PACKAGE', value: msdeployPackageUrl, slotSetting:false}
  { name: 'WEBSITE_CONTENTOVERVNET', value: '1', slotSetting: false }
  { name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES', value:  '10', slotSetting:false}
  { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4', slotSetting: false }
  { name: 'FUNCTIONS_INPROC_NET8_ENABLED', value: '0', slotSetting: false }
  { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated', slotSetting: false }
  { name: 'DiagnosticServices_EXTENSION_VERSION', value: '~3', slotSetting: true }
  { name: 'WEBSITE_ADD_SITENAME_BINDINGS_IN_APPHOST_CONFIG', value: 'true', slotSetting: false }
  { name: 'WEBSITE_FIRST_PARTY_ID', value: 'AntMDS', slotSetting:false}
  { name: 'WEBSITE_NODE_DEFAULT_VERSION', value: '6.7.0', slotSetting:false}
  { name: 'WEBSITE_LOAD_USER_PROFILE', value: '1', slotSetting:false}
  { name: 'AppStorageAccountName', value: PstorageAccountName, slotSetting:false}
  { name: 'ConfigurationStoreDatabase', value: PConfigurationStoreDatabase, slotSetting:false}
  { name: 'ConfigurationStoreCollection', value: PConfigurationStoreCollection, slotSetting:false}
  { name: 'ConfigurationStoreLeaseCollection', value: PConfigurationStoreLeaseCollection, slotSetting:false}
  { name: 'ConfigurationStoreConnection__accountEndpoint', value: PConfigurationStoreConnectionEndpoint, slotSetting:false}
]


var STAGING_SLOT_SETTINGS = [
  { name: 'cxoaiSlotName', value: 'staging' , slotSetting:true}
]


var STAGING_SETTINGS=concat(BASE_APPSETTINGS,STAGING_SLOT_SETTINGS)

resource appFunction 'Microsoft.Web/sites@2023-12-01' existing= {
  name: PfunctionName
}





resource functionAppStagingSlot 'Microsoft.Web/sites/slots@2021-02-01'  = {
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


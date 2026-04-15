param PservicePlanName string
param PservicePlanSkuName string
param PservicePlanSkuTier string
param PservicePlanSkuCapacity string
param PdeployInfra string
var deployInfra = bool(PdeployInfra)
@description('The CSM ID of the KeyVault instance containing the AntMDS certificate.')
param PgenevaCertVaultId string

@description('The name of the AntMDS certificate.')
param PgenevaCertSecretName string

@description('Sets the MONITORING_TENANT environment variable ')
param PmonitoringTenant string

@description('Sets the MONITORING_ROLE environment variable')
param PmonitoringRole string

@description('The endpoint for your Geneva Account.  Sets the MONITORING_GCS_ENVIRONMENT environment variable.')
@allowed([
  'Diagnostics Prod'
  'Test'
  'Stage'
  'FirstPartyProd'
  'BillingProd'
  'ExternalProd'
  'CA BlackForest'
  'CA Fairfax'
  'CA Mooncake'
])
param PmonitoringGcsEnvironment string

@description('Sets the MONITORING_GCS_ACCOUNT environment variable.')
param PmonitoringGcsAccount string

@description('Sets the MONITORING_GCS_NAMESPACE environment variable')
param PmonitoringGcsNamespace string

@description('Sets the MONITORING_GCS_AUTH_ID environment variable.')
param PmonitoringGcsAuthId string

@description('Sets the MONITORING_CONFIG_VERSION environment variable.')
param PmonitoringConfigVersion string

@description('Allows user to target a region other than the resource group region.')
param PcomputeRegionOverride string 

var siteLocation = (toLower(PcomputeRegionOverride)=='none' ? resourceGroup().location : PcomputeRegionOverride)
var configJson = {
  MONITORING_TENANT: PmonitoringTenant
  MONITORING_ROLE: PmonitoringRole
  MONITORING_XSTORE_ACCOUNTS: 'GCSPlaceholder'
  AdditionalEnvironmentVariables: [
    {
      Key: 'DATACENTER'
      Value: siteLocation
    }
    {
      Key: 'MONITORING_GCS_ENVIRONMENT'
      Value: PmonitoringGcsEnvironment
    }
    {
      Key: 'MONITORING_GCS_ACCOUNT'
      Value: PmonitoringGcsAccount
    }
    {
      Key: 'MONITORING_GCS_NAMESPACE'
      Value: PmonitoringGcsNamespace
    }
    {
      Key: 'MONITORING_GCS_REGION'
      Value: siteLocation
    }
    {
      Key: 'MONITORING_GCS_AUTH_ID'
      Value: PmonitoringGcsAuthId
    }
    {
      Key: 'MONITORING_GCS_AUTH_ID_TYPE'
      Value: 'AuthKeyVault'
    }
    {
      Key: 'MONITORING_CONFIG_VERSION'
      Value: PmonitoringConfigVersion
    }
    {
      Key: 'MONITORING_USE_GENEVA_CONFIG_SERVICE'
      Value: 'true'
    }
  ]
}
var configXml = '<MonitoringManagement eventVersion="1" version="1.0" timestamp="2017-12-29T00:00:00Z" namespace="PlaceHolder"></MonitoringManagement>'


resource appServicePlan 'Microsoft.Web/serverfarms@2021-03-01'= if(deployInfra) {
  name: PservicePlanName
  location: resourceGroup().location
  sku: {
    name: PservicePlanSkuName
    tier: PservicePlanSkuTier
    capacity: int(PservicePlanSkuCapacity)
  }
  properties: {
    reserved: false
    }
}


resource appServicePlanName_AntMDS_ConfigJson 'Microsoft.Web/serverfarms/firstPartyApps/settings@2015-08-01' = if(deployInfra) {
  name: '${PservicePlanName}/AntMDS/ConfigJson'
  location: siteLocation
  properties: {
    firstPartyId: 'AntMDS'
    settingName: 'ConfigJson'
    settingValue: string(configJson)
  }
  dependsOn: [
    appServicePlan
  ]
}

resource appServicePlanName_AntMDS_MdsConfigXml 'Microsoft.Web/serverfarms/firstPartyApps/settings@2015-08-01' = if(deployInfra) {
  name: '${PservicePlanName}/AntMDS/MdsConfigXml'
  properties: {
    firstPartyId: 'AntMDS'
    settingName: 'MdsConfigXml'
    settingValue: configXml
  }
  dependsOn: [
    appServicePlan
  ]
}

resource appServicePlanName_AntMDS_CERTIFICATE_PFX_GENEVACERT 'Microsoft.Web/serverfarms/firstPartyApps/keyVaultSettings@2015-08-01' = if(deployInfra) {
  name: '${PservicePlanName}/AntMDS/CERTIFICATE_PFX_GENEVACERT'
  properties: {
    firstPartyId: 'AntMDS'
    settingName: 'CERTIFICATE_PFX_GENEVACERT'
    vaultId: PgenevaCertVaultId
    secretName: PgenevaCertSecretName
  }
  dependsOn: [
    appServicePlan
  ]
}

resource appServicePlanName_AntMDS_CERTIFICATE_PASSWORD_GENEVACERT 'Microsoft.Web/serverfarms/firstPartyApps/settings@2015-08-01' = if(deployInfra) {
  name: '${PservicePlanName}/AntMDS/CERTIFICATE_PASSWORD_GENEVACERT'
  properties: {
    firstPartyId: 'AntMDS'
    settingName: 'CERTIFICATE_PASSWORD_GENEVACERT'
    settingValue: ''
  }
  dependsOn: [
    appServicePlan
  ]
}

output servicePlanId string =deployInfra? appServicePlan.id :''



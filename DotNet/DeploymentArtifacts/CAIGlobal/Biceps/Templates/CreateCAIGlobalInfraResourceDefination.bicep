param PcreateCaiGlobalInfraInput object
var PmuiName = PcreateCaiGlobalInfraInput.PmuiName
var PlogAnalyticsWorspaceName = PcreateCaiGlobalInfraInput.PlogAnalyticsWorkspaceName
var PappInsightsName = PcreateCaiGlobalInfraInput.PappInsightsName
var PstorageSkuName = PcreateCaiGlobalInfraInput.PstorageSkuName
var PstorageKind = PcreateCaiGlobalInfraInput.PstorageKind
var PkeyVaultName = PcreateCaiGlobalInfraInput.PkeyVaultName
var PcosmosDbccountsName = PcreateCaiGlobalInfraInput.PcosmosDbccountsName
var PcosmosdbDatabaseCollectionsThroughputMapping = PcreateCaiGlobalInfraInput.PcosmosdbDatabaseCollectionsThroughputMapping
var PcosmosdbDatabases = PcreateCaiGlobalInfraInput.PcosmosdbDatabases
var PdeployCosmosDbAccount = PcreateCaiGlobalInfraInput.PdeployCosmosDbAccount
var PcosmosLocation = PcreateCaiGlobalInfraInput.PcosmosLocation
var PcosmosGeoRepLocations = PcreateCaiGlobalInfraInput.PcosmosGeoRepLocations
var PCosmosBackupPolicyTier = PcreateCaiGlobalInfraInput.PCosmosBackupPolicyTier
var PvectorEmbeddingCollections = PcreateCaiGlobalInfraInput.PvectorEmbeddingCollections
var PeventHubNamespaceName = PcreateCaiGlobalInfraInput.PeventHubNamespaceName
var PeventHubNames = PcreateCaiGlobalInfraInput.PeventHubNames
var PeventHubNamespaceSkuCapacity = PcreateCaiGlobalInfraInput.PeventHubNamespaceSkuCapacity
var PeventhubNamespacePartitionCount = PcreateCaiGlobalInfraInput.PeventhubNamespacePartitionCount
var PdefaultEventHubConsumerGroupNames = PcreateCaiGlobalInfraInput.PdefaultEventHubConsumerGroupNames
var PmessageRetentionInDays = PcreateCaiGlobalInfraInput.PmessageRetentionInDays

var PsearchServiceMapping =  PcreateCaiGlobalInfraInput.PsearchServiceMapping
var PdeploySearchService = PcreateCaiGlobalInfraInput.PdeploySearchService
var PsearchserviceSku = PcreateCaiGlobalInfraInput.PsearchserviceSku
var PsearchservicereplicaCount = PcreateCaiGlobalInfraInput.PsearchservicereplicaCount
var PsearchservicepartitionCount = PcreateCaiGlobalInfraInput.PsearchservicepartitionCount
var PsearchServiceRoleDefinationIds =PcreateCaiGlobalInfraInput.PsearchServiceRoleDefinationIds
var PdeploySecondaryRegionSearch = PcreateCaiGlobalInfraInput.PdeploySecondaryRegionSearch

var PWafPolicyName =PcreateCaiGlobalInfraInput.PWafPolicyName
var PafdWafPolicySku = PcreateCaiGlobalInfraInput.PafdWafPolicySku

var PAFDName = PcreateCaiGlobalInfraInput.PAFDName
var PoriginGroup = PcreateCaiGlobalInfraInput.PoriginGroup
var PWafPolicyPatternMatch = PcreateCaiGlobalInfraInput.PWafPolicyPatternMatch

var PfoundryAccountName = PcreateCaiGlobalInfraInput.PfoundryAccountName
var PdeployFoundry = PcreateCaiGlobalInfraInput.PdeployFoundry
var PfoundryModelDeployments = PcreateCaiGlobalInfraInput.PfoundryModelDeployments
var PfoundryCreateProject = PcreateCaiGlobalInfraInput.PfoundryCreateProject

var PreportsStorageAccountName = PcreateCaiGlobalInfraInput.PreportsStorageAccountName
var PdeployReportsStorage = PcreateCaiGlobalInfraInput.PdeployReportsStorage
var PreportsContainerNames = PcreateCaiGlobalInfraInput.PreportsContainerNames

module CreateAppMui 'Core/MUIResourceDefination.bicep'={
  name:'AppMUIResourceDefination'
  params:{
     PmuiName:PmuiName
  }
  dependsOn:[]
}



module CreateLogAnalyticsWorkspace 'Core/LogAnalyticsWorkspaceResourceDefinition.bicep'={
  name:'LogAnalyticsWorkspaceResourceDefinition'
  params:{
    PlogAnalyticsWorspaceName:PlogAnalyticsWorspaceName
  }
  dependsOn:[]

}

module CreateAppInsights 'Core/AppInsightsResourceDefinition.bicep'={
  name:'AppInsightsResourceDefinition'
  params:{
    PappInsightsName:PappInsightsName
    PlogAnalyticsWorkspaceId:CreateLogAnalyticsWorkspace.outputs.logAnalyticsWorkspaceId
  }
  dependsOn:[]

}


module CreateKeyvault 'Core/KeyVaultResourceDefination.bicep'={
  name:'KeyVaultResourceDefination'
  params:{
    PkeyVaultName:PkeyVaultName
    PKeyVaultSkuFamily:'A'
    PKeyVaultSkuName:'premium'
    Plocation:resourceGroup().location
  }
  dependsOn:[]
}


module CreateCosmosDbAccount 'Core/ScopedCosmosDbAccountResourceDefination.bicep'={
  name:'ScopedCosmosDbAccountResourceDefination'
  params:{
    PcosmosDbccountsName:PcosmosDbccountsName
    PcosmosdbDatabaseCollectionsThroughputMapping:PcosmosdbDatabaseCollectionsThroughputMapping
    PcosmosdbDatabases:PcosmosdbDatabases
     PdeployCosmosDbAccount:PdeployCosmosDbAccount
      PcosmosLocation:PcosmosLocation
      PcosmosGeoRepLocations: PcosmosGeoRepLocations
      PCosmosBackupPolicyTier:PCosmosBackupPolicyTier
      PvectorEmbeddingCollections:PvectorEmbeddingCollections
  }
  dependsOn:[]
}


module CreateEventHubNamespace 'Core/EventhubNamespaceResourceDefination.bicep'={
  name:'EventHubNamespaceResourceDefination'
  params:{
    PeventHubNamespaceName:PeventHubNamespaceName
    PeventHubNames:PeventHubNames
    PpartitionCount:PeventhubNamespacePartitionCount
    PeventHubNamespaceSkuCapacity:PeventHubNamespaceSkuCapacity
    PdefaultConsumerGroupNames:PdefaultEventHubConsumerGroupNames
    PeventHubNamespaceSkuName:'premium'
    PeventHubNamespaceSkuTier:'premium'
    PmessageRetentionInDays:PmessageRetentionInDays
  }
  dependsOn:[]
}


module CreateSearchService 'MultiSearchServiceResourceDefinition.bicep'={
  name:'SearchServiceResourceDefinition'
  params:{
     PdeploySearchService:PdeploySearchService
      PsearchServiceMapping:PsearchServiceMapping
       PsearchservicepartitionCount:PsearchservicepartitionCount
        PsearchservicereplicaCount:PsearchservicereplicaCount
         PsearchserviceSku:PsearchserviceSku
         PdeploySecondaryRegionSearch:PdeploySecondaryRegionSearch
  }
  dependsOn:[]
}


module SearchIndexRbacAssign 'MultiSearchServiceRbacAssignmentResourceDefinition.bicep'={
  name:'SearchIndexRbacAssignmentResourceDefinition'
  params:{
    PdeploySearchService:PdeploySearchService
    PsearchServiceMapping:PsearchServiceMapping
    PsearchServiceRoleDefinationIds:PsearchServiceRoleDefinationIds
    PresourceManagedIdentityId:CreateAppMui.outputs.userManagedIdentityPrincipleId
    PdeploySecondaryRegionSearch:PdeploySecondaryRegionSearch
  }
  dependsOn:[CreateSearchService]
}


module CreateFrontDoorWAFPolicy 'Core/AFDWAFPolicy.bicep'={
  name:'AFDWAFPolicy'
  params:{
    PWafPolicyName:PWafPolicyName
    PafdWafPolicySku:PafdWafPolicySku
  }
  dependsOn:[]
}



module CreateAzureFrontDoor 'Core/AFDResourceDefinition.bicep'={
  name:'AFDResourceDefinition'
  params:{
    PAFDName:PAFDName
    PFrontDoorExternalId:CreateFrontDoorWAFPolicy.outputs.AFDPolicyId
    PWafPolicyName:PWafPolicyName
    PafdWafPolicySku:PafdWafPolicySku
    PoriginGroup:PoriginGroup
    PWafPolicyPatternMatch:PWafPolicyPatternMatch
  }
  dependsOn:[]
}

@description('Create Azure AI Foundry account')
module CreateFoundry 'Core/AzFoundry.bicep'= if (bool(PdeployFoundry)) {
  name:'AzFoundryResourceDefinition'
  params:{
    PfoundryAccountName:PfoundryAccountName
    Plocation:resourceGroup().location
    PmodelDeployments:PfoundryModelDeployments
    PcreateProject:PfoundryCreateProject
  }
  dependsOn:[]
}

@description('Create Reports Storage account')
module CreateReportsStorageAccount 'Core/StorageResourceDefination.bicep'={
  name:'ReportsStorageAccountResourceDefinition'
  params:{
    PstorageAccountName:PreportsStorageAccountName
    PstorageSkuName:PstorageSkuName
    PstorageKind:PstorageKind
    PdeployInfra:PdeployReportsStorage
  }
  dependsOn:[]
}

module CreateReportsStorageContainers 'Core/StorageContainerResourceDefination.Bicep'= if (bool(PdeployReportsStorage)) {
  name:'ReportsStorageContainerResourceDefination'
  params:{
    PstorageAccountName:PreportsStorageAccountName
    PcontainerNames:PreportsContainerNames
  }
  dependsOn:[CreateReportsStorageAccount]
}

resource reportsStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = if (bool(PdeployReportsStorage)) {
  name: PreportsStorageAccountName
}

resource reportsLifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = if (bool(PdeployReportsStorage)) {
  name: 'default'
  parent: reportsStorageAccount
  properties: {
    policy: {
      rules: [
        {
          name: 'auto-delete-reports-after-30-days'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['reports/']
            }
            actions: {
              baseBlob: {
                delete: {
                  daysAfterCreationGreaterThan: 30
                }
              }
            }
          }
        }
      ]
    }
  }
  dependsOn: [CreateReportsStorageContainers]
}

output CreateAppMui_userManagedIdentityResourceId string =CreateAppMui.outputs.userManagedIdentityResourceId
output CreateAppMui_userManagedIdentityClientId string = CreateAppMui.outputs.userManagedIdentityClientId
output CreateAppMui_userManagedIdentityPrincipleId string = CreateAppMui.outputs.userManagedIdentityPrincipleId
output DefaultKeyValueName string= PcreateCaiGlobalInfraInput.PkeyVaultName
output ReportsStorageAccountName string= PreportsStorageAccountName

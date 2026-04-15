using './caiglobal.orchestrator.infra.bicep'
var env='ppe'
var location='dynamiclocation'
var appname='caiglobal'
var actuallocation ='$location()'
var config_failcaiglobaldeployment = 'false'
param PfailDeployment =  '${config_failcaiglobaldeployment}'

var PmuiName = 'mui-${appname}-${env}-${location}'
var PappInsightsName =  'ai-${appname}-${env}-${location}'
var PlogAnalyticsWorkspaceName =  'lg-${appname}-${env}-${location}'
var PstorageSkuName = 'Standard_GRS'
var PstorageKind = 'StorageV2'

var PkeyVaultName = 'kv-${appname}-${env}-${location}'
var PeventHubNamespaceName = 'ehns-${appname}-${env}-${location}'
var PeventHubNames = ['prompttelemetry','systemtelemetry','usertelemetry','orgtelemetry']
var PeventHubNamespaceSkuCapacity = '4'
var PeventhubNamespacePartitionCount = '32'
var PmessageRetentionInDays='31'
var PdefaultEventHubConsumerGroupNames = ['$Default']

var PcosmosDbccountsName =  'csms-${appname}-${env}-cm' 
var PcosmosdbDatabaseCollectionsThroughputMapping = [
  {collectionFullName:'csms-caiglobal-${env}-cm/CXOAI/Leases',collectionName:'Leases',partitionKey:'id',maxThroughput:'4000',defaultTtl:-1}
  {collectionFullName:'csms-caiglobal-${env}-cm/CXOAI/ConversationStore',collectionName:'ConversationStore',partitionKey:'UserId',maxThroughput:'10000',defaultTtl:-1}
  {collectionFullName:'csms-caiglobal-${env}-cm/CXOAI/ConfigurationStore',collectionName:'ConfigurationStore',partitionKey:'ComponentName',maxThroughput:'4000',defaultTtl:-1}]
var PcosmosdbDatabases =  ['CXOAI']
var PcosmosGeoRepLocations =  [
  { locationName: 'Canada Central', failoverPriority: 0, isZoneRedundant: false }
]
var PdeployCosmosDbAccount =  'true'
var PcosmosLocation =actuallocation
var PCosmosBackupPolicyTier ='Continuous7Days'
var PvectorEmbeddingCollections = [
  {collectionFullName:'csms-caiglobal-${env}-cm/CXOAI/MemoryStore',collectionName:'MemoryStore',partitionKey:'UserId',maxThroughput:'10000',defaultTtl:-1,embeddingPath:'/embedding',embeddingDataType:'float32',embeddingDimensions:512,embeddingDistanceFunction:'cosine',vectorIndexType:'quantizedFlat',quantizationByteSize:64}
]
var PAFDName = 'afd-${appname}-${env}-${location}'
var PWafPolicyName =  'waf${appname}${env}${location}'
var PafdWafPolicySku =  'Premium_AzureFrontDoor'
var PoriginGroup= [{name:'cxoaiapi-origin-group', sessionAffinityState:'Enabled'}]
var PWafPolicyPatternMatch =  '/*'

var PdeploySearchService =  'true'
var PdeploySecondaryRegionSearch = 'false'
var PsearchServiceMapping =  [{searchServiceName:'srch-${appname}-${env}-${location}',location:actuallocation},{searchServiceName:'srch-${appname}-${env}-ecan',location:'canadaeast'}]
var PsearchservicepartitionCount =  '1'
var PsearchservicereplicaCount =  '2'
var PsearchServiceRoleDefinationIds = ['b24988ac-6180-42a0-ab88-20f7382dd24c','7ca78c08-252a-4471-8644-bb5ff32d4ba0']
var PsearchserviceSku =  'standard'

var PfoundryAccountName = 'foundry-${appname}-${env}-${location}'
var PdeployFoundry = 'true'
var PfoundryModelDeployments = [
  {name:'gpt-4o-mini', modelName:'gpt-4o-mini', format:'OpenAI', skuName:'GlobalStandard', capacity:2000, versionUpgradeOption:'OnceNewDefaultVersionAvailable', raiPolicyName:'Microsoft.DefaultV2'}
  {name:'gpt-4o', modelName:'gpt-4o', format:'OpenAI', skuName:'GlobalStandard', capacity:450, versionUpgradeOption:'OnceNewDefaultVersionAvailable', raiPolicyName:'Microsoft.DefaultV2'}
  {name:'text-embedding-3-small', modelName:'text-embedding-3-small', format:'OpenAI', skuName:'GlobalStandard', capacity:1000, versionUpgradeOption:'OnceNewDefaultVersionAvailable', raiPolicyName:'Microsoft.DefaultV2'}
] // replace
var PfoundryCreateProject = false

var PreportsStorageAccountName = 'stmreports${env}${location}'
var PdeployReportsStorage = 'true'
var PreportsContainerNames = ['reports']

param PcreateCaiGlobalInfraInput ={
  PmuiName:PmuiName
  PlogAnalyticsWorkspaceName:PlogAnalyticsWorkspaceName
  PappInsightsName:PappInsightsName
  PstorageSkuName:PstorageSkuName
  PstorageKind:PstorageKind
  PkeyVaultName:PkeyVaultName
  PcosmosDbccountsName:PcosmosDbccountsName
  PcosmosdbDatabaseCollectionsThroughputMapping:PcosmosdbDatabaseCollectionsThroughputMapping
  PcosmosdbDatabases:PcosmosdbDatabases
  PdeployCosmosDbAccount:PdeployCosmosDbAccount
  PcosmosLocation:PcosmosLocation
  PcosmosGeoRepLocations:PcosmosGeoRepLocations
  PCosmosBackupPolicyTier:PCosmosBackupPolicyTier
  PvectorEmbeddingCollections:PvectorEmbeddingCollections
  PeventHubNamespaceName:PeventHubNamespaceName
  PeventHubNames:PeventHubNames
  PeventHubNamespaceSkuCapacity:PeventHubNamespaceSkuCapacity
  PeventhubNamespacePartitionCount:PeventhubNamespacePartitionCount
  PdefaultEventHubConsumerGroupNames:PdefaultEventHubConsumerGroupNames
  PmessageRetentionInDays:PmessageRetentionInDays
  PsearchServiceMapping:PsearchServiceMapping
  PsearchServiceRoleDefinationIds:PsearchServiceRoleDefinationIds
  PdeploySearchService:PdeploySearchService
  PsearchservicereplicaCount:PsearchservicereplicaCount
  PsearchservicepartitionCount:PsearchservicepartitionCount
  PsearchserviceSku:PsearchserviceSku
  PdeploySecondaryRegionSearch:PdeploySecondaryRegionSearch
  PWafPolicyName:PWafPolicyName
  PafdWafPolicySku:PafdWafPolicySku
  PAFDName:PAFDName
  PoriginGroup:PoriginGroup
  PWafPolicyPatternMatch:PWafPolicyPatternMatch
  PfoundryAccountName:PfoundryAccountName
  PdeployFoundry:PdeployFoundry
  PfoundryModelDeployments:PfoundryModelDeployments
  PfoundryCreateProject:PfoundryCreateProject
  PreportsStorageAccountName:PreportsStorageAccountName
  PdeployReportsStorage:PdeployReportsStorage
  PreportsContainerNames:PreportsContainerNames
}
param PkeyvaultRoleDefinationIds =  [ 'b24988ac-6180-42a0-ab88-20f7382dd24c','f25e0fa2-a7c8-4377-a976-54943a77a395','21090545-7ca7-4776-b22c-e363652d74d2', '00482a5a-887f-4fb3-b363-3b7fe8e74483',   'a4417e6f-fecd-4de8-b567-7b0420556985',  'b86a8fe4-44ce-4948-aee5-eccb2c155cd7']
param PresourceGroupRoleDefinationIds=['b24988ac-6180-42a0-ab88-20f7382dd24c']
param Pev2AssistedIdentityAppObjectId =  '13b2d604-52d3-4964-af01-73f14447efd5' //enterprise app object id
param PgenevaMSAppServiceObjectId='f8daea97-62e7-4026-becf-13c2ea98e8b4'
param PdeployKeyvaultCertificates = 'true'
param assistedIdentityKeyVaultName =  'kv-nrtglbev2-test-eastus'  //replace
param commandsToExecuteInShell =  ['/bin/bash','-c','pwsh KeyVaultPublicAccessChange.ps1']
param ev2KeyVaultResourceGroupName =  'rg-nrtglobal-test-eastus'  //replace
param Pev2PackageName =  'package.zip'
// param PmuiEv2RgIdentityClientId =  '6f8b246d-d690-4f9f-89fe-3e9e819eb435'  //mui enterprise app id
// param PmuiEv2RgResourceId =  '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-test-eastus/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mui-nrtglobal-test-eastus'
param PShellMaxExecutionTime=  'PT20M'
param publicNetworkAccessStateDisabled = 'Disabled'
param publicNetworkAccessStateEnabled =  'Enabled'
param PcertkeyVaultPrivateIssuer =  'OneCertV2-PrivateCA'
param PcertkeyvaultProviderName =  'OneCertV2-PrivateCA'
param PcertKeyvaultSecureId =  'https://kv-nrtglbev2-test-eastus.vault.azure.net/secrets/ADFEv2DeploymentAssistedIdentityCert'
param PcertificateName =  'sdp-geneva-auth-certificate'  //replace
param PcnSubjectName =  '${env}.geneva.keyvault.cxpes.microsoft.com'
param Pev2ApplicationAppId='0319dcf8-f436-49ca-8e8b-d9f7e68fa0d4'  //replace
param commandsToExecuteInShellSearchIndex = ['/bin/bash','-c','pwsh CreateSearchIndex.ps1']
param PcsvAzureSearchServiceNames = 'srch-${appname}-${env}-ccan, srch-${appname}-${env}-ecan'
param PcsvSearchIndexes = 'cxoaiconfigurationstore'
param PdeploySearchIndexes = 'true'
param PSearchIndexDefinitionRootPath =  './Config/SearchIndex'
param PdeployAssistantKeyvaultRoleAssignment='true'
param PdeployEv2Keyvault='true'

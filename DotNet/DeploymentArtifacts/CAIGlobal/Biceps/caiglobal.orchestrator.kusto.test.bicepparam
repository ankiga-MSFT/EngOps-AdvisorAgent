using './caiglobal.orchestrator.kusto.bicep'
var env='test'
var appname='caiglobal'
var actuallocation ='$location()'
var config_failicmkustodeployment = 'false'
param PfailDeployment =  '${config_failicmkustodeployment}'

param PkustoClusterResourceId = '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-Support-wus3-${env}/providers/Microsoft.Kusto/clusters/supportadxwus3${env}'
param PkustoDatabaseNames = ['ICMRatioData']
param PdeployICMRatioData = 'true'

param PeventHubNameResourceIdsConsumerMappings = [{
  eventhubResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.EventHub/namespaces/ehns-icm-srevent-${env}-ccan/eventhubs/srchange'
  consumergroupName:'srchange'
}]
param PdeployEventhubConsumerGroup = 'true'

param pkustoManagedPrivateEndpointConnectionMapping = [{
  kustoConnectionKind: 'EventHubNamespace'
  managePrivateEndpointName: 'mpe-srchange-eh'
  message: 'Please approve the connection'
  resourceId: '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.EventHub/namespaces/ehns-icm-srevent-${env}-ccan'
}
{
  kustoConnectionKind: 'CosmosDb'
  managePrivateEndpointName: 'mpe-ratiolinkage-cosmos'
  message: 'Please approve the connection'
  resourceId: '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.DocumentDB/databaseAccounts/csms-caiglobal-${env}-cm'
}]
param PdeployKustoManagedPrivateEndpoint = 'true'

param PcosmodbresourceIdsPrincipleMappings = [{
  cosmosDbAccountResourceId: '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.DocumentDB/databaseAccounts/csms-caiglobal-${env}-cm'
  roleDefinitionId: '00000000-0000-0000-0000-000000000001'
  principleId: 'fbf55c5d-a294-4c43-9ad8-7b0e82b2fe24'
  appName: 'icmratio'
  deploySqlAssignment: 'true'
  deployRoleAssignment: 'true'
}]
param PdeployCosmosRoleAssignment = 'true'

param PeventhubresourceIdsPrincipleMappings = [{
  eventhubResourceId: '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.EventHub/namespaces/ehns-icm-srevent-${env}-ccan/eventhubs/srchange'
  roleDefinationId: 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde'
  principleId: 'fbf55c5d-a294-4c43-9ad8-7b0e82b2fe24'
}]
param PdeployEventhubRoleAssignment = 'true'

param PkustoDataConnectionsMappings = [{
  kustoDatabaseName:'ICMRatioData'
  kustoDataConnectionName:'eh-srchange'
  kustoConnectionKind:'EventHub'
  eventHubResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.EventHub/namespaces/ehns-icm-srevent-${env}-ccan/eventhubs/srchange'
  eventhubConsumerGroup:'srchange'
  mappingRuleName:'SRChange_mapping'
  retrievalStartDate:'2023-10-01T00:00:00Z'
  tableName:'SRChange'
  }
  {
  kustoDatabaseName:'ICMRatioData'
  kustoDataConnectionName:'cosmosdb-SrSnapshot'
  kustoConnectionKind:'CosmosDb'
  cosmosDbAccountResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.DocumentDB/databaseAccounts/csms-caiglobal-${env}-cm'
  cosmosDbContainer:'SrSnapshot'
  cosmosDbDatabase:'SRData'
  mappingRuleName:'SrSnapshot_mapping'
  retrievalStartDate:'2023-10-01T00:00:00Z'
  tableName:'SrSnapshot'
  }
  {
  kustoDatabaseName: 'ICMRatioData'
  kustoDataConnectionName: 'cosmosdb-RatioLinkage'
  kustoConnectionKind: 'CosmosDb'
  cosmosDbAccountResourceId: '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.DocumentDB/databaseAccounts/csms-caiglobal-${env}-cm'
  cosmosDbContainer: 'RatioLinkage'
  cosmosDbDatabase: 'SRData'
  mappingRuleName: 'RatioLinkage_mapping'
  retrievalStartDate: '2023-10-01T00:00:00Z'
  tableName: 'RatioLinkage'
}
{
  kustoDatabaseName: 'ICMRatioData'
  kustoDataConnectionName: 'cosmosdb-IcmL2Aggregates'
  kustoConnectionKind: 'CosmosDb'
  cosmosDbAccountResourceId: '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.DocumentDB/databaseAccounts/csms-caiglobal-${env}-cm'
  cosmosDbContainer: 'IcmL2Aggregates'
  cosmosDbDatabase: 'SRData'
  mappingRuleName: 'IcmL2Aggregate_mapping'
  retrievalStartDate: '2023-10-01T00:00:00Z'
  tableName: 'IcmL2Aggregate'
}
{
  kustoDatabaseName: 'ICMRatioData'
  kustoDataConnectionName: 'cosmosdb-IncidentLinkage'
  kustoConnectionKind: 'CosmosDb'
  cosmosDbAccountResourceId: '/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-caiglobal-${env}-canadacentral/providers/Microsoft.DocumentDB/databaseAccounts/csms-caiglobal-${env}-cm'
  cosmosDbContainer: 'IncidentLinkage'
  cosmosDbDatabase: 'SRData'
  mappingRuleName: 'IncidentLinkage_mapping'
  retrievalStartDate: '2023-10-01T00:00:00Z'
  tableName: 'IncidentLinkage'
}]
param PdeployKustoDataConnection = 'true'

param PdeployKustoRoleAssignment =  'true' 
param PkustoResourceIdsPrincipleMappings =  [{kustoDatabaseName:'ICMRatioData',principleid:'be55f430-c2ad-4523-88f4-8f97b0d7237e',tenantid:'72f988bf-86f1-41af-91ab-2d7cd011db47',role:'Admin',principleType:'User'},{kustoDatabaseName:'ICMRatioData',principleid:'21ca88c9-f159-4c8e-8264-a7fa3855d542',tenantid:'72f988bf-86f1-41af-91ab-2d7cd011db47',role:'Admin',principleType:'User'},{kustoDatabaseName:'ICMRatioData',principleid:'170320f6-b396-401b-b7be-1048cb5bab4e',tenantid:'72f988bf-86f1-41af-91ab-2d7cd011db47',role:'Admin',principleType:'App'},{kustoDatabaseName:'SupportValidationData',principleid:'170320f6-b396-401b-b7be-1048cb5bab4e',tenantid:'72f988bf-86f1-41af-91ab-2d7cd011db47',role:'Admin',principleType:'App'}]


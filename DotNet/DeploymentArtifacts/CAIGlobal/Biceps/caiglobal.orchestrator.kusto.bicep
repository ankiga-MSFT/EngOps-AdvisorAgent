
metadata PsubscriptionOwnerMuiResourceId='__SUBSCRIPTION_OWNER_MUI_RESOURCE_ID__'
metadata Plocation='__CURRENT_APPLICATION_LOCATION__'


metadata PfailDeployment='__FAIL_DEPLOYMENT__'
param PfailDeployment string

module StopDeployment 'Templates/Core/Extension/ConfigurableStopByFailureDeployment.bicep'={
  name:'ConfigurableStopByFailureDeployment'
  params:{
    PfailDeployment: PfailDeployment
  }
  dependsOn:[]
}

metadata PkustoClusterResourceId = '__KUSTO_CLUSTER_RESOURCE_ID__'
param PkustoClusterResourceId string
metadata PkustoDatabaseNames= '__KUSTO_DATABASE_NAMES__'
param PkustoDatabaseNames array
metadata PdeployICMRatioData= '__DEPLOYICMRATIODATA__'
param PdeployICMRatioData string

// Does this create the Kusto database too?
@description('Create Kusto database')
module DeployKustoScript 'Templates/auto.scopedKustoScriptDefination.bicep' ={
name: 'DeployKustoScript'
params:{
PKustoClusterResourceId: PkustoClusterResourceId
PkustoDatabaseNames: PkustoDatabaseNames
PdeployICMRatioData: PdeployICMRatioData
  }
  dependsOn:[StopDeployment]
}

metadata PkustoResourceIdsPrincipleMappings='__KUSTO_RESOURCE_IDS_ROLE_DEFINATION_IDS_MAPPINGS__'
param PkustoResourceIdsPrincipleMappings array

metadata PdeployKustoRoleAssignment ='__DEPLOY_KUSTO_ROLE_ASSIGNMENT__'
param PdeployKustoRoleAssignment string

@description('Rabac to Kusto cluster')
module KustoClusterRbac 'Templates/Core/ScopedKustoRbacResourceDefination.bicep'= {
  name:'ScopedKustoClusterRbac'
  params:{
    PkustoClusterResourceId:PkustoClusterResourceId
    PkustoResourceIdsPrincipleMappings:PkustoResourceIdsPrincipleMappings
    PdeployKustoRoleAssignment:PdeployKustoRoleAssignment

  }
  dependsOn:[DeployKustoScript]
}



/////////////////////////ADD Consumer Group to eventhub///////////////////////////
metadata PeventHubNameResourceIdSConsumerMappings ='__EVENTHUB_RESOURCE_IDS_CONSUMER_MAPPINGS__'
param PeventHubNameResourceIdsConsumerMappings array

metadata PdeployEventhubConsumerGroup ='__DEPLOY_EVENTHUB_CONSUMER_GROUP__'
param PdeployEventhubConsumerGroup string

@description('Create Consumer Group on the Event Hub')
module EventhubConsumerGroup './Templates/ScopedEventhubConsumerGroupResourceDefination.bicep'={
  name:'ScopedEventhubConsumerGroup'
  params:{
    PeventHubNameResourceIdsConsumerMappings:PeventHubNameResourceIdsConsumerMappings
    PdeployEventhubConsumerGroup:PdeployEventhubConsumerGroup
  }
  dependsOn:[StopDeployment]
}

/////////////////////////Adding Managed Private endpoint to kusto///////////////////////////

metadata pkustoManagedPrivateEndpointConnectionMapping='__KUSTO_MANAGED_PRIVATE_ENDPOINT_CONNECTION_MAPPINGS__'
param pkustoManagedPrivateEndpointConnectionMapping array

metadata PdeployKustoManagedPrivateEndpoint ='__DEPLOY_KUSTO_MANAGED_PRIVATE_ENDPOINT__'
param PdeployKustoManagedPrivateEndpoint string


@description('Managed Private Endpoint to Kusto cluster')
module KustoClusterManagedPrivateEndpoint 'Templates/ScopedKustoManagedPrivateEndpointResourceDefination.bicep'= {
  name:'ScopedKustoClusterManagedPrivateEndpoint'
  params:{
     PkustoClusterResourceId:PkustoClusterResourceId
     PdeployKustoManagedPrivateEndpoint:PdeployKustoManagedPrivateEndpoint
      pkustoManagedPrivateEndpointConnectionMapping:pkustoManagedPrivateEndpointConnectionMapping
  }
  dependsOn:[StopDeployment]
}

/////////////////////////Add Cosmos db sql Role assignment///////////////////////////
metadata PcosmodbresourceIdsPrincipleMappings='__COSMOSDB_RESOURCE_IDS_ROLE_DEFINATION_IDS_MAPPINGS__'
param PcosmodbresourceIdsPrincipleMappings array

metadata PdeployCosmosRoleAssignment ='__DEPLOY_COSMOS_ROLE_ASSIGNMENT__'
param PdeployCosmosRoleAssignment string

@description('Create CosmosDB SQL Role Assignment')
module AssignCosmosDbRoles 'Templates/ScopedCosmosDbRbacResourceDescription.bicep' = {
  name: 'ScopedAssignCosmosDbRoles'
  params: {
    PresourceIdsPrincipleMappings: PcosmodbresourceIdsPrincipleMappings
    PdeployCosmosRoleAssignment:PdeployCosmosRoleAssignment
  }
  dependsOn:[StopDeployment]
}

/////////////////////////Add Role assignment to eventhub///////////////////////////
metadata PeventhubresourceIdsPrincipleMappings='__EVENTHUB_RESOURCE_IDS_ROLE_DEFINATION_IDS_MAPPINGS__'
param PeventhubresourceIdsPrincipleMappings array

metadata PdeployEventhubRoleAssignment ='__DEPLOY_EVENTHUB_ROLE_ASSIGNMENT__'
param PdeployEventhubRoleAssignment string

@description('Add Role Assignments to the Event Hub')
module EventhubRolesAssignment './Templates/ScopedEventhubRbacAssignmentResourceDefinition.bicep'={
  name:'ScopedEventhubRolesAssignment'
  params:{
    PresourceIdsPrincipleMappings:PeventhubresourceIdsPrincipleMappings
    PdeployEventhubRoleAssignment:PdeployEventhubRoleAssignment
      
  }
  dependsOn:[StopDeployment]
}

/////////////////////////Adding Kusto DB Data connection///////////////////////////
metadata PkustoDataConnectionsMappings='__KUSTO_DATA_CONNECTIONS_MAPPINGS__'
param PkustoDataConnectionsMappings array

metadata PdeployKustoDataConnection ='__DEPLOY_KUSTO_DATA_CONNECTION__'
param PdeployKustoDataConnection string


@description('Data connection to Kusto cluster')
module KustoClusterDataConnection './Templates/ScopedKustoDirectIngestionResourceDefination.bicep'= {
  name:'ScopedKustoClusterDataConnection'
  params:{
     PkustoClusterResourceId:PkustoClusterResourceId
      PdeployKustoDataConnection:PdeployKustoDataConnection
      PkustoDataConnectionsMappings:PkustoDataConnectionsMappings
  }
  dependsOn:[EventhubConsumerGroup,KustoClusterManagedPrivateEndpoint,EventhubRolesAssignment,AssignCosmosDbRoles, DeployKustoScript]
}




// ////az deployment group  create -g rg-caiglobal-test-canadacentral  -f caiglobal.orchestrator.kusto.bicep -p caiglobal.orchestrator.kusto.test.bicepparam

# SupportUtility Modules details and parameter schema


## 01.--------------------ADD Consumer Group to any eventhub-------------------------------
### Multiple deployment supported: yes
### param PeventHubNameResourceIdsConsumerMappings array
     
     ```bicep schema
     [
        {	eventhubResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-${env}-eastus/providers/Microsoft.EventHub/namespaces/ehns-nrt-rawevent-${env}-eastus/eventhubs/rawtest', 
            consumergroupName:'dckusto1'
        }
    ]
    ```
    1.eventhubResourceId : full resourceId of eventhub not eventhubnamespace
    2.consumergroupName  : name of the consumergroup
### param PdeployEventhubConsumerGroup string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module
   

## 02.--------------------Add Role assignment to eventhub-------------------------------
### Multiple deployment supported: yes
### param PeventhubresourceIdsPrincipleMappings array
    ``` bicep schema
     [
        {
            eventhubResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-${env}-eastus/providers/Microsoft.EventHub/namespaces/ehns-nrt-rawevent-${env}-eastus/eventhubs/rawtest',
            roleDefinationId:'a638d3c7-ab3a-418d-83e6-5f17a39d4fde',
            principleId:'fbf55c5d-a294-4c43-9ad8-7b0e82b2fe24'
        }
    ]
    ```
    1.eventhubResourceId : full resourceId of eventhub not eventhubnamespace
    2.roleDefinationId   : you need to get the roledefination id from eventhub roleassignment role (specific)->json view -> roleurl -> guid
    3.principleId        : its is the object id of app , user, group, you can get the object id from azure portal (microsoft entra id) 
### param PdeployEventhubRoleAssignment string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 03.--------------------Remove Role assignment to eventhub (Not Working)-------------------------------
### Multiple deployment supported: yes
### param PeventHubRemoveResourceIdsPrincipleMappings array
    ``` bicep schema
     [
        {
            eventhubResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-${env}-eastus/providers/Microsoft.EventHub/namespaces/ehns-nrt-rawevent-${env}-eastus/eventhubs/rawtest',
            roleDefinitionId:'a638d3c7-ab3a-418d-83e6-5f17a39d4fde',
            principleId:'fbf55c5d-a294-4c43-9ad8-7b0e82b2fe24',
            muiResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-test-eastus/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mui-nrtglobal-test-eastus'
        }
    ]
    ```
    1.eventhubResourceId : full resourceId of eventhub not eventhubnamespace
    2.roleDefinationId   : you need to get the roledefination id from eventhub roleassignment role (specific)->json view -> roleurl -> guid
    3.principleId        : its is the object id of app , user, group, you can get the object id from azure portal (microsoft entra id) 
    4.muiResourceId      : here you need to provide resourcId of an mui which has contributor role on eventhubnamespace or eventhub or resourcegroup
### param PdeployEventhubRemoveRoleAssignment string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 04.--------------------Add Cosmos db SQL Role assignment-------------------------------
### Multiple deployment supported: yes
### param PcosmodbresourceIdsPrincipleMappings array
    ``` bicep schema
    [
        {
            PcosmosDbAccountResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-cxpesinfra-test-sdp-cm/providers/Microsoft.DocumentDB/databaseAccounts/cosmos-cxpes-test-sdp-cm',
            roleDefinitionId:'00000000-0000-0000-0000-000000000001',
            principleId:'fbf55c5d-a294-4c43-9ad8-7b0e82b2fe24',
            appName:'iridiasTeam'
        }
    ]
    ```
    1.PcosmosDbAccountResourceId : full cosmosdb resourceId 
    2.roleDefinationId           : '00000000-0000-0000-0000-000000000001' for Reader, '00000000-0000-0000-0000-000000000002' for Reader/writer access
    3.principleId                : its is the object id of app , user, group, you can get the object id from azure portal (microsoft entra id) 
    4.appName                    : team / app / user name to identify role uniquely
### param PcosmodbresourceIdsPrincipleMappings string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 05.--------------------Function App stop/start/restart-------------------------------
### Multiple deployment supported: yes
### param PfunctionResourceIdsCommandMappings array
    ``` bicep schema
    [
        {
            functionAppResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-ingestor-test-northcentralus/providers/Microsoft.Web/sites/fun-ingestor-test-ncus',
            muiResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-ingestor-test-northcentralus/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mui-ingestor-test-ncus',
            slotName:'Production',
            command:'Start'
        }
    ]
    ```
    1.functionAppResourceId : full function app resourceId 
    2.muiResourceId         : here you need to provide resourcId of an mui which has web contributor role on azure function app
    3.slotName              : 'Production' or 'Staging'
    4.command               : 'Start' or 'Stop' or 'Restart'
### param PdeployFunctionReset string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 06.--------------------Create new Kusto DB-------------------------------
### Multiple deployment supported: yes
### param PkustoClusterResourceId string
    1. Kusto cluster full resourceId
### param PcreateKustoNewDatabaseNames array
    ``` bicep schema
    [
        'dctest',
        'dctest2'
    ] 
    ```
    1.provide list of db names to be created
### param PdeployCreateKustoDatabases string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 07.--------------------Adding Kusto DB role Assignment-------------------------------
### Multiple deployment supported: yes
### param PkustoClusterResourceId string
    1. Kusto cluster full resourceId
### param PkustoResourceIdsPrincipleMappings array
    ``` bicep schema
    [
        {
            kustoDatabaseName:'dctest',
            principleid:'be55f430-c2ad-4523-88f4-8f97b0d7237e',
            tenantid:'72f988bf-86f1-41af-91ab-2d7cd011db47',
            role:'Admin',
            principleType:'User'
        }
    ]
    ```
    1.kustoDatabaseName : Kusto db name
    2.tenantid          : '72f988bf-86f1-41af-91ab-2d7cd011db47' for MSFT, '33e01921-4d64-4f8c-a055-5bdaffd5e33d' for AME
    3.principleId       : its is the object id of app , user, group, you can get the object id from azure portal (microsoft entra id) 
    4.role              : 'Admin' or 'Viewer'
    5.principleType     : 'User' or 'Group' or 'App'
### param PdeployKustoRoleAssignment string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 08.--------------------Adding Managed Private endpoint to kusto-------------------------------
### Multiple deployment supported: yes
### param PkustoClusterResourceId string
    1. Kusto cluster full resourceId
### param pkustoManagedPrivateEndpointConnectionMapping array
    ``` bicep schema
    [
        {
            kustoConnectionKind:'EventHubNamespace',
            managePrivateEndpointName:'mpe-dctestingestion',
            message:'Please approve the connection',
            resourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-${env}-eastus/providers/Microsoft.EventHub/namespaces/ehns-nrt-rawevent-${env}-eastus'
        }
    ]
    ```
    1.kustoConnectionKind       : 'EventHubNamespace' or 'Storage' or 'Kusto' or 'CosmosDb'
    2.managePrivateEndpointName : name of the endpoint(must be unique)
    3.message                   : message for approval
    4.resourceId                : resourceId of eventhubnamespace or storageaccount or kusto cluster or Cosmosdb
### param pkustoManagedPrivateEndpointConnectionMapping string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 09.--------------------Adding Kusto DB Data connection-------------------------------
    ```NOTE:
    for a successfull kusto DB connection you need four steps, based on whether any of these steps are already done you need to toggle related module deploy flag
    Data connection is possible for Eventhub and Cosmosdb 
    1. Kusto cluster identity (objectId) should have access to eventhub (Event Hub consumer role ) and in case of cosmosdb Sql role for reader
    2. Managed private Endpoint between kusto to eventhubnamespace or cosmos db
    3. in case of eventhub, new consumergroup has to be created in that eventhub
    4. Kusto db table should be created with json mapping provided to it.(manual or via Kusto Ev2)
    ```
### Multiple deployment supported both Eventhub and Cosmos can be deployed together: yes
### param PkustoClusterResourceId string
    1. Kusto cluster full resourceId
### param PkustoDataConnectionsMappings array
    ``` bicep schema for Eventhub / CosmosDb
    NOTE: Schema for Eventhub and CosmosDb is different
     [
        {
            kustoDatabaseName:'dctest',
            kustoDataConnectionName:'dctestingestion',
            kustoConnectionKind:'EventHub',
            eventHubResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-${env}-eastus/providers/Microsoft.EventHub/namespaces/ehns-nrt-rawevent-${env}-eastus/eventhubs/rawtest',
            eventhubConsumerGroup:'dckusto1',
            mappingRuleName:'MSaaS_DocumentMapping',
            retrievalStartDate:'',
            tableName:'MSaaSSupportCases_Staging'
        },
        {
            kustoDatabaseName:'dctest',
            kustoDataConnectionName:'dctestingestion',
            kustoConnectionKind:'CosmosDb',
            cosmosDbAccountResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-${env}-eastus/providers/Microsoft.EventHub/namespaces/ehns-nrt-rawevent-${env}-eastus/eventhubs/rawtest',
            cosmosDbContainer:'dckusto1',
            cosmosDbDatabase:'dckusto1',
            mappingRuleName:'MSaaS_DocumentMapping',
            retrievalStartDate:'',
            tableName:'MSaaSSupportCases_Staging'
        },
    ]
    ```
    EVENTHUB SCHEMA
        1.kustoDatabaseName         : Kusto Database name
        2.kustoDataConnectionName   : Unique connection name for the kusto cluster
        3.kustoConnectionKind       : 'EventHub'
        4.eventHubResourceId        : resourceId of eventhub not eventhubnamespace
        4.eventhubConsumerGroup     : eventhub consumergroup
        4.mappingRuleName           : name of the json mapping that has been stored in kusto table where ingestion will happen
        4.retrievalStartDate        : UTC Json format , provide start time to ingest, if left empty, it will consume all data from beginning
        4.tableName                 : Kusto Database name
    COSMOS SCHEMA
        1.kustoDatabaseName         : Kusto Database name
        2.kustoDataConnectionName   : Unique connection name for the kusto cluster
        3.kustoConnectionKind       : 'EventHub'
        4.cosmosDbAccountResourceId : resourceId of cosmos db
        4.cosmosDbContainer         : cosmosDb collection name
        4.cosmosDbDatabase          : cosmosDb Database name
        4.mappingRuleName           : name of the json mapping that has been stored in kusto table where ingestion will happen
        4.retrievalStartDate        : UTC Json format , provide start time to ingest, if left empty, it will consume all data from beginning
        4.tableName                 : Kusto Database name
### param PdeployKustoDataConnection string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 10.--------------------Removing Data connection from kusto-------------------------------
### Multiple deployment supported: yes
### param PkustoClusterResourceId string
    1. Kusto cluster full resourceId
### param pRemoveKustoDataConnectionMapping array
    ``` bicep schema
    [
        {
            kustoDatabaseName:'dctest',
            kustoDataConnectionName:'dctestingestion',
            muiResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-Support-wus3-test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/support-mi-wus3-test'
        }
    ]
    ```
    1.kustoDatabaseName       : kusto db name from where data connection has to be removed
    2.kustoDataConnectionName : name of the endpoint(must be unique)
    3.muiResourceId           : data platform default mui resourceId, mui which has admin access to kusto cluster
### param PdeployRemoveKustoDataConnection string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 11.--------------------Aspect role Assignment (Not working)-------------------------------
### Multiple deployment supported: yes
### param PaspectRoleAssignmentMappings array
    ``` bicep schema
    [
        {
            appRoleName:'',
            ourAppName:'',
            provideAccessAppName:''
        }
    ]
    ```
    1.appRoleName          : SDP App Role Name
    2.ourAppName           : SDP App Name
    3.provideAccessAppName : Other Team App Name (team whome to provide access to our aspect)
### param PdeployAspectRoleAssignment string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 12.--------------------Add Keyvault secret-------------------------------
### Multiple deployment supported: yes
### param PkeyvaultResourceIdsKeyValueMappings array
    ``` bicep schema
    [
        {
            keyVaultResourceId:'/subscriptions/5e662e65-98a5-4ab8-addb-a944db412187/resourceGroups/rg-nrtglobal-test-eastus/providers/Microsoft.KeyVault/vaults/kv-nrtglobal-test-eastus',
            secretName:'testing1',
            value:'testing1'
        }
    ]
    ```
    1.keyVaultResourceId   : Keyvault ResourceId
    2.secretName           : Keyvault secret name
    3.value                : Keyvault secret value
### param PdeployKeyvaultKeyValue string
    1. here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module


## 13.--------------------Add Keyvault Certificate-------------------------------
### Multiple deployment supported: No
    **Note :** if you need to deploy multiple certificate then you need to copies of CreateCertificates module in orchestrator file and create a dependency between there CreateCertificates module
    they shouldn't run in parallel to make sure they run one by one. 
    also you need to create new param for PcertificateName,PcnSubjectName for each new module and use these new param and assign value in respective bicepparam file
###  param assistedIdentityKeyVaultName string 
     Ev2 key vault name
###  param commandsToExecuteInShell array  
     ['/bin/bash','-c','pwsh KeyVaultPublicAccessChange.ps1']
###  param ev2KeyVaultResourceGroupName string 
     ev2 KeyVault ResourceGroup Name
###  param Pev2PackageName string
     'package.zip'
###  param PmuiEv2RgIdentityClientId string
     your mui which has contributor access to keyvault its enterprise App Id
###  param PmuiEv2RgResourceId string
     your mui which has contributor access to keyvault its resource id
###  param PShellMaxExecutionTime string
     'PT20M'
###  param publicNetworkAccessStateDisabled string
     'Disabled'
###  param publicNetworkAccessStateEnabled string
     'Enabled'
###  param PcertificateKeyVaultName string
     Keyvault name where certificate has to be created
###  param PcertificateKeyVaultResourceGroupName string
    Keyvault resource group name where certificate has to be created
###  param PmuiCertificateKeyvaultRgIdentityClientId string  
    your mui which has contributor access to keyvault its its enterprise App Id
###  param PmuiCertificateKeyvaultRgResourceId string  
     your mui which has contributor access to keyvault its resource id
###  param PcertKeyvaultAppId string
    your ev2 app registration AppId same that is used for synapse/adf deployment
###  param PcertkeyVaultPrivateIssuer string
   'OneCertV2-PrivateCA'
###  param PcertkeyvaultProviderName string
   'OneCertV2-PrivateCA'
###  param PcertKeyvaultSecureId string
   your ev2 keyvault certificate full url, same used by synapse/adf
###  param PcertificateName string
  your new certificate Name
###  param PcnSubjectName sting
  your new certificate subject/domain name that you have registered on onecert portal
### param PdeployKeyvaultCertificates string
   here the value can be 'true' or 'false', 'true' will deploy the module 'false' will skip the module






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

metadata PcreateCaiGlobalInfraInput='__CREATE_CAIGLOBAL_INFRA_INPUT__'
metadata PcreateCaiGlobalInfraInput_SCHEMA={
  PmuiName:'string'
  PlogAnalyticsWorkspaceName:'string'
  PappInsightsName:'string'
  PstorageSkuName:'string'
  PstorageKind:'string'
  PkeyVaultName:'string'
  PcosmosDbccountsName:'string'
  PcosmosdbDatabaseCollectionsThroughputMapping:'array'
  PcosmosdbDatabases:'array'
  PdeployCosmosDbAccount:'bool'
  PcosmosLocation:'string'
  PcosmosGeoRepLocations:'array'
  PCosmosBackupPolicyTier:'string'
  PvectorEmbeddingCollections:'array'
  PeventHubNamespaceName:'string'
  PeventHubNames:'array'
  PeventHubNamespaceSkuCapacity:'string'
  PeventhubNamespacePartitionCount:'string'
  PdefaultEventHubConsumerGroupNames:'array'
  PmessageRetentionInDays:'string'
  PsearchServiceMapping:'array'
  PsearchServiceRoleDefinationIds:'array'
  PdeploySearchService:'bool'
  PsearchservicereplicaCount:'string'
  PsearchservicepartitionCount:'string'
  PsearchserviceSku:'string'
  PdeploySecondaryRegionSearch:'string'
  PWafPolicyName:'string'
  PafdWafPolicySku:'string'
  PAFDName:'string'
  PoriginGroup:'array'
  PWafPolicyPatternMatch:'string'
  PfoundryAccountName:'string'
  PdeployFoundry:'string'
  PfoundryModelDeployments:'array'
  PfoundryCreateProject:'bool'
  PreportsStorageAccountName:'string'
  PdeployReportsStorage:'string'
  PreportsContainerNames:'array'
}

param PcreateCaiGlobalInfraInput object
module CreateCAIGlobalInfra 'Templates/CreateCAIGlobalInfraResourceDefination.bicep'={
  name:'CreateCAIGlobalInfra'
  params:{
    PcreateCaiGlobalInfraInput:PcreateCaiGlobalInfraInput
  }
  dependsOn:[StopDeployment]
} 


metadata PdeployEv2Keyvault='__DEPLOY_EV2_KEYVAULT__'
param PdeployEv2Keyvault string

metadata ev2KeyVaultLocation = '__EV2_KEYVAULT_LOCATION__'
param ev2KeyVaultLocation string

metadata CreateEv2AssistedKeyvault = 'CREATE_EV2_ASSISTED_KEYVAULT'

module CreateEv2AssistedKeyvault 'Templates/ScopedEv2KeyvaultResourceDefination.bicep'={
  name:'CreateEv2AssistedKeyvault'
  params:{
    PkeyVaultName:assistedIdentityKeyVaultName
    Plocation:ev2KeyVaultLocation
    PresourceGroup:ev2KeyVaultResourceGroupName
    Pdeploy:PdeployEv2Keyvault
  }
  dependsOn:[CreateCAIGlobalInfra]
}

metadata PresourceGroupRoleDefinationIds = '__RESOURCE_GROUP_ROLE_DEFINATION_IDS__'
param PresourceGroupRoleDefinationIds  array

module resourceGroupRbacAssign 'Templates/Core/ResourceGroupRbacAssignmentResourceDefinition.bicep'={
  name:'ResourceGroupRbacAssignmentResourceDefinition'
  params:{
    PresourceGroupRoleDefinationIds:PresourceGroupRoleDefinationIds
    PresourceManagedIdentityId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityPrincipleId
  }
  dependsOn:[CreateCAIGlobalInfra]

}




metadata PkeyvaultRoleDefinationIds ='__KEYVAULT_ROLE_DEFINATION_IDS__'
param PkeyvaultRoleDefinationIds array

module KeyvaultRbacAssign 'Templates/Core/KeyvaultRbacAssignmentResourceDefinition.bicep'={
  name:'KeyvaultRbacAssignmentResourceDefinition'
  params:{
    PkeyvaultAccountName:CreateCAIGlobalInfra.outputs.DefaultKeyValueName
    PkeyvaultRoleDefinationIds:PkeyvaultRoleDefinationIds
    PresourceManagedIdentityId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityPrincipleId
  }
  dependsOn:[CreateCAIGlobalInfra]
}

// module KeyvaultRbacAssignForNRTMui 'Templates/Core/KeyvaultRbacAssignmentResourceDefinition.bicep'={
//   name:'KeyvaultRbacAssignForNRTMui'
//   params:{
//     PkeyvaultAccountName:CreateCAIGlobalInfra.outputs.DefaultKeyValueName
//     PkeyvaultRoleDefinationIds:PkeyvaultRoleDefinationIds
//     PresourceManagedIdentityId:GetNrtMui.outputs.userManagedIdentityPrincipleId
//   }
//   dependsOn:[KeyvaultRbacAssign]
// }


metadata Pev2AssistedIdentityAppObjectId='__EV2_ASSISTED_IDENTITY_APP_OBJECT_ID__'
param Pev2AssistedIdentityAppObjectId string
module AssistantEv2KeyvaultRbacAssign 'Templates/Core/KeyvaultRbacAssignmentResourceDefinition.bicep'={
  name:'AssistantEv2KeyvaultRbacAssign'
  params:{
    PkeyvaultAccountName:CreateCAIGlobalInfra.outputs.DefaultKeyValueName
    PkeyvaultRoleDefinationIds:PkeyvaultRoleDefinationIds
    PresourceManagedIdentityId:Pev2AssistedIdentityAppObjectId
  }
  dependsOn:[KeyvaultRbacAssign]
}

param PgenevaMSAppServiceObjectId string
metadata PgenevaMSAppServiceObjectId='__GENEVA_MS_APP_SERVICE_OBJECT_ID__'

module GenevaMSAppServiceKeyvaultRbacAssign 'Templates/Core/KeyvaultRbacAssignmentResourceDefinition.bicep'={
  name:'GenevaMSAppServiceKeyvaultRbacAssign'
  params:{
    PkeyvaultAccountName:CreateCAIGlobalInfra.outputs.DefaultKeyValueName
    PkeyvaultRoleDefinationIds:PkeyvaultRoleDefinationIds
    PresourceManagedIdentityId:PgenevaMSAppServiceObjectId
  }
  dependsOn:[AssistantEv2KeyvaultRbacAssign]
}

var assistantKeyvaultResourceId = resourceId(ev2KeyVaultResourceGroupName, 'Microsoft.KeyVault/vaults', assistedIdentityKeyVaultName)
metadata PdeployAssistantKeyvaultRoleAssignment='__DEPLOY_ASSISTANT_KEYVAULT_ROLE_ASSIGNMENT__'
param PdeployAssistantKeyvaultRoleAssignment string

module AssistantEv2KeyvaultRbacAssignAppMui 'Templates/ScopedKeyvaultRbacAssignmentResourceDefinition.bicep'={
  name:'AssistantEv2KeyvaultRbacAssignAppMui'
  params:{
    PkeyvaultResourceId:assistantKeyvaultResourceId
    PkeyvaultRoleDefinationIds:PkeyvaultRoleDefinationIds
    PresourceManagedIdentityId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityPrincipleId
    PdeployKeyvaultRoleAssignment: PdeployAssistantKeyvaultRoleAssignment
  }
  dependsOn:[GenevaMSAppServiceKeyvaultRbacAssign]
}


metadata commandsToExecuteInShellSearchIndex = '__COMMANDS_TO_EXECUTE_IN_SHELL_SEARCHINDEX__'
param commandsToExecuteInShellSearchIndex array
metadata PcsvAzureSearchServiceNames = '__CSV_AZURE_SEARCH_SERVICE_NAMES__'
param PcsvAzureSearchServiceNames string
metadata PSearchIndexDefinitionRootPath = '__SNAPSHOT_INDEX_DEFINITION_PATH__'
param PSearchIndexDefinitionRootPath string
metadata PcsvSearchIndexes = '__CSV_SEARCH_INDEXES_NAMES__'
param PcsvSearchIndexes string
metadata PdeploySearchIndexes = '__DEPLOY_SEARCH_INDEXES__'
param PdeploySearchIndexes string


metadata CreateAzureSearchIndexDefinition = 'SHELL'
module CreateAzureSearchIndexes 'Templates/Core/CreateAzureSearchIndexDefinition.bicep'={
  name: 'CreateAzureSearchIndexDefinition'
  params: {
    packageName:Pev2PackageName
    commandsToExecuteInShell:commandsToExecuteInShellSearchIndex
    maxExecutionTime:PShellMaxExecutionTime
    IdentityClientId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityClientId
    muiResourceId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityResourceId
    searchServiceNames: PcsvAzureSearchServiceNames
    indexNames:PcsvSearchIndexes
    indexRootDefinitionPath:PSearchIndexDefinitionRootPath
    deploySearchIndex:PdeploySearchIndexes

  }
  dependsOn:[CreateCAIGlobalInfra]
}


metadata ev2KeyVaultResourceGroupName = '__EV2_KEYVAULT_RESOURCE_GROUP_NAME__'
param ev2KeyVaultResourceGroupName string
metadata Pev2PackageName = '__EV2_PACKAGE_NAME__'
param Pev2PackageName string
metadata commandsToExecuteInShell = '__COMMANDS_TO_EXECUTE_IN_SHELL__'
param commandsToExecuteInShell array
metadata PShellMaxExecutionTime = '__SHELL_MAX_EXECUTION_TIME__'
param PShellMaxExecutionTime string
metadata assistedIdentityKeyVaultName = '__ASSISTED_IDENTITY_KEYVAULT_NAME__'
param assistedIdentityKeyVaultName string
metadata publicNetworkAccessStateEnabled = '__PUBLIC_NETWORK_ACCESS_STATE_ENABLED__'
param publicNetworkAccessStateEnabled string
metadata publicNetworkAccessStateDisabled = '__PUBLIC_NETWORK_ACCESS_STATE_DISABLED__'
param publicNetworkAccessStateDisabled string
metadata PdeployKeyvaultCertificates = '__DEPLOY_CERTIFICATES__'
param PdeployKeyvaultCertificates string
metadata EnabledAssistedIdentityKeyVaultNetwork= 'SHELL'

module EnabledAssistedIdentityKeyVaultNetwork 'Templates/auto.KeyvaultPublicAccessChangeDefination.bicep'={
  name: 'EnabledAssistedIdentityKeyVaultNetwork'
  params: {
    resourceGroupName:ev2KeyVaultResourceGroupName
    packageName:Pev2PackageName
    commandsToExecuteInShell:commandsToExecuteInShell
    maxExecutionTime:PShellMaxExecutionTime
    identityClientId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityClientId
    muiResourceId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityResourceId
    keyvaultName:assistedIdentityKeyVaultName
    publicNetworkAccessState:publicNetworkAccessStateEnabled
    deployCertificates:PdeployKeyvaultCertificates
  }
dependsOn: [ CreateCAIGlobalInfra,AssistantEv2KeyvaultRbacAssignAppMui ]
}


metadata EnabledCertificateKeyVaultNetwork= 'SHELL'
module EnabledCertificateKeyVaultNetwork 'Templates/auto.KeyvaultPublicAccessChangeDefination.bicep'={
  name: 'EnabledCertificateKeyVaultNetwork'
  params: {
    resourceGroupName:resourceGroup().name
    packageName:Pev2PackageName
    commandsToExecuteInShell:commandsToExecuteInShell
    maxExecutionTime:PShellMaxExecutionTime
    identityClientId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityClientId
    muiResourceId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityResourceId
    keyvaultName:CreateCAIGlobalInfra.outputs.DefaultKeyValueName
    publicNetworkAccessState:publicNetworkAccessStateEnabled
    deployCertificates:PdeployKeyvaultCertificates
  }
dependsOn: [ CreateCAIGlobalInfra,AssistantEv2KeyvaultRbacAssignAppMui ]
}

metadata PcertificateName='__CERTIFICATE_NAME__'
param PcertificateName string
metadata PcnSubjectName='__CN_SUBJECT_NAME__'
param PcnSubjectName string
metadata PcertKeyvaultSecureId='__CERT_KEYVAULT_SECURE_ID__'
param PcertKeyvaultSecureId string
metadata PcertkeyVaultPrivateIssuer='__CERT_KEYVAULT_PRIVATE_ISSUER__'
param PcertkeyVaultPrivateIssuer string
metadata PcertkeyvaultProviderName='__CERT_KEYVAULT_PROVIDER_NAME__'
param PcertkeyvaultProviderName string

param Pev2ApplicationAppId string
metadata Pev2ApplicationAppId='__EV2_APPLICATION_APP_ID__'


metadata CreateGenevaCertificate= 'CREATE_CERTIFICATES'
module CreateGenevaCertificate 'Templates/CreateKeyvaultCertificateDefination.bicep'={
  name: 'CreateGenevaCertificate'
  params: {
    certificateName:PcertificateName
    cnSubjectName:PcnSubjectName
    keyVaultAppId:Pev2ApplicationAppId
    KeyvaultName:CreateCAIGlobalInfra.outputs.DefaultKeyValueName
    keyVaultPrivateIssuer:PcertkeyVaultPrivateIssuer
    keyvaultProvider:PcertkeyvaultProviderName
    keyvaultSecureId:PcertKeyvaultSecureId
    deployCertificates:PdeployKeyvaultCertificates
          
  }
dependsOn: [EnabledAssistedIdentityKeyVaultNetwork,EnabledCertificateKeyVaultNetwork]
}

metadata DisabledCertificateKeyVaultNetwork= 'SHELL'
module DisabledCertificateKeyVaultNetwork 'Templates/auto.KeyvaultPublicAccessChangeDefination.bicep'={
  name: 'DisabledCertificateKeyVaultNetwork'
  params: {
    resourceGroupName:resourceGroup().name
    packageName:Pev2PackageName
    commandsToExecuteInShell:commandsToExecuteInShell
    maxExecutionTime:PShellMaxExecutionTime
    identityClientId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityClientId
    muiResourceId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityResourceId
    keyvaultName:CreateCAIGlobalInfra.outputs.DefaultKeyValueName
    publicNetworkAccessState:publicNetworkAccessStateDisabled
    deployCertificates:PdeployKeyvaultCertificates
  }
dependsOn: [ CreateGenevaCertificate ]
}


metadata DisabledAssistedIdentityKeyVaultNetwork= 'SHELL'
module DisabledAssistedIdentityKeyVaultNetwork 'Templates/auto.KeyvaultPublicAccessChangeDefination.bicep'={
  name: 'DisabledAssistedIdentityKeyVaultNetwork'
  params: {
    resourceGroupName:ev2KeyVaultResourceGroupName
    packageName:Pev2PackageName
    commandsToExecuteInShell:commandsToExecuteInShell
    maxExecutionTime:PShellMaxExecutionTime
    identityClientId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityClientId
    muiResourceId:CreateCAIGlobalInfra.outputs.CreateAppMui_userManagedIdentityResourceId
    keyvaultName:assistedIdentityKeyVaultName
    publicNetworkAccessState:publicNetworkAccessStateDisabled
    deployCertificates:PdeployKeyvaultCertificates
  }
dependsOn: [ CreateGenevaCertificate ]
}




// ////az deployment group  create -g rg-caiglobal-test-canadacentral  -f caiglobal.orchestrator.infra.bicep -p caiglobal.orchestrator.infra.test.bicepparam

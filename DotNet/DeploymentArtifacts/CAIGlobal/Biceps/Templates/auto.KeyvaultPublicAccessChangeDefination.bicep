param identityClientId string
param resourceGroupName string
param muiResourceId string
param commandsToExecuteInShell array
param packageName string
param maxExecutionTime string
param publicNetworkAccessState string
param keyvaultName string
param deployCertificates string

output KeyvaultPublicAccessChanges string='deployCertificates: ${deployCertificates}, identityClientId: ${identityClientId}, resourceGroupName: ${resourceGroupName}, muiResourceId: ${muiResourceId}, commandsToExecuteInShell: ${commandsToExecuteInShell}, packageName: ${packageName}, maxExecutionTime: ${maxExecutionTime}, publicNetworkAccessState: ${publicNetworkAccessState}, keyvaultName: ${keyvaultName}, '

param IdentityClientId string
param muiResourceId string
param commandsToExecuteInShell array
param packageName string
param maxExecutionTime string
param searchServiceNames string
param indexNames string
param indexRootDefinitionPath string
param deploySearchIndex string


output SearchIndexCreation string='IdentityClientId: ${IdentityClientId}, muiResourceId: ${muiResourceId}, commandsToExecuteInShell: ${commandsToExecuteInShell}, packageName: ${packageName}, maxExecutionTime: ${maxExecutionTime}, searchServiceNames: ${searchServiceNames}, indexName: ${indexNames}, indexRootDefinitionPath: ${indexRootDefinitionPath}, deploySearchIndex: ${deploySearchIndex}'

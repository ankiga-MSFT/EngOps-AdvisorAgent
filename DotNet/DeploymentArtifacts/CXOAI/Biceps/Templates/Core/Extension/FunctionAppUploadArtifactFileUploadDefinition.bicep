param IdentityClientId string //clientid
param StorageName string //storage
param ContainerName string //container
param FunctionAppArtifactBlobFileName string //blob
param FunctionAppArtifactZipFileName string //zip
param muiResourceId string //resource
param commandsToExecuteInShell array
param packageName string
param maxExecutionTime string
param resourceGroupName string
param EnvironmentName string
param CurrentApplicationSubscription string
output FileUploadScriptDetails string='CurrentApplicationSubscription: ${CurrentApplicationSubscription}, EnvironmentName: ${EnvironmentName}, IdentityClientId: ${IdentityClientId},ResourceGroupName: ${resourceGroupName}, StorageName: ${StorageName}, ContainerName: ${ContainerName}, FunctionAppArtifactBlobFileName: ${FunctionAppArtifactBlobFileName}, muiResouceId: ${muiResourceId}, commandsToExecuteInShell ${commandsToExecuteInShell[0]}, packageName ${packageName}, FunctionAppArtifactZipFileName ${FunctionAppArtifactZipFileName}, maxExecutionTime: ${maxExecutionTime}'

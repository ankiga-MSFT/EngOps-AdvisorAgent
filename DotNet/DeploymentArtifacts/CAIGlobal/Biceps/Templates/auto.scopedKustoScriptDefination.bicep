param PKustoClusterResourceId string
param PkustoDatabaseNames array
param PdeployICMRatioData string
var clusterName= split(PKustoClusterResourceId,'/')[8]
var subscriptionId= split(PKustoClusterResourceId,'/')[2]
var resourceGroupName= split(PKustoClusterResourceId,'/')[4]


module ScopedKustoScriptDefination 'auto.KustoScriptDefination.bicep' = {
name: 'ScopedKustoScriptDefination'
scope: resourceGroup(subscriptionId,resourceGroupName)
params: {
PclusterName: clusterName
PdatabaseNames: PkustoDatabaseNames
PdeployICMRatioData: PdeployICMRatioData
}
}

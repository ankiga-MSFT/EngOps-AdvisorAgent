param PadfName string
param PkustoClusterPrivateEndpointDnsName string
param PtenantId string
param PkustoServicePrincipalId string
param PkustoDatabaseName string
param PkustoSecretName string
// Reference the existing Azure Data Factory
resource adf 'Microsoft.DataFactory/factories@2018-06-01' existing = {
  name: PadfName
}

// Create Linked Service for Kusto in ADF
resource linkedService 'Microsoft.DataFactory/factories/linkedservices@2018-06-01' = {
  name: 'AzureDataExplorerLinkedService'
  parent: adf
  properties: {
    type: 'AzureDataExplorer'
    typeProperties: {
      endpoint: 'https://${PkustoClusterPrivateEndpointDnsName}'
      database: PkustoDatabaseName
      servicePrincipalId: PkustoServicePrincipalId
      servicePrincipalKey: {
        type: 'AzureKeyVaultSecret'
        store: {
          referenceName: 'AzureKeyVaultLinkedService'
          type: 'LinkedServiceReference'
        }
        secretName: PkustoSecretName
      }
      tenant: PtenantId
    }
  }
}

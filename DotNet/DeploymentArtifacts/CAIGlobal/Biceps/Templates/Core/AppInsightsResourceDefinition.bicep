param PappInsightsName string
param PlogAnalyticsWorkspaceId string

resource symbolicname 'Microsoft.Insights/components@2020-02-02' = {
  name: PappInsightsName
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    DisableLocalAuth: false
    Flow_Type: 'Redfield'
    IngestionMode:'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    Request_Source: 'IbizaAIExtensionEnablementBlade'
    RetentionInDays: 90
    WorkspaceResourceId: PlogAnalyticsWorkspaceId
  }
}

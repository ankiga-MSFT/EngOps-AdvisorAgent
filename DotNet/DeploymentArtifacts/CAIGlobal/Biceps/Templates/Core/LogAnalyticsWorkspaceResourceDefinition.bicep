param PlogAnalyticsWorspaceName string 
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: PlogAnalyticsWorspaceName
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    features: {
      disableLocalAuth: true
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    retentionInDays: 30
    sku: {
      name: 'pergb2018'
    }
    workspaceCapping: {
      dailyQuotaGb: 500
    }
  }
}

output logAnalyticsWorkspaceId string = logAnalytics.id

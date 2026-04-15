param PappInsightName string
param PappInsightsResourceGroupName string
resource appInsightComponent 'Microsoft.Insights/components@2020-02-02' existing  = {
  name: PappInsightName
  scope: resourceGroup(PappInsightsResourceGroupName)
}
output appInsightComponentId string = appInsightComponent.id
output appInsightInstrumentationKey string = appInsightComponent.properties.InstrumentationKey
output appInsightConnectionString string = appInsightComponent.properties.ConnectionString

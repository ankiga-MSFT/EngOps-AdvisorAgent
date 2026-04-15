param PAFDResourceId string
param PoriginGroupName string
param PWafPolicyPatternMatch string
param PfunctionName string
param PoriginName string
param ProuteName string

var subscriptionId = split(PAFDResourceId, '/')[2]
var resourceGroupName = split(PAFDResourceId, '/')[4]
var resourceName = split(PAFDResourceId, '/')[8]

module ExternaldAFDProfileOriginRouteDefination 'Core/AFDProfileOriginRouteDefination.bicep'={
  name:'ExternaldAFDProfileOriginRouteDefination'
  scope:resourceGroup(subscriptionId, resourceGroupName)
  params:{
    PAFDName:resourceName
    PFunctionName:PfunctionName
    PWafPolicyPatternMatch:PWafPolicyPatternMatch
    PoriginGroupName:PoriginGroupName
    PoriginName:PoriginName
    ProuteName:ProuteName
  }
}

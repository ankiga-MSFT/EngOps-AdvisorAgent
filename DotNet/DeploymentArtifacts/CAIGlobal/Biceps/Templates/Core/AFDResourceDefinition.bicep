param PAFDName string 
param PFrontDoorExternalId string 
param PWafPolicyName string
param PafdWafPolicySku  string
param PoriginGroup array
param PWafPolicyPatternMatch string


resource AzureFrontDoor 'Microsoft.Cdn/profiles@2024-09-01' = {
  name: PAFDName
  location: 'Global'
  sku: {
    name: PafdWafPolicySku
  }
  properties: {
    originResponseTimeoutSeconds: 60
  }
}

resource afd_profile_name 'Microsoft.Cdn/profiles/afdendpoints@2024-09-01' = {
  parent: AzureFrontDoor
  name: PAFDName
  location: 'Global'
  properties: {
    enabledState: 'Enabled'
  }
}

resource afd_origin_group 'Microsoft.Cdn/profiles/origingroups@2024-09-01' = [for i in range(0,length(PoriginGroup)):{
  parent: AzureFrontDoor
  name: PoriginGroup[i].name
  properties: {
       sessionAffinityState: PoriginGroup[i].sessionAffinityState
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/'
      probeRequestType: 'HEAD'
      probeProtocol: 'Http'
      probeIntervalInSeconds: 100
    }
 
  }
}
]


resource afd_waf_policies 'Microsoft.Cdn/profiles/securitypolicies@2024-09-01' = {
  parent: AzureFrontDoor
  name: PWafPolicyName
  properties: {
    parameters: {
      wafPolicy: {
        id: PFrontDoorExternalId
      }
      type: 'WebApplicationFirewall'
      associations: [
        {
          domains: [
            {
              id: afd_profile_name.id
            }
          ]
          patternsToMatch: [
            PWafPolicyPatternMatch
          ]
        }
      ]
    }
  }
}

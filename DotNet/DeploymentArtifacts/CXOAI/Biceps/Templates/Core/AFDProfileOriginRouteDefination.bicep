param PAFDName string 
param PoriginGroupName string
param PWafPolicyPatternMatch string
param PFunctionName string 
param PoriginName string
param ProuteName string
resource AzureFrontDoor 'Microsoft.Cdn/profiles@2024-09-01' existing = {
  name: PAFDName
  
  
}

resource afd_profile_name 'Microsoft.Cdn/profiles/afdendpoints@2024-09-01' existing = {
  parent: AzureFrontDoor
  name: PAFDName
  
}

resource afd_origin_group 'Microsoft.Cdn/profiles/origingroups@2024-09-01' existing = {
  parent: AzureFrontDoor
  name: PoriginGroupName
  
  }



  resource afd_origin 'Microsoft.Cdn/profiles/origingroups/origins@2024-09-01' = {
    parent: afd_origin_group
    name: PoriginName
    properties: {
      hostName: '${PFunctionName}.azurewebsites.net'
      httpPort: 80
      httpsPort: 443
      originHostHeader: '${PFunctionName}.azurewebsites.net'
      priority: 1
      weight: 1000
      enabledState: 'Enabled'
      enforceCertificateNameCheck: true
    }
    dependsOn: [
      AzureFrontDoor
    ]
  }
  resource afd_route 'Microsoft.Cdn/profiles/afdendpoints/routes@2024-09-01' = {
    parent: afd_profile_name
    name: ProuteName
    properties: {
      customDomains: []
      originGroup: {
        id: afd_origin_group.id
      }
      ruleSets: []
      supportedProtocols: [
        'Http'
        'Https'
      ]
      patternsToMatch: [
        PWafPolicyPatternMatch
      ]
      forwardingProtocol: 'MatchRequest'
      linkToDefaultDomain: 'Enabled'
      httpsRedirect: 'Enabled'
      enabledState: 'Enabled'
    }
    dependsOn: [
      afd_profile_name
      afd_origin_group
      afd_origin
    ]
  }

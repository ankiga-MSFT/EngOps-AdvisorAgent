param redisCacheName string
param location string
param skuName string
param capacity int
param maxMemoryReserved int
param maxFragmentationMemoryReserved int
param maxMemoryDelta int
param msiName string
param msiPrincipalId string
param redisPublicEndpointAccess string //'Disabled'
param redisshardCount int

resource Redis 'Microsoft.Cache/Redis@2024-03-01' =  {
  name: redisCacheName
  location: location
  properties: {
    redisVersion: '6.0'
    sku: {
      name: skuName
      family: 'P'
      capacity: capacity
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: redisPublicEndpointAccess
    shardCount:redisshardCount
    redisConfiguration: {
      'maxmemory-reserved': '${maxMemoryReserved}'
      'maxfragmentationmemory-reserved': '${maxFragmentationMemoryReserved}'
      'maxmemory-delta': '${maxMemoryDelta}'
      'aad-enabled': 'True'
    }
    disableAccessKeyAuthentication: true 
  }

  //zones: [ '1','2','3']
}

resource resource_name_msiPrincipalId 'Microsoft.Cache/Redis/accessPolicyAssignments@2023-08-01' = {
  parent: Redis
  name: msiPrincipalId
  properties: {
    accessPolicyName: 'Data Owner'
    objectId: msiPrincipalId
    objectIdAlias: msiName
  }
}

//asign redis contributor access to msi
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(msiPrincipalId, 'Redis Cache Contributor',redisCacheName)
  scope: Redis
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'e0f68234-74aa-48ed-b826-c38b57376e17') 
    principalId: msiPrincipalId
    principalType: 'ServicePrincipal'
  }
}


output redisCacheId string = Redis.id
output redisCacheHostName string = Redis.properties.hostName

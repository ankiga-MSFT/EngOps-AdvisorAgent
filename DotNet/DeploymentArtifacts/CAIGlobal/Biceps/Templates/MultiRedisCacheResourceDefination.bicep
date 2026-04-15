param PredisLocations array //= ['East US','West US','North Europe','Southeast Asia']
param PredisCacheNames array 
param PskuName string //= 'Premium'
param Pcapacity string //= 6
param PmuiName string
param PmsiPrincipalId string
param PmuiResourceId string
param PredisPublicEndpointAccess string
// param PsubnetName string
param PredisCacheSizeInGB string
param PredisshardCount string
var redisshardCount = int(PredisshardCount)
var redisCacheSize=int(PredisCacheSizeInGB)
var capacity = int(Pcapacity)
var maxMemoryReserved = (redisCacheSize * 1024 * 15)/100
var maxFragmentationMemoryReserved = (redisCacheSize * 1024 * 8)/100
var maxMemoryDelta = (redisCacheSize * 1024 * 8)/100
param PdeployRedis string
var deployRedis = bool(PdeployRedis)

module redisCaches 'Core/RedisCacheResourceDefination.bicep' = [for (location, i) in PredisLocations: if(deployRedis) {
  name: PredisCacheNames[i]
  params: {
    redisCacheName: PredisCacheNames[i]
    location: PredisLocations[i]
    skuName: PskuName
    capacity: capacity
    maxMemoryReserved: maxMemoryReserved
    maxFragmentationMemoryReserved: maxFragmentationMemoryReserved
    maxMemoryDelta: maxMemoryDelta
    msiName:PmuiName
    msiPrincipalId:PmsiPrincipalId
    redisPublicEndpointAccess:PredisPublicEndpointAccess
    redisshardCount:redisshardCount
  }
}]

module linkAllRedisCaches 'Core/RedisLinkedResourceDefination.bicep' = if(deployRedis) {
  name: 'RedisLinkedResourceDefination'
  params: {
    PmuiResourceId:PmuiResourceId
    PredisCacheNames:PredisCacheNames
  }
  dependsOn:redisCaches
}


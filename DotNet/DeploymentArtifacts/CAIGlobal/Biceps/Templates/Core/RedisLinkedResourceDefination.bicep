param PredisCacheNames array
param PmuiResourceId string

var formattedArray = replace(replace(replace(string(json(string(PredisCacheNames))), '[', '@('), ']', ')'), '"', '\'')
//var importmodule =' Import-Module Az.RedisCache  \n Import-Module Az.ManagedServiceIdentity \n'
var scriptVariables =' $redisCacheNames = ${formattedArray} \n  '
var mainScript = ''' 
try {
      if ($RedisCacheNames.Length -gt 1) {
        $PrimaryRedisCache = $RedisCacheNames[0]
        $SecondaryRedisCaches = $RedisCacheNames[1..($RedisCacheNames.Length - 1)]
        
        # Link secondary Redis caches to primary
        foreach ($SecondaryRedisCache in $SecondaryRedisCaches) {
            Write-Output "Linking $SecondaryRedisCache to $PrimaryRedisCache"
            New-AzRedisCacheLink  -PrimaryServerName $PrimaryRedisCache -SecondaryServerName $SecondaryRedisCache 
        }
      }
      else {
        Write-Output "Only one Redis cache in the array. Skipping linking."
      }
} 
catch {
Write-Error "An error occurred: $_"
throw $_
}
'''
var script='${scriptVariables}${mainScript}'
//var script='${importmodule}${scriptVariables}${mainScript}'
var scriptId = 'LinkRedisClusters'

module LinkAllRedisCluster 'DeployScripts.Template.bicep'={
  name:scriptId
 params: {
  Pps_command:script
   PmuiResourceId:PmuiResourceId
   PScriptId:scriptId
}
}

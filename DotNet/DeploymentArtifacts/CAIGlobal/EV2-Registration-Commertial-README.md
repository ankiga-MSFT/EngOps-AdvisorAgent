#-------------------------Pre Requisite-------------------------------------------------------------------------------------
1) download this repo as zip and extract the zip to your desired location https://msazure.visualstudio.com/Azure-Express/_git/Quickstart?version=GBmaster
2) open powershell 5.1 and change directory to the unzipped location
3) run .\AzureServiceDeployClient.ps1 file.
4) now you can execute below commands in the same terminal. Every time you open new powershell terminal you will have to execute .\AzureServiceDeployClient.ps1 before executing the below commands


##-----------------Start test your rollout(upload and run deploy)------------------------------------------------------

$ServiceId = "61b4b63c-3588-445e-a558-5352b1bd06c2"
$ServiceGroupName = "Microsoft.Azure.EngOps.CAIGlobal.Test-sg"
$StageMapPath="D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\TargetedAppEv2\HotpathStageMap.json"
$StageMapName = "Microsoft.Azure.CXPSDP.HotPath"
$StageMapVersion = "1.0.0.0"
$SubscriptionIdForBackfill = "5e662e65-98a5-4ab8-addb-a944db412187"
$ServiceArtifactsRoot = "D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal"
$RolloutSpec = "Public-Test/RolloutSpec.infra.json"

$ArtifactsVersion = Get-Content "D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal\version.txt" -First 1

Register-AzureServiceArtifacts -ServiceGroupRoot $ServiceArtifactsRoot -RolloutSpec $RolloutSpec -RolloutInfra Test -Force

Register-AzureServiceSubscription -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -SubscriptionKey "SDPCAIGlobalTestSubscriptionKey" -SubscriptionId $SubscriptionIdForBackfill -RolloutInfra Test 

New-AzureServiceRollout -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -StageMapName $StageMapName -StageMapVersion $StageMapVersion -Select "regions(canadacentral)" -ArtifactsVersion $ArtifactsVersion -RolloutInfra Test

New-AzureServiceStageMap -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -StageMapFilePath $StageMapPath -RolloutInfra Test

##-----------------Start ppe your rollout(upload and run deploy)------------------------------------------------------

$ServiceId = "61b4b63c-3588-445e-a558-5352b1bd06c2"
$ServiceGroupName = "Microsoft.Azure.EngOps.CAIGlobal.Ppe-sg"
$StageMapPath="D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\TargetedAppEv2\HotpathStageMap.json"
$StageMapName = "Microsoft.Azure.CXPSDP.HotPath"
$StageMapVersion = "1.0.0.0"
$SubscriptionIdForBackfill = "5e662e65-98a5-4ab8-addb-a944db412187"
$ServiceArtifactsRoot = "D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal"
$RolloutSpec = "Public-Ppe/RolloutSpec.infra.json"

$ArtifactsVersion = Get-Content "D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal\version.txt" -First 1

Register-AzureServiceArtifacts -ServiceGroupRoot $ServiceArtifactsRoot -RolloutSpec $RolloutSpec -RolloutInfra Test -Force

Register-AzureServiceSubscription -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -SubscriptionKey "SDPCAIGlobalPPESubscriptionKey" -SubscriptionId $SubscriptionIdForBackfill -RolloutInfra Test 

New-AzureServiceRollout -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -StageMapName $StageMapName -StageMapVersion $StageMapVersion -Select "regions(canadacentral)" -ArtifactsVersion $ArtifactsVersion -RolloutInfra Test

New-AzureServiceStageMap -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -StageMapFilePath $StageMapPath -RolloutInfra Test

##------------------------------Prvw subscription registration:------------------------------------------------------

$ServiceId = "61b4b63c-3588-445e-a558-5352b1bd06c2"
$ServiceGroupName = "Microsoft.Azure.EngOps.CAIGlobal.Prvw-sg"
$StageMapPath="D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\TargetedAppEv2\HotpathStageMap.json"
$StageMapName = "Microsoft.Azure.CXPSDP.HotPath"
$StageMapVersion = "1.0.0.0"

$ServiceArtifactsRoot = "D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal"
$RolloutSpec = "Public-Prvw/RolloutSpec.infra.json"

#Register-AzureServiceArtifacts -ServiceGroupRoot $ServiceArtifactsRoot -RolloutSpec $RolloutSpec -RolloutInfra Test  -Force

$SubscriptionIdForBackfill = "86612d19-f7d0-48b5-85d4-ae2f603a842e"

Register-AzureServiceSubscription -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -SubscriptionKey "SDPCAIGlobalPrvwSubscriptionKey" -SubscriptionId $SubscriptionIdForBackfill -RolloutInfra Test

New-AzureServiceStageMap -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -StageMapFilePath $StageMapPath -RolloutInfra Test

##------------------------------Prod subscription registration:------------------------------------------------------

$ServiceId = "61b4b63c-3588-445e-a558-5352b1bd06c2"
$ServiceGroupName = "Microsoft.Azure.EngOps.CAIGlobal.Prod-sg"
$StageMapPath="D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\TargetedAppEv2\HotpathStageMap.json"
$StageMapName = "Microsoft.Azure.CXPSDP.HotPath"
$StageMapVersion = "1.0.0.0"

$ServiceArtifactsRoot = "D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal"
$RolloutSpec = "Public-Prod/RolloutSpec.infra.json"

Register-AzureServiceArtifacts -ServiceGroupRoot $ServiceArtifactsRoot -RolloutSpec $RolloutSpec -RolloutInfra Prod  -Force

$SubscriptionIdForBackfill = "443efde9-a0c0-4d0e-8f52-63cdcd9e0931"

Register-AzureServiceSubscription -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -SubscriptionKey "SDPCAIGlobalProdSubscriptionKey" -SubscriptionId $SubscriptionIdForBackfill -RolloutInfra Prod

New-AzureServiceStageMap -ServiceIdentifier $ServiceId -ServiceGroup $ServiceGroupName -StageMapFilePath $StageMapPath -RolloutInfra Prod
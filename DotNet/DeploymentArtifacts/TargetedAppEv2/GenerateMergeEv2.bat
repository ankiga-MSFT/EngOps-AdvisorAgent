@echo off
setlocal
REM Define variables
set SERVICE_GROUP_FORMAT=Microsoft.Azure.EngOps.TargetedAppEv2.{0}-sg
set ENVIROMENTS=Test,Ppe,Prvw,Prod
set BICEPDIRECTORY=D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\TargetedAppEv2\Biceps
set SERVICETREEID=61b4b63c-3588-445e-a558-5352b1bd06c2
set APP_NAME=targetedappev2
set SERVICEGROUP_DEFINATION_NAME_FORMAT=engOps-eng-TargetedAppEv2-public-{0}-ev2-srg
set SUBSCRIPTION_KEY_FORMAT=SDPTargetedAppEv2{0}SubscriptionKey
set GENERATE_EV2_FOR_ORCHESTRATOR=app
set TEAMEMAIL=madhulikafte@microsoft.com
set TESTOWNERGROUPOBJECTID=da3e29ee-69a0-4d37-8405-a90c33c4e392
set PRODOWNERGROUPOBJECTID=60012848-5ae7-4d4d-931a-191222984335
set MOVETOAPPDIRECTORY=true
@REM set MERGEEV2TEMPLATENAME=MergeEv2Metadata
set OVERRIDESERVICEMODEENVIROMENTTAGS="[{\"Settings\":[{\"FieldName\":\"environment\",\"Value\":\"PPE\"}],\"AppType\":\"app\",\"Environment\":\"Prvw\"}]"
set APPROLLOUTCONFIG="{\"Default\":\"Default,func\",\"ADF\":\"ADF,adf\",\"CSIngestor\":\"CSIngestor,func\",\"CAIGlobal\":\"CAIGlobal,infra\",\"ICMIngest\":\"ICMIngest,func\",\"ICMKusto\":\"CAIGlobal,kusto\",\"ICMUtility\":\"ICMUtility,func\",\"Ingestor\":\"Ingestor,func\",\"Ingestorusres\":\"Ingestor,func\",\"NRTGlobal\":\"NRTGlobal,infra\",\"NRTGlobalusres\":\"NRTGlobal,infra\",\"NRTKusto\":\"NRTGlobal,kusto\",\"NRTUtility\":\"NRTUtility,func\",\"PiiScrubber\":\"PiiScrubber,func\",\"CXOAI\":\"CXOAI,func\",\"SupportUtility\":\"SupportUtility,infra\",\"Transformer\":\"Transformer,func\",\"Transformerusres\":\"Transformer,func\"}"
MergeEv2 --sgf %SERVICE_GROUP_FORMAT% --envs %ENVIROMENTS% --d %BICEPDIRECTORY% --stid %SERVICETREEID% --appname %APP_NAME% --rgdnf %SERVICEGROUP_DEFINATION_NAME_FORMAT% ^
 --skeyf %SUBSCRIPTION_KEY_FORMAT% --email %TEAMEMAIL% --togid %TESTOWNERGROUPOBJECTID% --pogid %PRODOWNERGROUPOBJECTID% --oetsm %OVERRIDESERVICEMODEENVIROMENTTAGS% --gev2o %GENERATE_EV2_FOR_ORCHESTRATOR% ^
 --arm %APPROLLOUTCONFIG% --move2appdir %MOVETOAPPDIRECTORY% 
pause
endlocal

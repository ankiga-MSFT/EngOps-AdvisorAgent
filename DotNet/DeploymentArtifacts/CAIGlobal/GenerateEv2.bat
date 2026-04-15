@echo off
setlocal
REM Define variables
set SERVICE_GROUP_FORMAT=Microsoft.Azure.EngOps.CAIGlobal.{0}-sg
set ENVIROMENTS=Test,Ppe,Prvw,Prod
set BICEPDIRECTORY=D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal\Biceps
set SERVICETREEID=61b4b63c-3588-445e-a558-5352b1bd06c2
set APP_NAME=caiglobal
set SERVICEGROUP_DEFINATION_NAME_FORMAT=engOps-eng-CAIGlobal-public-{0}-ev2-srg
set SUBSCRIPTION_KEY_FORMAT=CXOAI{0}SubscriptionKey
set TEAMEMAIL=madhulikafte@microsoft.com
set MOVETOAPPDIRECTORY=true
set TESTOWNERGROUPOBJECTID=da3e29ee-69a0-4d37-8405-a90c33c4e392
set PRODOWNERGROUPOBJECTID=60012848-5ae7-4d4d-931a-191222984335
set OVERRIDESERVICEMODEENVIROMENTTAGS="[{\"Settings\":[{\"FieldName\":\"environment\",\"Value\":\"PPE\"},{\"FieldName\":\"serviceGroupDefinationFormat\",\"Value\":\"Microsoft.Azure.EngOps.CAIGlobalKusto.{0}-sg\"}],\"AppType\":\"kusto\",\"Environment\":\"Prvw\"},{\"Settings\":[{\"FieldName\":\"serviceGroupDefinationFormat\",\"Value\":\"Microsoft.Azure.EngOps.CAIGlobalKusto.{0}-sg\"}],\"AppType\":\"kusto\",\"Environment\":\"Test\"},{\"Settings\":[{\"FieldName\":\"serviceGroupDefinationFormat\",\"Value\":\"Microsoft.Azure.EngOps.CAIGlobalKusto.{0}-sg\"}],\"AppType\":\"kusto\",\"Environment\":\"Ppe\"},{\"Settings\":[{\"FieldName\":\"serviceGroupDefinationFormat\",\"Value\":\"Microsoft.Azure.EngOps.CAIGlobalKusto.{0}-sg\"}],\"AppType\":\"kusto\",\"Environment\":\"Prod\"}]"
BicepToEv2 --sgf %SERVICE_GROUP_FORMAT% --envs %ENVIROMENTS% --d %BICEPDIRECTORY% --stid %SERVICETREEID% --appname %APP_NAME% --rgdnf %SERVICEGROUP_DEFINATION_NAME_FORMAT% ^
 --skeyf %SUBSCRIPTION_KEY_FORMAT% --email %TEAMEMAIL% --togid %TESTOWNERGROUPOBJECTID% --pogid %PRODOWNERGROUPOBJECTID% --oetsm %OVERRIDESERVICEMODEENVIROMENTTAGS% --move2appdir %MOVETOAPPDIRECTORY%
pause
endlocal

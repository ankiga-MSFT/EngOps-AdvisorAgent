------------------Prerequiste one time activity---------------------------
install azure cli
install visual studio code
install bicep extension in vscode
install ARMTool extension in vscode
bicepToEv2 tool path: https://microsoftapc-my.sharepoint.com/:f:/g/personal/sgandham_microsoft_com/EhUp7atm_Y5GlAba0ehPDrEBc2wycxxzc_ubxUlxIdXBoQ?e=XyFHRB 
Copy the above folder in D:\utils and rename the folder as BicepToEv2,make sure all files are in top level of this folder.
add D:\utils\BicepToEv2 to user enviroment variable path
copy relavent enviroment bat section  and paste it in *.bat file and then execute it
-----------------------------------------------------------
supportNRT/DotNet/DeploymentArtifacts/CAIGlobal/BicepToEv2-README.md
------------------------TEST, PPE and PROD------------------------------------
@echo off
setlocal
REM Define variables
set SERVICE_GROUP_FORMAT=Microsoft.Azure.EngOps.CAIGlobal.{0}-sg
set ENVIROMENTS=Test,Ppe,Prvw,Prod,Testusres,Ppeusres,Prvwusres,Produsres
set BICEPDIRECTORY=D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal\Biceps
set SERVICETREEID=61b4b63c-3588-445e-a558-5352b1bd06c2
set APP_NAME=caiglobal
set SERVICEGROUP_DEFINATION_NAME_FORMAT=engOps-eng-CAIGlobal-public-{0}-ev2-srg
set SUBSCRIPTION_KEY_FORMAT=SDPCAIGlobal{0}SubscriptionKey

---------------Test bat file------------------------------------------
@echo off
setlocal
REM Define variables
set SERVICE_GROUP_FORMAT=Microsoft.Azure.EngOps.CAIGlobal.{0}-sg
set ENVIROMENTS=Test,Ppe,Prvw,Prod
set BICEPDIRECTORY=D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\SupportUtility\Biceps
set SERVICETREEID=61b4b63c-3588-445e-a558-5352b1bd06c2
set APP_NAME=caiglobal
set SERVICEGROUP_DEFINATION_NAME_FORMAT=engOps-eng-CAIGlobal-public-{0}-ev2-srg
set SUBSCRIPTION_KEY_FORMAT=CAIGlobal{0}SubscriptionKey
supportNRT/DotNet/DeploymentArtifacts/SupportUtility/BicepToEv2-README.md
set TEAMEMAIL=madhulikafte@microsoft.com
set TESTOWNERGROUPOBJECTID=da3e29ee-69a0-4d37-8405-a90c33c4e392
set PRODOWNERGROUPOBJECTID=60012848-5ae7-4d4d-931a-191222984335
BicepToEv2 --sgf %SERVICE_GROUP_FORMAT% --envs %ENVIROMENTS% --d %BICEPDIRECTORY% --stid %SERVICETREEID% --appname %APP_NAME% --rgdnf %SERVICEGROUP_DEFINATION_NAME_FORMAT% ^
 --skeyf %SUBSCRIPTION_KEY_FORMAT% --email %TEAMEMAIL% --togid %TESTOWNERGROUPOBJECTID% --pogid %PRODOWNERGROUPOBJECTID%
pause
endlocal

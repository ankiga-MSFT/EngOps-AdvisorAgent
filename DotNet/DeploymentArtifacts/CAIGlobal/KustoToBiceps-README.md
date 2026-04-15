------------------Prerequiste one time activity---------------------------
install azure cli
install visual studio code
install bicep extension in vscode
install ARMTool extension in vscode
KustoToBiceps tool path: https://microsoftapc-my.sharepoint.com/:f:/g/personal/sgandham_microsoft_com/ElWXZ1hJnWNLvbBqANe7-4sBB9_FAkZpfFGKSF7q0JUr4A?e=h2ShxC 
Copy the above folder in D:\utils and rename the folder as KustoToBiceps,make sure all files are in top level of this folder.
add D:\utils\KustoToBiceps to user enviroment variable path
copy relavent enviroment bat section  and paste it in *.bat file and then execute it
-----------------------------------------------------------
------------------------TEST, PPE PRVW and PROD------------------------------------
@echo off
setlocal
REM Define variables
set KQL_SCRIPT_PATH=D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal\Kusto
set ENVIROMENTS=Test,Ppe,Prvw,Prod
set BICEPDIRECTORY=D:\src\EngOps-CXObserve-AI\CXOAI\DotNet\DeploymentArtifacts\CAIGlobal\Biceps
set APP_NAME=caiglobal
set GENERATEORCHESTRATOR=FALSE
KustoToBiceps --kqlscriptpath %KQL_SCRIPT_PATH% --envs %ENVIROMENTS% --d %BICEPDIRECTORY%  --appname %APP_NAME%   --go %GENERATEORCHESTRATOR%
pause
endlocal

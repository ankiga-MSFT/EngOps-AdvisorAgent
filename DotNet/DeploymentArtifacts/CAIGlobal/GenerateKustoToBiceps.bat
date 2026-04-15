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
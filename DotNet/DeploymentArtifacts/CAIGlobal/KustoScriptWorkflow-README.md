# Kusto script Deployment Workflow Steps (make sure your repo is set up exactly at D:\src\AzureCXP-Support-DataDomain)
# if repo is not set  in above path, please do it, else below steps won't work
## Standards for Kusto Script
    1. make sure each script file is placed in it respective database folder
    2. in case you want a certain sequence of script to be followed while deployment, you can specify the file name or folder name with sequence number. tool sorts the files so it will automatically follow the sequence
    3. make sure you script doesn't use **.execute database script with <|.** kusto internally uses this command to deploy the script so it will fail in case the user script to has this command
    4. make sure you kusto script size is at max 20kb. the tool internally batches two file together to deploy. if the length of the script is beyond 20000 bicep will not accept it.
    5. The script can only run database-level management commands that start with the following verbs:
    .create
    .create-or-alter
    .create-merge
    .alter
    .alter-merge
    .add
    6. Bicep support max 260 characters for object param property value length.
    7. The maximum length of the scriptContent property in the Microsoft.Kusto/clusters/databases/scripts@2022-02-01 schema is 1,000,000 characters
## Automation
    1. follow the KustoToBiceps-ReadMe.md file to setup the tool
    2. **IMPORTANT** there are some db that is not present in all environment, example AcehubSupportData in prvw, when the deployment happens in prvw then it will fail
    3. **IMPORTANT** in order to avoid the failure there is a KustoDbMapping.json file in KustoToBicep folder, you need to specify on which db scripts are suppose to be deployed
    4. in case of a new db that doesn't exist, just add it to the KustoDbMapping.json and add its scripts. the tool will create the Db and deploy the script
    
## Ev2 Generation
    1) Run GenerateKustoToBiceps.bat (make sure you have set up KustoToBiceps if not follow KustoToBiceps-Readme.md)
    2) inside Kusto/Biceps for each .bicepparam file of each env, open and visual studio code and check if there is error
        a) if your script file exceed certain threshold for size the bicep file will throw error. you will have to fix you script file
        b) after fixing script file, re run GenerateKustoToBiceps.bat
    2) Run GenerateEv2.bat (make sure you have set up BicepToEv2 if not follow BicepToEv2-Readme.md)
    3) go to Kusto/Biceps/.out file , here you will find all ev2 file , cut all the folder
    4) go to Kusto folder delete existing EV2 folders and then paste the newly generated ev2 file
    5) NOTE Don't replace the existing EV2 folder you need to delete them first.
## Ev2 Test env Local Testing
    1) increment version in version.txt file
    2) follow Ev2-Registration-Readme.md

   

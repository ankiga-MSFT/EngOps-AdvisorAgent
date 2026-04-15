###################################STEPS####################################
#1) copy scripts to ev2\rawfolder\scripts
#2) zip the code to ev2\rawfolder\scripts\
#3) zip the ev2\rawfolder\scripts  to  ev2\rawfolder
############################################################################
#############################Local Debugging################################
#1) download the build artifact to local download folder
#2) traverse to  drop_build_main in powershell ISE
#3) comment param line
#4) uncomment $SourcesDirectory $arctifactFolderName lines
###########################################################################
 
param([string] $SourcesDirectory, [string] $ArtifactFolderName) #Comment from local debugging
#$SourcesDirectory= "C:\Users\sgandham\Downloads\drop_build_main\drop_build_main" #Uncomment for local debugging
#$SourcesDirectory= Get-Location #Uncomment for local debugging
#$ArtifactFolderName= "SDPNRTArtifact" #Uncomment for local debugging
$BlobArctifactZipFileName="package.zip"
$skipFoldersName=@("DeploymentArtifacts","WorkerExtensions","scripts","ev2")
$artifactFolder="$SourcesDirectory\$ArtifactFolderName"
$deploymentArtifactFolderPath="$artifactFolder\DeploymentArtifacts"
$destinationFolderEv2Path="$artifactFolder\ev2"
$AllApps = Get-ChildItem -Path $deploymentArtifactFolderPath -Directory | ForEach-Object { $_.Name }
function Get-GlobalPackages {
    param([string[]] $AllApps,[string] $deploymentArtifactFolderPath,[string] $artifactFolder,[string] $skipFoldersName)
    Write-Host "Starting Get-GlobalPackages"
    $globalpackagesApp=@{}
    foreach ($folder in $AllApps) {
          try
          {
            if ($skipFoldersName -contains $folder) {
                continue
            }
              $ConfigurationSpecificationPath = "$deploymentArtifactFolderPath\$folder\Public-Prod\ConfigurationSpecification.app.json"
              if (Test-Path -Path $ConfigurationSpecificationPath) {
                    $config = Get-Content $ConfigurationSpecificationPath -Raw | ConvertFrom-Json
                    $packagesApps = $config.Settings.config_packagesapps ?? ""
                    if( $packagesApps -ne "") {
                        $globalpackagesApp[$folder] = $packagesApps.Split(",") | ForEach-Object { $_.Trim() }
                    }
              } else {
                  Write-Host "ConfigurationSpecification.app.json not found for $folder"
              }
          }
          catch
         {
                Write-Host "Failed to create zip for $folderName"
                Write-Host "Error: $_"
            }
    }
    Write-Host "Completed Get-GlobalPackages"
    return $globalpackagesApp
}

function Create-AllEv2Folder{
    param([string[]] $AllApps,[string] $deploymentArtifactFolderPath,[string] $destinationFolderEv2Path,[string] $artifactFolder,[string] $skipFoldersName)
      Write-Host "Starting Coping EV2 files"
        foreach ($folder in $AllApps) {  
          try
          {
            if ($skipFoldersName -contains $folder) {
                continue
            }
              $sourceFolder = "$deploymentArtifactFolderPath\$folder"
              $destinationFolder = "$destinationFolderEv2Path\$folder"
              $scriptsfolder= "$artifactFolder\scripts"
              $configfolder= "$sourceFolder\Config"
              $configDestinationFolder= "$destinationFolder\scripts"
               if (Test-Path -Path $sourceFolder) {

                   if (Test-Path -Path $destinationFolder) {
                        try {
                            # Delete the folder and its contents recursively
                            Remove-Item -Path $destinationFolder -Recurse -Force

                            # Output the result
                            Write-Host "Deleted exisitng folder: $destinationFolder"
                        }
                        catch {
                            # Output the error message
                            Write-Host "Failed to delete existing folder: $destinationFolder"
                            Write-Host "Error: $_"
                        }
                    }
                Write-Host "Coping from $sourceFolder to $destinationFolder"
                Copy-Item -Path $sourceFolder -Destination $destinationFolder -Recurse -Force
                Write-Host "Copied $sourceFolder to $destinationFolder"
                Write-Host "Coping from $scriptsfolder to $destinationFolder"
                Copy-Item -Path $scriptsfolder -Destination $destinationFolder -Recurse -Force
                Write-Host "Copied $scriptsfolder to $destinationFolder"
                if (Test-Path -Path $configfolder) {
                    Write-Host "Coping from $configfolder to $configDestinationFolder"
                    Copy-Item -Path $configfolder -Destination $configDestinationFolder -Recurse -Force
                    Write-Host "Copied $configfolder to $configDestinationFolder"
                } 
               }
               else
               {
                Write-Host   "Not Found: $sourceFolder"
               }
          }
          catch 
          {
            Write-Host "Failed to copy $folder to $destinationFolder"
            Write-Host "Error: $_"
          }
       } 
       Write-Host "Completed Coping EV2 files"
    }



function Create-AllAppsZip{
    param([array] $AllApps,[string] $artifactFolder,[string] $destinationFolderEv2Path,[string] $BlobArctifactZipFileName, [string] $skipFoldersName )
    Write-Host "Starting Zipping files"

    foreach ($folderName in $AllApps) {
        try
        {
            if ($skipFoldersName -contains $folder) {
                continue
            }
            $folderPath = "$artifactFolder\$folderName"
            $archiveFile = "$destinationFolderEv2Path\$folderName\scripts\$folderName.zip"
            $finalSourceZipPath ="$destinationFolderEv2Path\$folderName\scripts"
            $finalarchiveFile="$destinationFolderEv2Path\$folderName\$BlobArctifactZipFileName"
            # if file already exist delete it
            if (Test-Path -Path $archiveFile) {
                try {
                    # Delete the folder and its contents recursively
                    Remove-Item -Path $archiveFile -Recurse -Force

                    # Output the result
                    Write-Host "Deleted existing file: $folderName.zip"
                }
                catch {
                # Output the error message
                Write-Host "Failed to delete existing file: $folderName.zip"
                Write-Host "Error: $_"
               }
            }
			# zip code to ev2 script folder
            if (Test-Path -Path $folderPath){
				Write-Host "Creating zip for folder: $folderName in $folderPath to $archiveFile"
				New-ZipFile -Path $folderPath -DestinationPath $archiveFile
				Write-Host "Created zip for folder"
            }
            else {
				Write-Host "Path not found: $folderPath"
            }
			# copy EnviromentSettings folder content to script
			$envSettingPath = "$folderPath\EnviromentSettings"
            if (Test-Path -Path $envSettingPath) {
                Write-Host "Coping EnviromentSettings folder files"
                # Get-ChildItem -Path $envSettingPath -File | ForEach-Object {
                #     Copy-Item -Path $_.FullName -Destination $finalSourceZipPath
                # }
				$finalSourceZipEnvironmentSettingPath="${finalSourceZipPath}\EnvironmentSettings-$folderName"
                # Ensure the destination folder exists
                if (-not (Test-Path $finalSourceZipEnvironmentSettingPath)) {
                    New-Item -Path $finalSourceZipEnvironmentSettingPath -ItemType Directory | Out-Null
                }
                Copy-Item -Path "$envSettingPath\*" -Destination $finalSourceZipEnvironmentSettingPath -Recurse -Force
                Write-Host "Copied EnviromentSettings folder files"
            } 
			else {
                Write-Host "EnviromentSettings files not found."
            }
			# create final package.zip file
			Write-Host "Creating BlobArtifact Zip file for folder: $folderName in $finalSourceZipPath to $finalarchiveFile"
            New-ZipFile -Path $finalSourceZipPath -DestinationPath $finalarchiveFile
            Write-Host "Created  BlobArtifact Zip file for folder: $folderName"
        }
        catch
        {
            Write-Host "Failed to create zip for $folderName"
            Write-Host "Error: $_"
        }
    }
    Write-Host "Completed Zipping files"
 }

function New-ZipFile {
        param (
            [string]$Path,
            [string]$DestinationPath
        )
        Compress-Archive -Path "$Path\*" -DestinationPath $DestinationPath -Force
    }

function Create-GlobalPackagesZip {
    param([array] $AllApps,[string] $artifactFolder,[string] $destinationFolderEv2Path,[string] $BlobArctifactZipFileName, [string] $skipFoldersName ,[hashtable] $globalpackagesApp)
    Write-Host "Starting Global app Zipping files"
    foreach ($key in $globalpackagesApp.Keys) {
        $finalarchiveFile="$destinationFolderEv2Path\$key\$BlobArctifactZipFileName"
        $finalDestinationZipPath ="$destinationFolderEv2Path\$key\scripts"
        foreach ($value in $globalpackagesApp[$key]) {
            $zipFile = "$destinationFolderEv2Path\$value\scripts\$value.zip"
            $environmentSettingFiles = "$destinationFolderEv2Path\$value\scripts\EnvironmentSettings-$value"
            $configFiles = "$destinationFolderEv2Path\$value\scripts\Config"
            if (Test-Path -Path $zipFile) {
               Copy-Item -Path $zipFile -Destination $finalDestinationZipPath -Force
            }
            if (Test-Path -Path $environmentSettingFiles) {
                $destinationFolder = Join-Path $finalDestinationZipPath (Split-Path $environmentSettingFiles -Leaf)
                # Remove existing folder if it exists
                if (Test-Path $destinationFolder) {
                    Remove-Item $destinationFolder -Recurse -Force
                }
                # Copy the entire source folder into destination
                Copy-Item -Path $environmentSettingFiles -Destination $destinationFolder -Recurse -Force
            }
            if (Test-Path -Path $configFiles) {
                $destinationFolder = Join-Path $finalDestinationZipPath (Split-Path $configFiles -Leaf)
                # Remove existing folder if it exists
                if (Test-Path $destinationFolder) {
                    Remove-Item $destinationFolder -Recurse -Force
                }
                # Copy the entire source folder into destination
                Copy-Item -Path $configFiles -Destination $destinationFolder -Recurse -Force
            }
        }
        if (Test-Path -Path $finalarchiveFile){
           Remove-Item -Path $finalarchiveFile -Force 
        }  
        Write-Host "Creating zip for folder: $folderName in $finalDestinationZipPath to $finalarchiveFile"
        New-ZipFile -Path $finalDestinationZipPath -DestinationPath $finalarchiveFile
        Write-Host "Created zip for folder"
    }
    Write-Host "Completed Global app Zipping files"
}


Write-Host "SourcesDirectory: $SourcesDirectory"
Write-Host "AllApps: $AllApps"
Write-Host "destinationFolderEv2Path: $destinationFolderEv2Path"
Write-Host "deploymentArtifactFolderPath: $deploymentArtifactFolderPath"
Write-Host "destinationFolderEv2Path: $destinationFolderEv2Path"
Write-Host "artifactFolder: $artifactFolder"

$globalpackagesApp = @{}
Write-Host "-----------------------------------Create EV2 Folder---------------------------------------------"
$globalpackagesApp = Get-GlobalPackages -AllApps $AllApps -deploymentArtifactFolderPath $deploymentArtifactFolderPath  -artifactFolder $artifactFolder -skipFoldersName $skipFoldersName
Write-Host "-----------------------------------Created EV2 Folder---------------------------------------------"


Write-Host "-----------------------------------Create EV2 Folder---------------------------------------------"
Create-AllEv2Folder -AllApps $AllApps -deploymentArtifactFolderPath $deploymentArtifactFolderPath -destinationFolderEv2Path $destinationFolderEv2Path -artifactFolder $artifactFolder -skipFoldersName $skipFoldersName
Write-Host "-----------------------------------Created EV2 Folder---------------------------------------------"

Write-Host "-----------------------------------Start Zipping Apps---------------------------------------------"
Create-AllAppsZip -AllApps $AllApps -artifactFolder $artifactFolder -destinationFolderEv2Path $destinationFolderEv2Path -BlobArctifactZipFileName $BlobArctifactZipFileName -skipFoldersName $skipFoldersName
Write-Host "-----------------------------------End Zipping Apps-----------------------------------------------"

Write-Host "-----------------------------------Start Zipping Apps---------------------------------------------"
Create-GlobalPackagesZip -AllApps $AllApps -artifactFolder $artifactFolder -destinationFolderEv2Path $destinationFolderEv2Path -BlobArctifactZipFileName $BlobArctifactZipFileName -skipFoldersName $skipFoldersName -globalpackagesApp $globalpackagesApp
Write-Host "-----------------------------------End Zipping Apps-----------------------------------------------"


Write-Host "----------------------COMPLETE EV2 FOLDER STRUCTURE CREATED---------------------------------------"





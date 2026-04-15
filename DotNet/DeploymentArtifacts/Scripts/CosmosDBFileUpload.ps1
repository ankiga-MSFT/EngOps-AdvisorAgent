# add necessary assembly
#
Import-Module Az
Add-Type -AssemblyName System.Web

Write-Host "Trying to connect to Azure Account"

Connect-AzAccount -Identity -AccountId $env:IdentityClientId -SubscriptionId $env:SubscriptionId
Write-Host "Connected to Azure Account"
Write-Host "AccountId:   $env:IdentityClientId"
Write-Host "SubscriptionId:   $env:SubscriptionId"

Write-Host "APIResourceId:   $env:APIResourceId"
$TokenUri = 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=' + $env:APIResourceId
Write-Host 'TokenUri' + $TokenUri

$response = Invoke-WebRequest -Uri $TokenUri -Headers @{Metadata="true"}
$content =$response.Content | ConvertFrom-Json
$access_token = $content.access_token

# Function to post at metadata api
Function Post-Metadata
{
[CmdletBinding()]
Param
(
    [Parameter(Mandatory=$true)][String]$endPoint,
    [Parameter(Mandatory=$true)][String]$JSON
)

    $Verb = "POST"
    $authHeader = "Bearer " + $access_token
    Write-Host 'Access token ' + $access_token
    $header = @{authorization=$authHeader;}
    $contentType= "application/json"

    #[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-RestMethod -Method $Verb -ContentType $contentType -Uri $endPoint -Headers $header -Body $JSON
    return $result.statuscode
}

function Invoke-CosmosDbUpload ($containerName, $folder, $partitionKeyCosmos) {

    try {
      # fill the target cosmos database endpoint uri, database id, collection id and masterkey

      $files = Get-ChildItem $folder

      foreach ($file in $files) {
        $configFileName = $folder + '\' + $file.Name
        Write-Host 'Uploading ' + $configFileName

        $configurationString = Get-Content -Raw -Path $configFileName

        $endPoint =  $env:EndPoint + '/api/dataseeding/' + $containerName + '/' + $partitionKeyCosmos;
        Post-Metadata -EndPoint $endPoint -JSON $configurationString -Verbose -Debug
      }
    }
    catch {
      Write-Warning $error[0]
      EXIT 1
    }
} 

$regionFolder = $env:MainFolderName

$rootFolderPath = Join-Path ".\" $regionFolder

$ExecutiveSummaryMetadataPath = Join-Path $rootFolderPath 'ExecutiveSummaryMetadata'

$ExecutiveSummaryPromptMetadataPath = Join-Path $rootFolderPath 'ExecutiveSummaryPromptMetadata'

# containers for scorecard summary
$ScorecardSummaryConfigPath       = Join-Path $rootFolderPath 'ScorecardSummaryConfig'
$ScorecardSummaryPromptConfigPath = Join-Path $rootFolderPath 'ScorecardSummaryPromptConfig'


Invoke-CosmosDbUpload -containerName $env:ExecutiveSummaryMetadataContainer -folder $ExecutiveSummaryMetadataPath -partitionKeyCosmos 'pageId'
Invoke-CosmosDbUpload -containerName $env:ExecutiveSummaryPromptMetadataContainer -folder $ExecutiveSummaryPromptMetadataPath -partitionKeyCosmos 'pageId'

Invoke-CosmosDbUpload -containerName $env:ScorecardSummaryConfigContainer -folder $ScorecardSummaryConfigPath -partitionKeyCosmos 'scorecardId'
Invoke-CosmosDbUpload -containerName $env:ScorecardSummaryPromptConfigContainer -folder $ScorecardSummaryPromptConfigPath -partitionKeyCosmos 'scorecardId'

# execute


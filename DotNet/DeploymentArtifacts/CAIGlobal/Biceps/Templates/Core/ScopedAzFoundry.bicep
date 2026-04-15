param PfoundryAccountName string
param Plocation string
param PmodelDeployments array = []
param PresourceGroup string
param Pdeploy string
param PcreateProject bool = false
param PprojectName string = '${PfoundryAccountName}-project'
param PpublicNetworkAccess string = 'Enabled'
var deploy = bool(Pdeploy)

module CreateAzFoundry 'AzFoundry.bicep' = if (deploy) {
  name: 'CreateAzFoundry'
  scope: resourceGroup(PresourceGroup)
  params: {
    Plocation: Plocation
    PfoundryAccountName: PfoundryAccountName
    PmodelDeployments: PmodelDeployments
    PcreateProject: PcreateProject
    PprojectName: PprojectName
    PpublicNetworkAccess: PpublicNetworkAccess
  }
}

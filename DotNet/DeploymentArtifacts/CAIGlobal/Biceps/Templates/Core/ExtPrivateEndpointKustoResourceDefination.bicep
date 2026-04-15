
param PvnetName string
param PkustoClusterName string
param Pkustolocation string
param PsubnetName string='storage'
param PkustoRGName string
param PvnetResourceGroupName string
param PVnetResourceId string
module CreatePrivateEndpointForKusto 'PrivateEndpointKustoResourceDefination.bicep'={
  name:'PrivateEndpointForKusto'
  scope:resourceGroup(PkustoRGName)
  params:{
     PvnetName:PvnetName
     PkustoClusterName:PkustoClusterName
     Plocation:Pkustolocation
     PsubnetName:PsubnetName
     PvnetResourceGroupName :PvnetResourceGroupName
     PvnetResourceId:PVnetResourceId
      
  }
}

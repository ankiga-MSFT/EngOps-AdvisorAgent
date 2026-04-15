param PkeyvaultResourceId string
param PdeployKeyvaultRoleAssignment string
param PkeyvaultRoleDefinationIds array
param PresourceManagedIdentityId string 
var deployKeyvaultRoleAssignment = bool(PdeployKeyvaultRoleAssignment)
var subscriptionId =  deployKeyvaultRoleAssignment ? split(PkeyvaultResourceId, '/')[2] :''
var resourceGroupName = deployKeyvaultRoleAssignment ? split(PkeyvaultResourceId, '/')[4] :''
var keyvaultName =  deployKeyvaultRoleAssignment ? split(PkeyvaultResourceId, '/')[8] : ''



module ScopedKeyVaultRbacAssignment 'Core/KeyvaultRbacAssignmentResourceDefinition.bicep'=  if (deployKeyvaultRoleAssignment) {
  name: 'ScopedKeyVaultRbacAssignment-${keyvaultName}'
  scope: resourceGroup(subscriptionId,resourceGroupName)
   params: {
     PkeyvaultAccountName: keyvaultName
    PkeyvaultRoleDefinationIds:PkeyvaultRoleDefinationIds
    PresourceManagedIdentityId:PresourceManagedIdentityId
}
}






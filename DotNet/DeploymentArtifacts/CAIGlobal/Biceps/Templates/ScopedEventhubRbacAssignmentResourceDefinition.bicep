param PresourceIdsPrincipleMappings array
param PdeployEventhubRoleAssignment string
var deployEventhubRoleAssignment = bool(PdeployEventhubRoleAssignment)
var jsonArray = PresourceIdsPrincipleMappings
var subscriptionIds = [for obj in jsonArray: deployEventhubRoleAssignment ? split(obj.eventhubResourceId, '/')[2] : '']
var resourceGroupNames = [for obj in jsonArray: deployEventhubRoleAssignment ? split(obj.eventhubResourceId, '/')[4] : '']
var eventhubNamespaces = [for obj in jsonArray: deployEventhubRoleAssignment ? split(obj.eventhubResourceId, '/')[8] : '']
var eventhubs = [for obj in jsonArray: deployEventhubRoleAssignment ? split(obj.eventhubResourceId, '/')[10] : '']



@batchSize(1)
module ScopedEventhubRbacAssignment 'Core/BasicEventhubRbacAssignmentResourceDefinition.bicep'= [for (obj,i) in jsonArray: if (deployEventhubRoleAssignment) {
  name: 'ScopedEventhubRbacAssignment-${i}'
  scope: resourceGroup(subscriptionIds[i],resourceGroupNames[i])
   params: {
    Peventhubnamespace: eventhubNamespaces[i]
    Peventhub: eventhubs[i]
    ProleDefinationId: obj.roleDefinationId
    principleid: obj.principleId
}
}]






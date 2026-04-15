param PsearchServiceMapping array
param PdeploySearchService string
param PresourceManagedIdentityId string
param PsearchServiceRoleDefinationIds array
param PdeploySecondaryRegionSearch string
var deploySearchService = bool(PdeploySearchService)
var deploySecondaryRegion = bool(PdeploySecondaryRegionSearch)
var effectiveMapping = deploySecondaryRegion ? PsearchServiceMapping : [PsearchServiceMapping[0]]
@batchSize(1)
 module MultiSearchService 'Core/SearchServiceRbacAssignmentResourceDefinition.bicep' =  [for (map, i) in effectiveMapping:  if(deploySearchService) {
   name: '${map.searchServiceName}-${i}'
   params: {
    PresourceManagedIdentityId:PresourceManagedIdentityId
    PsearchServiceAccountName: map.searchServiceName
    PsearchServiceRoleDefinationIds:PsearchServiceRoleDefinationIds
   }
 }
]


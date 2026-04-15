param PsearchServiceMapping array
param PsearchserviceSku string
param PsearchservicereplicaCount string
param PsearchservicepartitionCount string
param PdeploySearchService string
param PdeploySecondaryRegionSearch string
var deploySearchService = bool(PdeploySearchService)
var deploySecondaryRegion = bool(PdeploySecondaryRegionSearch)
var effectiveMapping = deploySecondaryRegion ? PsearchServiceMapping : [PsearchServiceMapping[0]]
@batchSize(1)
 module MultiSearchService 'Core/SearchServiceResourceDefinition.bicep' =  [for (map, i) in effectiveMapping:  if(deploySearchService) {
   name: '${map.searchServiceName}-${i}'
   params: {
     PsearchServiceName: map.searchServiceName
     Psku: PsearchserviceSku
     PreplicaCount: PsearchservicereplicaCount
     PpartitionCount: PsearchservicepartitionCount
     Plocation: map.location
   }
 }
]

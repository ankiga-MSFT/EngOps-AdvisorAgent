param PclusterName string
param PdatabaseNames array
param PdeployICMRatioData string
var deployICMRatioData = bool(PdeployICMRatioData)
param forceUpdateTag string = utcNow()
resource KustoCluster 'Microsoft.Kusto/Clusters@2024-04-13' existing = {
name: PclusterName
}

resource KustoDBCreations 'Microsoft.Kusto/Clusters/Databases@2024-04-13' =[for (dbname,i) in PdatabaseNames:{
  parent: KustoCluster
  name: dbname
  location: 'West US 3'
  kind: 'ReadWrite'
}
]


resource kustoPICMRatioData0 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = if(deployICMRatioData) {
    name: '${PclusterName}/ICMRatioData/PICMRatioData0'
    properties: {
        scriptContent: '''.create-merge table SRChange(SupportRequestNumber:string ,EventDateTime:datetime,  EventType:string, ChangeProperties:dynamic , Changes:dynamic, CritSitPrevious:string, CritSitCurrent:string, StatePrevious:string , StateCurrent:string, SeverityPrevious:string , SeverityCurrent:string, StatusPrevious:string , StatusCurrent:string, LinkedICMsPrevious:dynamic, LinkedICMsCurrent:dynamic, SREventActionTypePrevious:string, SREventActionTypeCurrent:string, ICMLinkChange:dynamic , Snapshot:dynamic , CritSit:bool, State:string, Severity:string, Status:string, Product:dynamic, LinkedICMs:dynamic, ModifiedOn:datetime, CreatedOn:datetime, ModifiedBy:string, CreatedBy:string, DataBoundary:string, AzureSubscriptionId:string, Customers:dynamic, SupportTopicTitle:string , SupportRegion:string , SREventActionType:string, CaseNumber:string, SdpInternal:dynamic, ResourceId:string,AgentId:string,Age:int,ResolvedDateTime:datetime, TenantId:string, ttl:long, IsM365:bool, RestrictedAccessProgramName:string,CaseUri:string,Content:dynamic )

.create-merge table SrSnapshot (CaseNumber:string, CritSit:bool, State:string, Severity:string, Status:string, Product:dynamic, LinkedICMs:dynamic,EventDateTime:datetime, ModifiedOn:datetime, CreatedOn:datetime, ModifiedBy:string, CreatedBy:string, DataBoundary:string, AzureSubscriptionId:string, Customers:dynamic, SupportTopicTitle:string, SupportRegion:string, SREventActionType:string,NonDFMLinkageActivities:dynamic, LinkedIncidents:dynamic, AdditionalIncidentIds:dynamic ,IncidentId:string, RatioIncidentIds:dynamic, ResourceId:string,AgentId:string,Age:int,ResolvedDateTime:datetime, TenantId:string,SdpCreatedOn:datetime, SdpModifiedOn:datetime, IsPurged:bool,Content:dynamic, RawProperties:dynamic, CosmosDbTs:long, SdpInternal:dynamic, ttl:long, IsM365:bool, RestrictedAccessProgramName:string, CaseUri:string,CompleteEvent:dynamic);

.create-merge table RatioLinkage(CaseNumber:string, Linkages:dynamic, EventType:string, EntityType:string , EntityAction:string , EntityEventDateTime:datetime , EntityID:string , CreatedBy:string , ModifiedBy:string , CreatedOn:datetime , ModifiedOn:datetime, EventDateTime:datetime, CosmosDbTs:long, Content:dynamic);

.alter table SRChange policy ingestionbatching ``` { "MaximumBatchingTimeSpan" : "00:00:10", "MaximumNumberOfItems" : 100, "MaximumRawDataSizeMB": 1024 } ```

.alter table SrSnapshot policy ingestionbatching ``` { "MaximumBatchingTimeSpan" : "00:00:10", "MaximumNumberOfItems" : 100, "MaximumRawDataSizeMB": 1024 } ```

.alter table RatioLinkage policy ingestionbatching ``` { "MaximumBatchingTimeSpan" : "00:00:10", "MaximumNumberOfItems" : 100, "MaximumRawDataSizeMB": 1024 } ```

.create-merge table ValidationRatioICMLinkages (CaseNumber:string, IncidentId:string, Action:string, Processtimestamp:datetime, IcMCloud:string, Reason:string, Source:string)

.create-merge table ValidationRatioICMLinkages_Staging (CaseNumber:string, IncidentId:string, Action:string, Processtimestamp:datetime, IcMCloud:string, Reason:string, Source:string)

.create-merge table ReconciliationData (CaseNumber:string, IncidentIds:string, IcMCloud:string, EventDateTime:datetime, Type:string, Content:dynamic, DatetimeInsertedForReconcilation:datetime)

.create-merge table ReconciliationDataHistory (CaseNumber:string, IncidentIds:string, IcMCloud:string, EventDateTime:datetime, Type:string, Content:dynamic, DatetimeInsertedForReconcilation:datetime)'''
        continueOnErrors: true
        forceUpdateTag: forceUpdateTag
    }
    dependsOn:[KustoDBCreations]
}



resource kustoPICMRatioData1 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = if(deployICMRatioData) {
    name: '${PclusterName}/ICMRatioData/PICMRatioData1'
    properties: {
        scriptContent: '''.create-merge table IcmL2Aggregate (IncidentIdCloud:string, IncidentId: string, DataBoundary: string, EventTime: datetime, EventType: string, SnapshotId: string, SupportRequestsCount: int, IsCritSitCount: int, 
    LinkedSupportRequests_Ids_PUBLIC: dynamic, LinkedSupportRequests_Ids_EU: dynamic, LinkedSupportRequests_LinkedIncidents: dynamic, FetchLink: string, SdpInternal: dynamic, Content: dynamic, AutoLinkedCount:int, AutoLinkedCritSitCount:int, EventCreatedTime: datetime, ManuallyLinkedCount: int, ManuallyLinkedCritSitCount: int, TotalCount : int);

.alter table IcmL2Aggregate policy ingestionbatching ``` { "MaximumBatchingTimeSpan" : "00:00:10", "MaximumNumberOfItems" : 100, "MaximumRawDataSizeMB": 1024 } ```

.create-merge table IncidentLinkage(CaseNumber:string, Content:dynamic, IncidentId:string, DataBoundary:string, LinkSource:string, LinkCreatedOn:string, AggregatedNonDfmLinks:dynamic, Aggregate_Add:dynamic, Aggregate_Remove:dynamic, EntityType:string , EntityAction:string , EventDateTime:datetime , EntityID:string , CreatedBy:string , ModifiedBy:string , CreatedOn:datetime , ModifiedOn:datetime,SdpInternal:dynamic, CosmosDbTs:long);

.alter table IncidentLinkage policy ingestionbatching ``` { "MaximumBatchingTimeSpan" : "00:00:10", "MaximumNumberOfItems" : 100, "MaximumRawDataSizeMB": 1024 } ```

.create-merge table  ICMLinkageRedisKeyStore  (CaseNumber:string, Prefix:string,ttl:long, TimeStamp:datetime) with (folder = "", docstring = "Redis key store")

.create-merge table PipelineTracker (['count']:int, index:int, PipelineName:string, ExecutionTime:datetime) with (folder = "ADF", docstring = "ICMLinkage pipeline tracker")

.create-merge table ChangeFeedPipelineSRs (CaseNumber:string, ModifiedOn:datetime, ExecutionTime:datetime) with (folder = "ADF", docstring = "ICMLinkage change feed pipeline helper tracker")

.create-merge table ChangeFeedPipelineIcmL2Aggregate (IncidentIdCloud:string, EventCreatedTime:datetime, ExecutionTime:datetime) with (folder = "ADF", docstring = "L2Aggregate change feed pipeline helper tracker")

.create-merge table ChangeFeedPipelineIncidentLinkage (CaseNumber:string, EventDateTime:datetime, ExecutionTime:datetime) with (folder = "ADF", docstring = "IncidentLinkage change feed pipeline helper tracker")

.create-merge table ChangeFeedPipelineRatioLinkage (CaseNumber:string, EventDateTime:datetime, ExecutionTime:datetime) with (folder = "ADF", docstring = "Ratio Linkage missing change feed pipeline helper tracker")'''
        continueOnErrors: true
        forceUpdateTag: forceUpdateTag
    }
    dependsOn:[kustoPICMRatioData0]
}



resource kustoPICMRatioData2 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = if(deployICMRatioData) {
    name: '${PclusterName}/ICMRatioData/PICMRatioData2'
    properties: {
        scriptContent: '''.create-merge table ICMLinkageRedisKeyStore (CaseNumber:string, Prefix:string, ttl:long, TimeStamp:datetime) with (folder = "ADF", docstring = "Redis key store")

.create-merge table ChangeFeedPipelineSRChange (CaseNumber:string, ModifiedOn:datetime, ExecutionTime:datetime) with (folder = "ADF", docstring = "ICMLinkage change feed pipeline helper for SRChange")

.create-merge table PurgedLinkageCorrectionPipelineSRs (CaseNumber:string, ModifiedOn:datetime, ExecutionTime:datetime) with (folder = "ADF", docstring = "Purged Linkage Correction PipelineSRs")

.create-or-alter table SRChange ingestion json mapping 'SRChange_mapping'
  ```
[
  {"column":"EventDateTime","path":"$.EventDateTime","datatype":"datetime"},
  {"column":"SupportRequestNumber","path":"$.SupportRequestNumber","datatype":"string"},
  {"column":"EventType","path":"$.EventType","datatype":"string"},
  {"column":"ChangeProperties","path":"$.ChangeProperties","datatype":"dynamic"},
  {"column":"Changes","path":"$.Changes","datatype":"dynamic"},
  {"column":"CritSitPrevious","path":"$.Changes.CritSit.PreviousValue","datatype":"string"},
  {"column":"CritSitCurrent","path":"$.Changes.CritSit.CurrentValue","datatype":"string"},
  {"column":"StatePrevious","path":"$.Changes.State.PreviousValue","datatype":"string"},
  {"column":"StateCurrent","path":"$.Changes.State.CurrentValue","datatype":"string"},
  {"column":"SeverityPrevious","path":"$.Changes.Severity.PreviousValue","datatype":"string"},
  {"column":"SeverityCurrent","path":"$.Changes.Severity.CurrentValue","datatype":"string"},
  {"column":"StatusPrevious","path":"$.Changes.Status.PreviousValue","datatype":"string"},
  {"column":"StatusCurrent","path":"$.Changes.Status.CurrentValue","datatype":"string"},
  {"column":"LinkedICMsPrevious","path":"$.Changes.LinkedICMs.PreviousValue","datatype":"dynamic"},
  {"column":"LinkedICMsCurrent","path":"$.Changes.LinkedICMs.CurrentValue","datatype":"dynamic"},
  {"column":"SREventActionTypePrevious","path":"$.Changes.SREventActionType.PreviousValue","datatype":"string"},
  {"column":"SREventActionTypeCurrent","path":"$.Changes.SREventActionType.CurrentValue","datatype":"string"},
  {"column":"ICMLinkChange","path":"$.Changes.LinkedICMs.ICMLinkChange","datatype":"dynamic"},
  {"column":"Snapshot","path":"$.Snapshot","datatype":"dynamic"},
  {"column":"CritSit","path":"$.Snapshot.CritSit","datatype":"bool"},
  {"column":"State","path":"$.Snapshot.State","datatype":"string"},
  {"column":"Severity","path":"$.Snapshot.Severity","datatype":"string"},
  {"column":"Status","path":"$.Snapshot.Status","datatype":"string"},
  {"column":"Product","path":"$.Snapshot.Product","datatype":"dynamic"},
  {"column":"LinkedICMs","path":"$.Snapshot.LinkedICMs","datatype":"dynamic"},
  {"column":"ModifiedOn","path":"$.Snapshot.ModifiedOn","datatype":"datetime"},
  {"column":"CreatedOn","path":"$.Snapshot.CreatedOn","datatype":"datetime"},
  {"column":"ModifiedBy","path":"$.Snapshot.ModifiedBy","datatype":"string"},
  {"column":"CreatedBy","path":"$.Snapshot.CreatedBy","datatype":"string"},
  {"column":"DataBoundary","path":"$.Snapshot.DataBoundary","datatype":"string"},
  {"column":"AzureSubscriptionId","path":"$.Snapshot.AzureSubscriptionId","datatype":"string"},
  {"column":"Customers","path":"$.Snapshot.Customers","datatype":"dynamic"},
  {"column":"SupportTopicTitle","path":"$.Snapshot.SupportTopicTitle","datatype":"string"},
  {"column":"SupportRegion","path":"$.Snapshot.SupportRegion","datatype":"string"},
  {"column":"SREventActionType","path":"$.Snapshot.SREventActionType","datatype":"string"},
  {"column":"CaseNumber","path":"$.Snapshot.CaseNumber","datatype":"string"},
  {"column":"SdpInternal","path":"$.SdpInternal","datatype":"dynamic"},
  {"column":"ResourceId","path":"$.Snapshot.ResourceId","datatype":"string"},
  {"column":"AgentId","path":"$.Snapshot.AgentId","datatype":"string"},
  {"column":"Age","path":"$.Snapshot.Age","datatype":"int"},
  {"column":"ResolvedDateTime","path":"$.Snapshot.ResolvedDateTime","datatype":"datetime"},
  {"column":"TenantId","path":"$.Snapshot.TenantId","datatype":"string"},
  {"column":"ttl","Properties":{"path":"$.Ttl"}},
  {"column":"IsM365","Properties":{"path":"$.Snapshot.IsM365"}},
  {"column":"RestrictedAccessProgramName","Properties":{"path":"$.Snapshot.RestrictedAccessProgramName"}},
  {"column":"CaseUri","path":"$.Snapshot.CaseUri","datatype":"string"},
  {"column":"Content","Properties":{"path":"$","datatype":"dynamic"}}
]
```

.create-or-alter table SrSnapshot ingestion json mapping "SrSnapshot_mapping"
  ```
  [
	  {"column":"CaseNumber","Properties":{"path":"$.Content.CaseNumber"}},
	  {"column":"SREventActionType","Properties":{"path":"$.RawProperties.UserProperties.EventType"}},  
    {"column":"CritSit","Properties":{"path":"$.Content.IsCritSit"}},
    {"column":"State","Properties":{"path":"$.Content.State"}},
    {"column":"Severity","Properties":{"path":"$.Content.Severity"}},
    {"column":"Status","Properties":{"path":"$.Content.StateAnnotation"}},
    {"column":"Product","Properties":{"path":"$.Content.Product"}},
    {"column":"LinkedICMs","Properties":{"path":"$.Content.LinkedIncidentIds"}},
    {"column":"ModifiedOn","Properties":{"path":"$.Content.UpdatedOn"}},
    {"column":"CreatedOn","Properties":{"path":"$.Content.CreatedOn"}},
    {"column":"ModifiedBy","Properties":{"path":"$.modifiedBy"}},
    {"column":"CreatedBy","Properties":{"path":"$.createdBy"}},
    {"column":"DataBoundary","Properties":{"path":"$.RawProperties.UserProperties.DataBoundary"}},
    {"column":"AzureSubscriptionId","Properties":{"path":"$.Content.AzureSubscriptionId"}},
    {"column":"Customers","Properties":{"path":"$.Content.Customers"}},
    {"column":"SupportTopicTitle","Properties":{"path":"$.Content.IssueContext.OriginalSupportTopicName"}},
    {"column":"SupportRegion","Properties":{"path":"$.Content.IssueContextLocation"}},
    {"column":"NonDFMLinkageActivities","Properties":{"path":"$.Content.NonDFMLinkageActivities"}},
    {"column":"LinkedIncidents","Properties":{"path":"$.Content.LinkedIncidents"}},
    {"column":"AdditionalIncidentIds","Properties":{"path":"$.Content.AdditionalIncidentIds"}},
    {"column":"IncidentId","Properties":{"path":"$.Content.IncidentId"}},
    {"column":"AgentId","Properties":{"path":"$.Content.AgentId"}},
    {"column":"Age","Properties":{"path":"$.Content.Age"}},
    {"column":"ResolvedDateTime","Properties":{"path":"$.Content.ResolvedDateTime"}},
    {"column":"TenantId","Properties":{"path":"$.Content.CalculatedTenantId"}},
    {"column":"ResourceId","Properties":{"path":"$.Content.IssueContext.ResourceId"}},
    {"column":"EventDateTime","Properties":{"path":"$.RawProperties.UserProperties.EventDateTime"}},
    {"column":"SdpCreatedOn","Properties":{"path":"$.createdOn"}},
    {"column":"SdpModifiedOn","Properties":{"path":"$.modifiedOn"}},
    {"column":"RatioIncidentIds","Properties":{"path":"$.Content.RatioIncidentIds"}},
    {"column":"IsPurged","Properties":{"path":"$.IsPurged"}},
    {"column":"Content","Properties":{"path":"$.Content"}},
    {"column":"RawProperties","Properties":{"path":"$.RawProperties"}},
    {"column":"CosmosDbTs","Properties":{"path":"$._ts"}},
    {"column":"SdpInternal","Properties":{"path":"$.SdpInternal"}},
    {"column":"ttl","Properties":{"path":"$.ttl"}},
    {"column":"IsM365","Properties":{"path":"$.RawProperties.UserProperties.IsM365"}},
    {"column":"RestrictedAccessProgramName","Properties":{"path":"$.Content.RestrictedAccess.RestrictedAccessProgramName"}},
    {"column":"CaseUri","Properties":{"path":"$.Content.CaseUri"}},
     {"column":"CompleteEvent","Properties":{"path":"$","datatype":"dynamic"}}
  ]
  ```

.create-or-alter table RatioLinkage ingestion json mapping "RatioLinkage_mapping"
```
[
  {"column":"CaseNumber","Properties":{"path":"$.Content.CaseNumber","datatype":"string"}},
  {"column":"Linkages","Properties":{"path":"$.Content.Linkages","datatype":"dynamic"}},
  {"column":"EventType","Properties":{"path":"$.EventType","datatype":"string"}},
  {"column":"EntityType","Properties":{"path":"$.RawProperties.UserProperties.EntityType","datatype":"string"}},
  {"column":"EntityAction","Properties":{"path":"$.RawProperties.UserProperties.EntityAction","datatype":"string"}},
  {"column":"EntityEventDateTime","Properties":{"path":"$.RawProperties.UserProperties.EventDateTime","datatype":"datetime"}},
  {"column":"EntityID","Properties":{"path":"$.RawProperties.UserProperties.EntityID","datatype":"string"}},
  {"column":"CreatedBy","Properties":{"path":"$.createdBy","datatype":"string"}},
  {"column":"ModifiedBy","Properties":{"path":"$.modifiedBy","datatype":"string"}},
  {"column":"CreatedOn","Properties":{"path":"$.createdOn","datatype":"datetime"}},
  {"column":"ModifiedOn","Properties":{"path":"$.modifiedOn","datatype":"datetime"}},
  {"column":"EventDateTime","Properties":{"path":"$.SdpInternal.CornerStoneServiceBusEnqueueTime","datatype":"datetime"}},
  {"column":"CosmosDbTs","Properties":{"path":"$._ts","datatype":"long"}},
  {"column":"Content","Properties":{"path":"$","datatype":"dynamic"}}
]
```

.create-or-alter table IcmL2Aggregate ingestion json mapping "IcmL2Aggregate_mapping"
  ```
  [
    {"column":"IncidentIdCloud","Properties":{"path":"$.IncidentIdCloud"}},
    {"column":"DataBoundary","Properties":{"path":"$.DataBoundary"}},
    {"column":"IncidentId","Properties":{"path":"$.IncidentId"}},
    {"column":"EventTime","Properties":{"path":"$.EventTime"}},
    {"column":"EventType","Properties":{"path":"$.EventType"}},
    {"column":"SnapshotId","Properties":{"path":"$.SnapshotId"}},
    {"column":"SupportRequestsCount","Properties":{"path":"$.SupportRequestsCount"}},
    {"column":"IsCritSitCount","Properties":{"path":"$.IsCritSitCount"}},
    {"column":"LinkedSupportRequests_Ids_PUBLIC","Properties":{"path":"$.LinkedSupportRequests.Ids.Public"}},
    {"column":"LinkedSupportRequests_Ids_EU","Properties":{"path":"$.LinkedSupportRequests.Ids.EU"}},
    {"column":"LinkedSupportRequests_LinkedIncidents","Properties":{"path":"$.LinkedSupportRequests.LinkedIncidents"}},
    {"column":"FetchLink","Properties":{"path":"$.FetchLink"}},
    {"column":"SdpInternal","Properties":{"path":"$.SdpInternal"}},
    {"column":"Content","Properties":{"path":"$"}},
    {"column":"AutoLinkedCount","Properties":{"path":"$.AutoLinkedCount"}},
    {"column":"AutoLinkedCritSitCount","Properties":{"path":"$.AutoLinkedCritSitCount"}},
    {"column":"EventCreatedTime","Properties":{"path":"$.EventCreatedTime"}},
    {"column":"ManuallyLinkedCount","Properties":{"path":"$.ManuallyLinkedCount"}},
    {"column":"ManuallyLinkedCritSitCount","Properties":{"path":"$.ManuallyLinkedCritSitCount"}},
    {"column":"TotalCount","Properties":{"path":"$.TotalCount"}}
  ]
  ```

.create-or-alter table IncidentLinkage ingestion json mapping "IncidentLinkage_mapping"
```
[
  {"column":"CaseNumber","Properties":{"path":"$.Content.CaseNumber","datatype":"string"}},
  {"column":"Content","Properties":{"path":"$","datatype":"dynamic"}},
  {"column":"IncidentId","Properties":{"path":"$.Content.IncidentId","datatype":"string"}},
  {"column":"DataBoundary","Properties":{"path":"$.Content.DataBoundary","datatype":"string"}},
  {"column":"LinkSource","Properties":{"path":"$.Content.LinkSource","datatype":"string"}},
  {"column":"LinkCreatedOn","Properties":{"path":"$.Content.LinkCreatedOn","datatype":"string"}},
  {"column":"AggregatedNonDfmLinks","Properties":{"path":"$.Content.AggregatedNonDfmLinks","datatype":"dynamic"}},
  {"column":"Aggregate_Add","Properties":{"path":"$.Content.AggregatedNonDfmLinks.Add","datatype":"dynamic"}},
  {"column":"Aggregate_Remove","Properties":{"path":"$.Content.AggregatedNonDfmLinks.Remove","datatype":"dynamic"}},
  {"column":"EntityType","Properties":{"path":"$.RawProperties.UserProperties.EntityType","datatype":"string"}},
  {"column":"EntityAction","Properties":{"path":"$.RawProperties.UserProperties.EntityAction","datatype":"string"}},
  {"column":"EventDateTime","Properties":{"path":"$.RawProperties.UserProperties.EventDateTime","datatype":"datetime"}},
  {"column":"EntityID","Properties":{"path":"$.RawProperties.UserProperties.EntityID","datatype":"string"}},
  {"column":"CreatedBy","Properties":{"path":"$.createdBy","datatype":"string"}},
  {"column":"ModifiedBy","Properties":{"path":"$.modifiedBy","datatype":"string"}},
  {"column":"CreatedOn","Properties":{"path":"$.createdOn","datatype":"datetime"}},
  {"column":"ModifiedOn","Properties":{"path":"$.modifiedOn","datatype":"datetime"}},
  {"column":"SdpInternal","Properties":{"path":"$.SdpInternal","datatype":"dynamic"}},
  {"column":"CosmosDbTs","Properties":{"path":"$._ts","datatype":"long"}}
]
```

.create-or-alter function with (folder = "SRICM", docstring = "Return latest snapshot data from cold path that is more recent than hot path", skipvalidation = "true") GetSRDataBySRNumbers(SupportRequestNumbers:dynamic) {
    SrSnapshot
    | where CaseNumber  in (SupportRequestNumbers)
    | summarize arg_max(EventDateTime, *) by CaseNumber
    | where SREventActionType != 'Delete'
    | project CaseNumber, SREventActionType, CritSit, State, Severity, Status, Product, LinkedICMs, ModifiedOn, CreatedOn, ModifiedBy, CreatedBy, DataBoundary, AzureSubscriptionId, Customers, SupportTopicTitle, SupportRegion, ResourceId, AgentId, Age, ResolvedDateTime, TenantId
}

.create-or-alter function with (folder = "SRICM", docstring = "Returns The SRs linked with the ICM with L2 aggregation", skipvalidation = "true") SearchSRsByICMId(IcmIds:dynamic, IcmDataboundary:string="PUBLIC") {
// let IcmIds=dynamic(['675266580','675262929']); //
// let IcmDataboundary='PUBLIC';
//
let databoundary=toupper(IcmDataboundary);
 //convert dynamics to table structure
let Icms= print Icm =IcmIds |mv-expand Icm|extend Icm=tostring(Icm);
//based on databoundry identify create possible IncidentIdCloud
let inputIcmWithBoundary=Icms| summarize Public=make_list(strcat(Icm,'-PUBLIC')),EU=make_list(strcat(Icm,'-EU'))| extend icms=iff(databoundary=='PUBLIC',Public,iff(databoundary =='EU',EU,array_concat(Public,EU))) | project-away Public,EU | mv-expand icms| extend IcmCloud=tostring(icms);
// get l2 aggregate recieved from icm L2Details identifies if icm result will be L1 or L2 
let L2Details= inputIcmWithBoundary| join kind=leftouter IcmL2AggregateView
 on $left.IcmCloud==$right.IncidentIdCloud 
 | project IcmCloud,
 Public=todynamic(iff(isempty(LinkedSupportRequests_Ids_PUBLIC),'[]',LinkedSupportRequests_Ids_PUBLIC)),
 EU=todynamic(iff(isempty(LinkedSupportRequests_Ids_EU),'[]',LinkedSupportRequests_Ids_EU)),
 SRIncidentsMap=LinkedSupportRequests_LinkedIncidents,
 EventCreatedTime,
 IncidentId,
 DataBoundary;
//get icms for which L1 aggrerate is returned
let directIcms= L2Details| where isempty(EventCreatedTime)| summarize IcmCloud=make_list(IcmCloud)|  mv-expand IcmCloud| extend IcmCloud=tostring(IcmCloud);
// get casenumbers for L1 aggregate ICMS
let CaseNumbers = toscalar(SrSnapshotByIcmNumberView | extend parts = split(SRNumIcmId, "-")| extend ICMId = parts[1]
    | where  strcat(ICMId,'-',DataBoundary) in (directIcms)
    | summarize max(EventDateTime) by CaseNumber | project CaseNumber| summarize casenumbers=make_list(CaseNumber)) ; 
//get L1 aggregate SR info for L1 aggregate Icms
let directsrs=SrSnapshotView
    | where CaseNumber in (CaseNumbers )
    |mv-expand LinkedICMs
    |extend IncidentIdCloud=tostring(LinkedICMs.IncidentIdCloud),
        Number=tostring(LinkedICMs.Number),
        IncidentDataBoundary=tostring(LinkedICMs.DataBoundary),
        Source=tostring(LinkedICMs.Source),
        Type=tostring(LinkedICMs.Type),
        LinkedDateTime=todatetime(LinkedICMs.LinkedDateTime)
    | where IncidentIdCloud in (directIcms)
    |project ICMId=Number, SupportRequestNumber=CaseNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product=tostring(Product), SupportRegion, Customers=tostring(Customers), CreatedOn, IncidentId=Number,IncidentDataBoundary,  Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,AggregationSource='Direct';
//get L2 aggregate ICMs details
let indirectIcms=L2Details| where isnotempty(EventCreatedTime)
| mv-expand SRIncidentsMap
|extend SRIncidentsMap=todynamic(SRIncidentsMap)
| extend SrNumberBoundary = bag_keys(SRIncidentsMap)
| mv-expand SrNumberBoundary
|extend SrNumberBoundary=tostring(SrNumberBoundary)
| extend ICMBoundary = SRIncidentsMap[SrNumberBoundary]
//|extend CheckIn=array_concat(Public,EU)
| mv-expand ICMBoundary | extend ICMBoundary=tostring(ICMBoundary)
//|where set_has_element(CheckIn,tostring(split(SrNumberBoundary, ".")[1])) == true;
| extend ICMBoundary=strcat(split(ICMBoundary, ".")[1], "-", toupper(split(ICMBoundary, ".")[0]))
|extend SrNumber=tostring(split(SrNumberBoundary, ".")[1])
|extend SRICMRelation=strcat(SrNumber,'-',ICMBoundary)
|project-away EU,Public, SRIncidentsMap,SrNumberBoundary;
//get all L2 aggregate ICMs SR irrespective of Direct or Indirect Aggregation source
let alll2srswithalllinkages=indirectIcms | distinct IcmCloud,IncidentId, SrNumber | join kind=leftouter SrSnapshotView on $left.SrNumber==$right.CaseNumber 
| project IcmCloud,ICMId=IncidentId, SupportRequestNumber=CaseNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product=tostring(Product), SupportRegion, Customers=tostring(Customers), CreatedOn,  ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,LinkedICMs,CaseUri
| mv-expand LinkedICMs 
| extend IncidentIdCloud=tostring(LinkedICMs.IncidentIdCloud),
        Number=tostring(LinkedICMs.Number),
        IncidentDataBoundary=tostring(LinkedICMs.DataBoundary),
        Source=tostring(LinkedICMs.Source),
        Type=tostring(LinkedICMs.Type),
        LinkedDateTime=todatetime(LinkedICMs.LinkedDateTime)
| extend SRICMRelation=strcat(SupportRequestNumber,'-',IncidentIdCloud)
|project-away LinkedICMs;
// get all L2 aggregate data
let all=indirectIcms| join kind=inner alll2srswithalllinkages on SRICMRelation
//| where SRICMRelation ==SRICMRelation1  //| extend AggMatchWith=strcat(Number,'-',IncidentDataBoundary)
| project IcmCloud,ICMId, SupportRequestNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product, SupportRegion, Customers, CreatedOn,IncidentId=Number,IncidentDataBoundary, Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,IncidentIdCloud
|distinct IcmCloud,ICMId, SupportRequestNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product, SupportRegion, Customers, CreatedOn,IncidentId,IncidentDataBoundary, Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,IncidentIdCloud;
// get all L2 aggregate direct linked ICM
// let minDirect=all| where strcat(IncidentId,'-',IncidentDataBoundary) == IcmCloud
// |summarize arg_min(LinkedDateTime,*) by IcmCloud,SupportRequestNumber,IncidentId,IncidentDataBoundary 
// | extend Priority=0
// |project IcmCloud,Priority, ICMId, SupportRequestNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product, SupportRegion, Customers, CreatedOn,IncidentId,IncidentDataBoundary, Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,AggregationSource=iff(IncidentIdCloud==strcat(IcmCloud),'Direct','Indirect');
// get all L2 aggregate min linkedDateTime Manual
let minmanual=all| where Type =='Manual'
|summarize arg_min(LinkedDateTime,*) by IcmCloud,SupportRequestNumber | extend Priority=1
|project IcmCloud,Priority, ICMId, SupportRequestNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product, SupportRegion, Customers, CreatedOn,IncidentId,IncidentDataBoundary, Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,AggregationSource=iff(IncidentIdCloud==strcat(IcmCloud),'Direct','Indirect'); //IncidentId,'-',IncidentDataBoundary
// get all L2 aggregate min linkedDateTime Automatic
let minautomatic=all| where Type =='Automatic'
|summarize arg_min(LinkedDateTime,*) by IcmCloud,SupportRequestNumber | extend Priority=2
|project IcmCloud,Priority, ICMId, SupportRequestNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product, SupportRegion, Customers, CreatedOn,IncidentId,IncidentDataBoundary, Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,AggregationSource=iff(IncidentIdCloud==strcat(IcmCloud),'Direct','Indirect');
// get all L2 aggregate min linkedDateTime for manual if not present then get automatic
//let l2Indirectlinkages=union kind=outer minDirect,minmanual,minautomatic
let l2Indirectlinkages=union kind=outer minmanual,minautomatic
|summarize arg_min(Priority,*) by IcmCloud,SupportRequestNumber | project-away Priority
|extend ICMId=tostring(split(IcmCloud,'-')[0]);
// union L1 aggregate, L2 aggregate Direct Aggregation source and L2 aggregate Indirect Aggregation source
union kind=outer directsrs,l2Indirectlinkages
|project ICMId=tostring(ICMId), SupportRequestNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product=todynamic(Product), SupportRegion, Customers=todynamic(Customers), CreatedOn,IncidentId,IncidentDataBoundary, Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,AggregationSource
| where isnotempty( Source)
|order by ICMId,AggregationSource asc
}'''
        continueOnErrors: true
        forceUpdateTag: forceUpdateTag
    }
    dependsOn:[kustoPICMRatioData1]
}



resource kustoPICMRatioData3 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = if(deployICMRatioData) {
    name: '${PclusterName}/ICMRatioData/PICMRatioData3'
    properties: {
        scriptContent: '''.create-or-alter function with (folder = "SRICM/Reconciliation", docstring = "Return latest snapshot data from cold path that is more recent than hot path", skipvalidation = "true") GetRatioReconciliationData() {
    let hot = database("ICMSRLinkageData").RatioLinkage
    | mv-expand Linkage=Linkages
    | project 
        CaseNumber
        , IncidentId=tostring(Linkage.Number)
        , Action=tostring(Linkage.Action)
        , LinkedDateTime=todatetime(Linkage.LinkedDateTime)
        , IcMCloud=toupper(tostring(Linkage.DataBoundary))
        , Reason=tostring(Linkage.Reason)
        , Source=tostring(Linkage.Source)
    | summarize arg_max(LinkedDateTime, *) by CaseNumber, IncidentId, IcMCloud;
    let cold = database("ICMSRLinkageData").ValidationRatioICMLinkages
    | project 
        CaseNumber
        , IncidentId
        , Action=case(tolower(Action)=='add', 'Linked', tolower(Action)=='remove', 'Removed', Action)
        , Processtimestamp
        , IcMCloud=toupper(IcMCloud)
        , Reason
        , Source
    | summarize arg_max(Processtimestamp, *) by CaseNumber, IncidentId, IcMCloud;
    cold
    | join kind=fullouter hot on $left.CaseNumber==$right.CaseNumber and $left.IncidentId==$right.IncidentId and $left.IcMCloud==$right.IcMCloud
    | where Processtimestamp > LinkedDateTime or isempty(LinkedDateTime)
}

.create-or-alter function with (folder = "SRICM/Reconciliation", docstring = "Return latest snapshot data from cold path that is more recent than hot path", skipvalidation = "true") GetRatioReconciliationDataJson() {
    GetRatioReconciliationData() 
    | project 
        CaseNumber
        , IncidentId
        , IcMCloud
        , EventDateTime=Processtimestamp
        , Type="RATIO"
        , Content=bag_pack("Content", bag_pack("CaseNumber", CaseNumber, "Linkages", pack_array(bag_pack("Number", IncidentId, "Type", 'Automatic', "Source", 'RATIO', "DataBoundary", IcMCloud, "LinkedDateTime", Processtimestamp, "IncidentIdCloud", strcat(IncidentId, '-', IcMCloud), "Action", Action, "Reason", Reason))), "id", CaseNumber, "EventType", 'ratiolinkage', "RawProperties", bag_pack("UserProperties", bag_pack("EntityType", 'ratiolinkage', "EntityAction", Action, "EventDateTime", Processtimestamp, "EntityID", CaseNumber)), "createdBy", 'ReconciliationEngine', "modifiedBy", 'ReconciliationEngine',"SdpServiceName",'CXOAI', "createdOn", now(), "modifiedOn", now())
}

.create-or-alter function with (folder = "SRICM/Reconciliation", docstring = "Return latest snapshot data from cold path that is more recent than hot path", skipvalidation = "true") GetSRSnapshotReconciliationData(windowoffsethours:int=-4) {
    let windowoffset = (windowoffsethours) * 1h;
    let windowlag = 10m;
    let beginwindowdate = now() + windowoffset; //Negative hours input, so add
    let endwindowdate = now() - windowlag;
    let currentutc = now();
    let cold = database("SupportValidationData").SupportCasesRawRecords 
    | extend SREventActionType=parse_json(Content).RawProperties.UserProperties.EventType 
    | where EntityType == 'Case' and SREventActionType <> 'Delete' 
    | project CaseNumber, EventDateTime, Content 
    | summarize arg_max(EventDateTime,*) by CaseNumber;
    let hot = database("ICMRatioData").SrSnapshot 
    | where SREventActionType <> 'Delete' 
    | extend ContentPacked=bag_pack("Content", Content, "RawProperties", RawProperties) 
    | project CaseNumber, EventDateTime, ContentPacked 
    | summarize arg_max(EventDateTime,*) by CaseNumber;
    cold
    | join kind=inner hot on $left.CaseNumber == $right.CaseNumber
    | where isempty(EventDateTime1) or (EventDateTime > EventDateTime1 and EventDateTime between (beginwindowdate .. endwindowdate))
}

.create-or-alter function with (folder = "SRICM/Reconciliation", docstring = "Return latest snapshot data from cold path that is more recent than hot path, in json format", skipvalidation = "true") GetSRSnapshotReconciliationDataJson(windowoffsethours:int=-4) {
    GetSRSnapshotReconciliationData(windowoffsethours)
    | project 
        CaseNumber
        , IncidentId=''// Can populate this if needed
        , IcMCloud= '' // Can populate this for this data if needed.
        , EventDateTime
        , Type="SNAPSHOT"
        , Content=bag_merge(Content, bag_pack("SdpServiceName", 'CXOAI', "ReconcileProccessTime",now()))
}

.create-or-alter function with (folder = "SRICM/Reconciliation", docstring = "Get Reconcilication data in tabular format", skipvalidation = "true") GetReconciliationData() {
    let newdata=ReconciliationData
    | extend x=strcat(CaseNumber,IncidentIds,IcMCloud,EventDateTime,Type)
    | project-away x;
    let history = ReconciliationDataHistory
    | summarize ReconciliationCount=count() by CaseNumber, IncidentIds, IcMCloud, EventDateTime, Type;
    newdata
    | join kind=leftouter  history on $left.CaseNumber == $right.CaseNumber and $left.IncidentIds == $right.IncidentIds and $left.EventDateTime == $right.EventDateTime and $left.Type == $right.Type
    | where ReconciliationCount <= 5
}

.create-or-alter function with (folder = "SRICM/Reconciliation", docstring = "Return reconciliation data in json format", skipvalidation = "true") GetReconciliationDataJson() {
    GetReconciliationData()
    | project CaseNumber, Content = replace(@'\r\n|\n', "", tostring(Content))
}

.create-or-alter function with (folder = "SRICM", docstring = "Returns The SRs linked with the ICM for L1 aggregation", skipvalidation = "true") GetSRsByICMId_L1(IcmIds:dynamic, IcmDataboundary:string="PUBLIC") {
let databoundary=toupper(IcmDataboundary);
 //convert dynamics to table structure
let Icms= print Icm =IcmIds |mv-expand Icm|extend Icm=tostring(Icm);
//based on databoundry identify create possible IncidentIdCloud
let inputIcmWithBoundary=Icms| summarize Public=make_list(strcat(Icm,'-PUBLIC')),EU=make_list(strcat(Icm,'-EU'))| extend icms=iff(databoundary=='PUBLIC',Public,iff(databoundary =='EU',EU,array_concat(Public,EU))) | project-away Public,EU | mv-expand icms| extend icms=tostring(icms);
//get icms for which L1 aggrerate is returned
let directIcms= Icms| summarize icmspublic=make_list(strcat(Icm,'-PUBLIC')), icmseu=make_list(strcat(Icm,'-EU')) | extend icms=iff(databoundary=='PUBLIC',icmspublic,iff(databoundary =='EU',icmseu,array_concat(icmspublic,icmseu))) | project-away icmspublic,icmseu | mv-expand icms| extend icms=tostring(icms);
// get casenumbers for L1 aggregate ICMS
let CaseNumbers = toscalar(SrSnapshotByIcmNumberView | extend parts = split(SRNumIcmId, "-")| extend ICMId = parts[1]
    | where  strcat(ICMId,'-',DataBoundary) in (directIcms)
    | summarize max(EventDateTime) by CaseNumber | project CaseNumber| summarize casenumbers=make_list(CaseNumber)) ; 
//get L1 aggregate SR info for L1 aggregate Icms
let directsrs=SrSnapshotView
    | where CaseNumber in (CaseNumbers )
    |mv-expand LinkedICMs
    |extend IncidentIdCloud=tostring(LinkedICMs.IncidentIdCloud),
        Number=tostring(LinkedICMs.Number),
        IncidentDataBoundary=tostring(LinkedICMs.DataBoundary),
        Source=tostring(LinkedICMs.Source),
        Type=tostring(LinkedICMs.Type),
        LinkedDateTime=todatetime(LinkedICMs.LinkedDateTime)
    | where IncidentIdCloud in (directIcms)
    |project ICMId=Number, SupportRequestNumber=CaseNumber, SupportTopicTitle, Severity, CritSit, State, Status, Product=tostring(Product), SupportRegion, Customers=tostring(Customers), CreatedOn, IncidentId=Number,IncidentDataBoundary,  Source, LinkedDateTime, Type, ResourceId, AgentId, Age, ResolvedDateTime, AzureSubscriptionId, TenantId, ModifiedOn, DataBoundary,CaseUri,AggregationSource='Direct'
|order by ICMId,AggregationSource asc;
directsrs
}

.create-or-alter function with (folder = "ADF", docstring = "Return CaseNumber which needs to be expired in redis", skipvalidation = "true") GetRedisKeyToBeExpired() {
 let redis=ICMLinkageRedisKeyStore| summarize arg_max(TimeStamp,*) by CaseNumber;
redis| join kind=leftouter SrSnapshotView on CaseNumber
| extend idealttl=((now(7d) - now()) / 1m)
| where (State in ('Closed') or SREventActionType in ('Delete') or isempty(CaseNumber1)  ) and ttl > idealttl
| take 1000
| project CaseNumber,ttl,Prefix,State,SREventActionType,SdpModifiedOn
| extend packed = bag_pack(CaseNumber, 1)
| summarize Payload= make_bag(packed) by Prefix
| project Value=tostring(bag_pack("ActionType","SET","Payload",iif(isempty(Payload),dynamic({}),Payload)))
}

.create-or-alter function with (folder = "SRICM", docstring = "Return Ratio ICMs which were overridden by DFM", skipvalidation = "true") GetRatioLinkageOverriddenIcms(fromdate:datetime) {
let allratiolinkage=RatioLinkage | summarize arg_max(EventDateTime,*) by CaseNumber | where EventDateTime > fromdate
|mv-expand Linkages
| extend Number=tostring(Linkages.Number)
| extend Type=tostring(Linkages.Type)
| extend Source=tostring(Linkages.Source)
| extend DataBoundary=tostring(Linkages.DataBoundary)
| extend IncidentIdCloud=tostring(Linkages.IncidentIdCloud)
| extend Action=tostring(Linkages.Action)
| extend LinkedDateTime=todatetime(Linkages.LinkedDateTime);
let valid= (allratiolinkage
|summarize Add=countif(Action=='Linked'),Removed=countif(Action!='Linked') by CaseNumber,Number,DataBoundary,IncidentIdCloud,Type,Source
| where Add >Removed) | join kind=inner allratiolinkage on CaseNumber,Number,DataBoundary,Type,Source
|project CaseNumber,Number,DataBoundary,IncidentIdCloud,Type,Source,LinkedDateTime;
valid | join kind=inner SrSnapshotView on CaseNumber
|mv-expand LinkedICMs
| extend ICMNumber=tostring(LinkedICMs.Number)
| extend ICMType=tostring(LinkedICMs.Type)
| extend ICMSource=tostring(LinkedICMs.Source)
| extend ICMDataBoundary=tostring(LinkedICMs.DataBoundary)
| extend ICMIncidentIdCloud=tostring(LinkedICMs.IncidentIdCloud)
| extend ICMLinkedDateTime=todatetime(LinkedICMs.LinkedDateTime)
| project CaseNumber,IncidentIdCloud,Number,Type,Source,DataBoundary,ICMNumber,ICMType,ICMSource,ICMDataBoundary,ICMIncidentIdCloud,ICMLinkedDateTime,LinkedDateTime
| where ICMIncidentIdCloud ==IncidentIdCloud and ICMSource !='RATIO'
| project CaseNumber, ICMNumber, ICMDataBoundary,ICMSource,ICMLinkedDateTime,RatioLinkedDateTime=LinkedDateTime,RatioSource=Source
|extend DurationOfVisibleRatioSourceInHours=datetime_diff('hour',ICMLinkedDateTime,RatioLinkedDateTime)
}

.create-or-alter function with (folder = "ADF", docstring = "Get SrSnapshot Change Feed", skipvalidation = "true") GetSRSnapshotChangeFeed(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
let skiprow=batch*index;
let dayStringMapping = 
    union 
    (print FieldPath = '$.SDPICMLinkageCorrection', Value = correctionReason, DataType='string' ),
    (print FieldPath = '$.SdpServiceName', Value = 'CXOAI',  DataType='string' );
let MetaData= toscalar(dayStringMapping |extend bag= bag_pack("FieldPath",FieldPath,"Value",Value,"DataType",DataType)| summarize make_list(bag) | project list_bag);
let latestSrs=ChangeFeedPipelineSRs|summarize arg_max(ExecutionTime,*) by CaseNumber|order by ModifiedOn asc| extend rownum=row_number()|where rownum >skiprow|take batch;
latestSrs| join kind=leftouter database('AceHubSupportData').CaseUriBackFill on CaseNumber | project-away CaseNumber1 //remove line
| join kind=inner SrSnapshotView on CaseNumber  |
where EventDateTime between (startTime.. endtime)  and isnotempty(State)
|order by ModifiedOn asc 
| extend Content=bag_merge(bag_remove_keys(Content,dynamic(["CaseUri"])),bag_pack("CaseUri",CaseUri)) //remove line
| extend Snapshot= bag_pack("Content",Content,"RawProperties",RawProperties)
| extend Value=bag_pack("Snapshot",Snapshot,"MetaData",MetaData,"Source","raw")
| project Key=CaseNumber,tostring(Value)
}'''
        continueOnErrors: true
        forceUpdateTag: forceUpdateTag
    }
    dependsOn:[kustoPICMRatioData2]
}



resource kustoPICMRatioData4 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = if(deployICMRatioData) {
    name: '${PclusterName}/ICMRatioData/PICMRatioData4'
    properties: {
        scriptContent: '''.create-or-alter function with (folder = "ADF", docstring = "Get SrSnapshot Change Feed Count", skipvalidation = "true") GetSRSnapshotChangeFeedCount(startTime:datetime, endtime:datetime, batch:int, index:int) {
let skiprow=batch*index;
let latestSrs=ChangeFeedPipelineSRs|summarize arg_max(ExecutionTime,*) by CaseNumber;
latestSrs|order by ModifiedOn asc| extend rownum=row_number()|where rownum >skiprow|take batch| count
}

.create-or-alter function with (folder = "SRICM", docstring = "Get Ratio Linkage Latency", skipvalidation = "true") GetRatioLinkageLatency(startTime:datetime, endtime:datetime) {
let ratiodata=RatioLinkage
| summarize arg_max(EventDateTime,*) by CaseNumber| where EventDateTime between (startTime .. endtime )
|mv-expand Linkages
|extend Number=tostring(Linkages.Number),Source=tostring(Linkages.Source), IncidentIdCloud=tostring(Linkages.IncidentIdCloud),LinkedDateTime=todatetime(Linkages.LinkedDateTime),Action=tostring(Linkages.Action),DataBoundary=tostring(Linkages.DataBoundary)
| project CaseNumber,Number,Source,IncidentIdCloud,LinkedDateTime,Action,DataBoundary;
let validdata=ratiodata|summarize Add=countif(Action=='Linked'),Removed=countif(Action != 'Linked') by CaseNumber,IncidentIdCloud,DataBoundary,Number,Source
|where Add>Removed
|join kind=inner ratiodata on CaseNumber,IncidentIdCloud
|project CaseNumber,Number,Source,IncidentIdCloud,LinkedDateTime,Action,DataBoundary;
let srjoin=validdata|join kind=inner SrSnapshot on CaseNumber
|project CaseNumber,Number,Source,IncidentIdCloud,LinkedDateTime,Action,DataBoundary,CreatedOn,ModifiedOn,EventDateTime
|where isnotempty(CreatedOn ) and isnotempty( ModifiedOn)
|where EventDateTime < LinkedDateTime
|summarize arg_max(EventDateTime,*)by CaseNumber
|extend LatencyRatioLinkages=datetime_diff('minute',LinkedDateTime,EventDateTime)
|where EventDateTime > todatetime('2025-06-01') | order by EventDateTime;
srjoin
}

.create-or-alter function with (folder = "SRICM", docstring = "", skipvalidation = "true") CleanICMs(text:string) {
let clean1=
iff(isnotempty(extract(@"(\d{15})",0,text)),extract(@"(\d{15})",0,text),
iff(isnotempty(extract(@"(\d{14})",0,text)),extract(@"(\d{14})",0,text),
iff(isnotempty(extract(@"(\d{13})",0,text)),extract(@"(\d{13})",0,text),
iff(isnotempty(extract(@"(\d{12})",0,text)),extract(@"(\d{12})",0,text),
iff(isnotempty(extract(@"(\d{11})",0,text)),extract(@"(\d{11})",0,text),
iff(isnotempty(extract(@"(\d{10})",0,text)),extract(@"(\d{10})",0,text),
iff(isnotempty(extract(@"(\d{9})",0,text)),extract(@"(\d{9})",0,text),
iff(isnotempty(extract(@"(\d{8})",0,text)),extract(@"(\d{8})",0,text),
iff(isnotempty(extract(@"(\d{7})",0,text)),extract(@"(\d{7})",0,text),'')))))))));
let cleaned=iff(strlen(clean1) in (15,13,11,8) , '',
iff(strlen(clean1)==14 and toint(substring(clean1,0,1))>=2 and text !contains('visualstudio') and text !contains('azure') and text !contains('office'), clean1,
iff(strlen(clean1)==12 and toint(substring(clean1,0,3)) in (999,900,111) and text !contains('visualstudio') and text !contains('azure') and text !contains('office'), substring(clean1,3,9),
iff(strlen(clean1)==10 and toint(substring(clean1,0,1)) in (1) and text !contains('visualstudio') and text !contains('azure') and text !contains('office'), substring(clean1,1,9),
iff(strlen(clean1)==9 and toint(substring(clean1,0,1)) >=1 and text !contains('visualstudio') and text !contains('azure') and text !contains('office'), clean1,
iff(strlen(clean1)==7 and toint(substring(clean1,0,1)) !=0 and text !contains('visualstudio') and text !contains('azure') and text !contains('office'), clean1,''
))))));
cleaned
}

.create-or-alter function with (folder = "ADF", docstring = "Get IcmL2Aggregate Change Feed Count", skipvalidation = "true") GetIcmL2AggregateChangeFeedCount(startTime:datetime, endtime:datetime, batch:int, index:int) {
let skiprow=batch*index;
let latestl2aggregates=ChangeFeedPipelineIcmL2Aggregate|summarize arg_max(ExecutionTime,*) by IncidentIdCloud;
latestl2aggregates|order by EventCreatedTime asc| extend rownum=row_number()|where rownum >skiprow|take batch| count
}

.create-or-alter function with (folder = "ADF", docstring = "Get IcmL2Aggregate Change Feed", skipvalidation = "true") GetIcmL2AggregateChangeFeed(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
let skiprow=batch*index;
let currentTime=now();
let latestSrs=ChangeFeedPipelineIcmL2Aggregate|summarize arg_max(ExecutionTime,*) by IncidentIdCloud|order by EventCreatedTime asc| extend rownum=row_number()|where rownum >skiprow|take batch;
latestSrs| join kind=inner (IcmL2AggregateView | extend DataBoundary=iff(isempty(DataBoundary),'PUBLIC',DataBoundary),IncidentIdCloud=iff(isempty( IncidentIdCloud),strcat(IncidentId,'-PUBLIC'),IncidentIdCloud))  on IncidentIdCloud |
where EventCreatedTime between (startTime.. endtime)
|order by EventCreatedTime asc 
| extend bag=bag_pack("SDPIcmL2AggregateCorrection",correctionReason,"SdpServiceName","ICMUtility","DataBoundary",DataBoundary, "IncidentIdCloud",IncidentIdCloud,"EventCreatedTime",currentTime,"id",IncidentIdCloud,"EventTime",currentTime)
|extend Content=bag_merge(bag,Content)
| project Key=IncidentIdCloud,Value=tostring(Content)
}

.create-or-alter function with (folder = "SRICM", docstring = "Returns The SRs linked with the ICM with L2 aggregation with complaince fields", skipvalidation = "true") SearchSRsByICMIdWithComplainceFields(IcmIds:dynamic, IcmDataboundary:string="PUBLIC") {
let icmdata=SearchSRsByICMId(IcmIds,IcmDataboundary);
let casenumbers=toscalar(icmdata | summarize make_list(SupportRequestNumber));
let support=database('AceHubSupportData').GetSRComplianceFieldsByCaseNumber(casenumbers);
icmdata| join kind=inner support on $left.SupportRequestNumber==$right.CaseNumber | project-away CaseNumber | extend ProductFamily=tostring(Product.productFamily)| extend CloudName=iif(IsM365==true,'M365',iff(ProductFamily=='Azure','Azure',iff(ProductFamily =='Dynamics','Dynamics','Misc')))
|project-away IsM365,ProductFamily
}

.create-or-alter function with (folder = "ADF", docstring = "Get IncidentLinkage Change Feed Count", skipvalidation = "true") GetIncidentLinkageChangeFeedCount(startTime:datetime, endtime:datetime, batch:int, index:int) {
let skiprow=batch*index;
let latestIncidentLinkages=ChangeFeedPipelineIncidentLinkage|summarize arg_max(ExecutionTime,*) by CaseNumber;
latestIncidentLinkages|order by EventDateTime asc| extend rownum=row_number()|where rownum >skiprow|take batch| count
}

.create-or-alter function with (folder = "ADF", docstring = "Get IncidentLinkage Change Feed", skipvalidation = "true") GetIncidentLinkageChangeFeed(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
let skiprow=batch*index;
let currentTime=now();
let latestSrs=ChangeFeedPipelineIncidentLinkage|summarize arg_max(ExecutionTime,*) by CaseNumber|order by EventDateTime asc| extend rownum=row_number()|where rownum >skiprow|take batch;
latestSrs| join kind=inner (IncidentLinkage | where LinkSource =='RATIO'| summarize arg_max(EventDateTime,*) by CaseNumber )  on CaseNumber |
where EventDateTime between (startTime.. endtime)
|order by EventDateTime asc 
| project CaseNumber,Aggregate_Add,Content,Aggregate_Remove
|mv-expand Aggregate_Add
| extend Number=tostring(Aggregate_Add.Number),
Type=tostring(Aggregate_Add.Type),
DataBoundary=tostring(Aggregate_Add.DataBoundary),
Source=tostring(Aggregate_Add.Source),
LinkedDateTime=todatetime(Aggregate_Add.LinkedDateTime),
IncidentIdCloud=tostring(Aggregate_Add.IncidentIdCloud),
Action=tostring(Aggregate_Add.Action),
Reason=tostring(Aggregate_Add.Reason)
| extend Number=CleanICMs(Number) | extend IncidentIdCloud=strcat(Number,'-',DataBoundary)
| extend Aggregate_Add=bag_pack('Number',Number,'Type',Type,'DataBoundary',DataBoundary,'Source',Source,'LinkedDateTime',LinkedDateTime,'IncidentIdCloud',IncidentIdCloud,'Action',Action,'Reason',Reason)
| summarize Aggregate_Add=make_list(Aggregate_Add) by CaseNumber,tostring(Aggregate_Remove),tostring(Content)
| extend AggregatedNonDfmLinks=bag_pack("Add",Aggregate_Add,"Remove",todynamic(Aggregate_Remove)) 
|extend Content=todynamic(Content) | project-away Aggregate_Add,Aggregate_Remove
| extend InnerContent=Content.Content, Raw=Content.RawProperties, User=Content.RawProperties.UserProperties
|extend InnerContent=bag_merge(bag_remove_keys(InnerContent, dynamic(["AggregatedNonDfmLinks","EventDateTime"])),bag_pack("AggregatedNonDfmLinks",AggregatedNonDfmLinks,"EventDateTime",currentTime,"SDPIncidentLinkageCorrection",correctionReason,"SdpServiceName","CXOAI"))
|extend User=bag_merge(bag_remove_keys(User, dynamic(["EventDateTime"])),bag_pack("EventDateTime",currentTime))
|extend Raw=bag_merge(bag_remove_keys(Raw, dynamic(["UserProperties"])),bag_pack("UserProperties",User)) | project-away User
|extend Content=bag_merge(bag_remove_keys(Content, dynamic(["Content","RawProperties"])),bag_pack("Content",InnerContent,"RawProperties",Raw)) 
| project-away InnerContent,Raw,AggregatedNonDfmLinks
| project Key=CaseNumber,Value=tostring(Content)
}

.create-or-alter function with (folder = "ADF", docstring = "Get Missing Ratio Linkage ", skipvalidation = "true") GetRatioLinkageMissingEvent(executionTime:datetime) {
 cluster("ratiorepwus3prod.westus3").database("ratiodata").View_allsupportoutagescore()
    | where likelihood == "High"   and Processtimestamp >=todatetime('2001-01-01')
    | join kind=leftouter (
    SrSnapshotView  | project-away IncidentId
    ) on $left.CaseNumber==$right.CaseNumber  
    |where LinkedICMs !contains tostring(IncidentId) and isnotempty(SREventActionType ) and SREventActionType !='Delete'
    | project CaseNumber,SREventActionType,EventDate,LinkedICMs, LinkedIncidents,RatioIncidentIds,NonDFMLinkageActivities,IncidentId=tostring(IncidentId),ModifiedOn=iff(isempty(ModifiedOn ),datetime('2025-05-19'),ModifiedOn)
| distinct CaseNumber,EventDateTime=EventDate,ExecutionTime=executionTime
}

.create-or-alter function with (folder = "ADF", docstring = "Get Missing Ratio Linkage ", skipvalidation = "true") GetRatioLinkageMissingEventNonProd(executionTime:datetime) {
let dt=datatable(CaseNumber:string,EventDateTime:datetime,ExecutionTime:datetime)[];
dt| project CaseNumber,EventDateTime,ExecutionTime
}'''
        continueOnErrors: true
        forceUpdateTag: forceUpdateTag
    }
    dependsOn:[kustoPICMRatioData3]
}



resource kustoPICMRatioData5 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = if(deployICMRatioData) {
    name: '${PclusterName}/ICMRatioData/PICMRatioData5'
    properties: {
        scriptContent: '''.create-or-alter function with (folder = "ADF", docstring = "Get RatioLinkage Change Feed Count", skipvalidation = "true") GetRatioLinkageChangeFeedCount(startTime:datetime, endtime:datetime, batch:int, index:int) {
let skiprow=batch*index;
let latestIncidentLinkages=ChangeFeedPipelineRatioLinkage|summarize arg_max(ExecutionTime,*) by CaseNumber;
latestIncidentLinkages|order by EventDateTime asc| extend rownum=row_number()|where rownum >skiprow|take batch| count
}

.create-or-alter function with (folder = "ADF", docstring = "Get RatioRecords for CaseNumbers", skipvalidation = "true") GetRatioLinkageChangeFeedNonProd(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
let skiprow=batch*index;
let currentTime=now();
let casenumbers=ChangeFeedPipelineRatioLinkage  |summarize arg_max(ExecutionTime,*) by CaseNumber|order by EventDateTime asc| extend rownum=row_number()|where rownum >skiprow|take batch | distinct CaseNumber;
let csinput= toscalar(casenumbers
    | summarize CaseNumberlist=iff(array_length(make_list(CaseNumber))==0,dynamic(['0']),make_list(CaseNumber))
    | extend result=bag_pack('updatesince', datetime("2001-01-01"), 'CaseNumberlist', CaseNumberlist)
    | project result);
cluster('primodsshare.westus3').database('primosharedbdev').Generate_SupportOutageLinkHistory(csinput)
| extend sortfield=strcat(Processtimestamp, '-', Action)
| order by sortfield asc
| project-away sortfield
| extend DataBoundary=iff(tolower(IcMCloud) contains "eu", "EU", "PUBLIC")
| summarize
    Linkages = make_list
               ( 
                   bag_pack("Number", tostring(IncidentId), "Type", "Automatic", "Source", Source, "DataBoundary", DataBoundary, "IncidentIdCloud", strcat(IncidentId, "-", DataBoundary), "LinkedDateTime", Processtimestamp, "Action", iff(Action == "Add", "Linked", "Removed"), "Reason", "")
               ),
    EntityType  = any("ratiolinkage"), 
    EntityAction = any("Create"), 
    EventDateTime= currentTime
    by CaseNumber 
| extend
    Content = bag_pack("CaseNumber", CaseNumber, "Linkages", Linkages),
    id = CaseNumber,
    EventType = "ratiolinkage", 
    RawProperties = bag_pack("UserProperties", bag_pack("EntityType", EntityType, "EntityAction", EntityAction, "EventDateTime", EventDateTime, "EntityID", CaseNumber)), 
    createdBy = "TouchProcessor",
    modifiedBy = "TouchProcessor",
    createdOn = currentTime,
    modifiedOn = currentTime, 
    SdpInternal = bag_pack("CornerStoneServiceBusEnqueueTime", now()) 
| project-away Linkages,EntityType,EventDateTime,EntityAction
| project CaseNumber, Value= tostring(pack_all())
| project CaseNumber, Content = replace(@"\r\n|\n", "", tostring(Value))
| project Key=CaseNumber,Value=tostring(Content) 
}

.create-or-alter function with (folder = "ADF", docstring = "Get RatioRecords for CaseNumbers", skipvalidation = "true") GetRatioLinkageChangeFeed(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
let skiprow=batch*index;
let currentTime=now();
let casenumbers=ChangeFeedPipelineRatioLinkage  |summarize arg_max(ExecutionTime,*) by CaseNumber|order by EventDateTime asc| extend rownum=row_number()|where rownum >skiprow|take batch | distinct CaseNumber;
let csinput= toscalar(casenumbers
    | summarize CaseNumberlist=iff(array_length(make_list(CaseNumber))==0,dynamic(['0']),make_list(CaseNumber))
    //| extend result=bag_pack('updatesince', datetime("2001-01-01"), 'CaseNumberlist', CaseNumberlist)
    | extend result=bag_pack('CaseNumberlist', CaseNumberlist)
    | project result);
cluster('ratiorepwus3prod.westus3').database('ratiodata').Generate_SR_History(csinput)|extend Source='RATIO'
| extend sortfield=strcat(Processtimestamp, '-', Action)
| order by sortfield asc
| project-away sortfield
| extend DataBoundary=iff(tolower(IcMCloud) contains "eu", "EU", "PUBLIC")
| summarize
    Linkages = make_list
               ( 
                   bag_pack("Number", tostring(IncidentId), "Type", "Automatic", "Source", Source, "DataBoundary", DataBoundary, "IncidentIdCloud", strcat(IncidentId, "-", DataBoundary), "LinkedDateTime", Processtimestamp, "Action", iff(Action == "Add", "Linked", "Removed"), "Reason", "")
               ),
    EntityType  = any("ratiolinkage"), 
    EntityAction = any("Create"), 
    EventDateTime= currentTime
    by CaseNumber 
| extend
    Content = bag_pack("CaseNumber", CaseNumber, "Linkages", Linkages),
    id = CaseNumber,
    EventType = "ratiolinkage", 
    RawProperties = bag_pack("UserProperties", bag_pack("EntityType", EntityType, "EntityAction", EntityAction, "EventDateTime", EventDateTime, "EntityID", CaseNumber)), 
    createdBy = "TouchProcessor",
    modifiedBy = "TouchProcessor",
    createdOn = currentTime,
    modifiedOn = currentTime, 
    SdpInternal = bag_pack("CornerStoneServiceBusEnqueueTime", now()) 
| project-away Linkages,EntityType,EventDateTime,EntityAction
| project CaseNumber, Value= tostring(pack_all())
| project CaseNumber, Content = replace(@"\r\n|\n", "", tostring(Value))
| project Key=CaseNumber,Value=tostring(Content) 
}

.create-or-alter function with (folder = "ADF", docstring = "Get Missing Ratio Linkage ", skipvalidation = "true") GetSrSnapshotMissingEvent(executionTime:datetime) {
SrSnapshotView |where EventDateTime between (todatetime("2001-01-01").. executionTime) and isnotempty(State)  and iff(isempty(LinkedICMs),"[]",LinkedICMs) !="[]" 
|order by ModifiedOn asc 
| project CaseNumber,ModifiedOn,executionTime
}

.create-or-alter function with (folder = "ADF", docstring = "Get Missing Ratio Linkage ", skipvalidation = "true") GetIcmL2AggregateMissingEvent(executionTime:datetime) {
IcmL2AggregateView 
|where EventCreatedTime between (todatetime("2001-01-01").. executionTime) 
|extend IncidentIdCloud= iff(isempty(IncidentIdCloud),strcat(IncidentId,"-PUBLIC"),IncidentIdCloud ) 
|extend DataBoundary= iff(isempty(DataBoundary),"PUBLIC",DataBoundary ) 
|order by EventCreatedTime asc 
| project IncidentIdCloud,EventCreatedTime,executionTime
}

.create-or-alter function with (folder = "ADF", docstring = "Get Missing Ratio Linkage ", skipvalidation = "true") GetIncidentLinkageMissingEvent(executionTime:datetime) {
IncidentLinkageView 
| where LinkSource=="RATIO"
|mv-expand Aggregate_Add
| extend IncidentIdCloud=tostring(Aggregate_Add.IncidentIdCloud) 
| extend Number=split(IncidentIdCloud, "-")[0] 
|extend isint=toint(Number) 
| where isempty(isint) 
|distinct CaseNumber,EventDateTime 
| where EventDateTime between (todatetime("2001-01-01").. todatetime(executionTime)) 
|order by EventDateTime asc 
| project CaseNumber,EventDateTime,executionTime
}

.create-or-alter function with (folder = "ADF", docstring = "Get SRChange Change Feed Count", skipvalidation = "true") GetSRChangeChangeFeedCount(startTime:datetime, endtime:datetime, batch:int, index:int) {
let skiprow=batch*index;
let latestSrs=ChangeFeedPipelineSRChange|summarize arg_max(ExecutionTime,*) by CaseNumber;
latestSrs|order by ModifiedOn asc| extend rownum=row_number()|where rownum >skiprow|take batch| count
}

.create-or-alter function with (folder = "ADF", docstring = "Get Missing SRChange Linkage ", skipvalidation = "true") GetSrChangeMissingEvent(executionTime:datetime) {
let srChangeLinkages=materialize(SRChange //IMPORTANT do join in srsnapview compartio icm and type to case number linkedICM if equal then do
| where ChangeProperties contains "LinkedICMs" and ICMLinkChange contains "Removed" |  project CaseNumber,ICMLinkChange,LinkedICMsPrevious,LinkedICMsCurrent,EventDateTime
| mv-expand ICMLinkChange
| extend RemovedNumber=tostring(ICMLinkChange.Number),RemovedType=tostring(ICMLinkChange.Type),RemovedSource=tostring(ICMLinkChange.Source),RemovedDataBoundary=tostring(ICMLinkChange.DataBoundary),RemovedIncidentIdCloud=tostring(ICMLinkChange.IncidentIdCloud),RemovedLinkedDateTime=tostring(ICMLinkChange.LinkedDateTime),RemovedAction=tostring(ICMLinkChange.Action)
| where RemovedAction =='Removed'
| extend Key=strcat(CaseNumber,'-',EventDateTime)
|project Key,RemovedNumber,RemovedType,RemovedSource,RemovedDataBoundary,RemovedIncidentIdCloud,RemovedLinkedDateTime,RemovedAction,LinkedICMsCurrent,LinkedICMsPrevious,CaseNumber
|where LinkedICMsCurrent contains RemovedNumber
|mv-expand LinkedICMsCurrent
| extend CurrentNumber=tostring(LinkedICMsCurrent.Number),CurrentType=tostring(LinkedICMsCurrent.Type),CurrentSource=tostring(LinkedICMsCurrent.Source),CurrentDataBoundary=tostring(LinkedICMsCurrent.DataBoundary),CurrentIncidentIdCloud=tostring(LinkedICMsCurrent.IncidentIdCloud),CurrentLinkedDateTime=tostring(LinkedICMsCurrent.LinkedDateTime)
|project-away LinkedICMsCurrent
|project Key,RemovedNumber,CurrentNumber,RemovedType,CurrentType,RemovedSource,CurrentSource,RemovedDataBoundary,CurrentDataBoundary,RemovedIncidentIdCloud,CurrentIncidentIdCloud,RemovedLinkedDateTime,CurrentLinkedDateTime,RemovedAction,LinkedICMsPrevious,CaseNumber
| where RemovedNumber ==CurrentNumber
| extend IsInvalidIcm=tolong(split(RemovedIncidentIdCloud,'-')[0])
|where isempty(IsInvalidIcm)
|order by Key| distinct CaseNumber );
srChangeLinkages|join kind=inner SrSnapshotView on CaseNumber | project CaseNumber,ModifiedOn,executionTime
|order by ModifiedOn asc
}

.create-or-alter function with (folder = "ADF", docstring = "Get SrChange Change Feed", skipvalidation = "true") GetSRChangeChangeFeed(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
// let startTime=datetime('2001-01-01');
// let endtime=now();
// let batch=50000;
// let correctionReason='';
// let index=0;
//
let currentTime=now();
let skiprow=batch*index;
let latestSrs=ChangeFeedPipelineSRChange|summarize arg_max(ExecutionTime,*) by CaseNumber|order by ModifiedOn asc| extend rownum=row_number()|where rownum >skiprow|take batch;
let srinfo=materialize(SrSnapshotView| where CaseNumber in (latestSrs| distinct  CaseNumber) and EventDateTime between (startTime.. endtime) |order by ModifiedOn asc 
| mv-expand  Customers
| extend Customer=bag_pack('CustomerName',tostring(Customers.CustomerName),'CustomerType',tostring(Customers.CustomerType))
| summarize Customers = make_list(Customer) by CaseNumber,SREventActionType,CritSit,State,Severity,Status,tostring(Product),DataBoundary,tostring(LinkedICMs),ModifiedOn,CreatedOn,ModifiedBy,CreatedBy,AzureSubscriptionId,SupportTopicTitle,SupportRegion,ResourceId,AgentId,Age,ResolvedDateTime,TenantId,CaseUri
|extend LinkedICMs=todynamic(LinkedICMs),Product=todynamic(Product)
| extend Snapshot=bag_pack('CaseNumber',CaseNumber,'SREventActionType',SREventActionType,'CritSit',CritSit,'State',State,'Severity',Severity,'Status',Status,
'Product',Product,'DataBoundary',DataBoundary,'LinkedICMs',LinkedICMs,'ModifiedOn',ModifiedOn,'CreatedOn',CreatedOn,'ModifiedBy',ModifiedBy,'CreatedBy',CreatedBy,'AzureSubscriptionId',AzureSubscriptionId,
'Customers',Customers,'SupportTopicTitle',SupportTopicTitle,'SupportRegion',SupportRegion,'ResourceId',ResourceId,'AgentId',AgentId,'Age',Age,'ResolvedDateTime',ResolvedDateTime,'TenantId',TenantId,
'CaseUri',CaseUri)
|mv-expand LinkedICMs
|extend SrNumber=tostring(LinkedICMs.Number),SrType=tostring(LinkedICMs.Type),SrSource=tostring(LinkedICMs.Source),SrDataBoundary=tostring(LinkedICMs.DataBoundary),SrIncidentIdCloud=tostring(LinkedICMs.IncidentIdCloud),SrLinkedDateTime=tostring(LinkedICMs.LinkedDateTime)
|project CaseNumber,SrNumber,SrType,SrSource,SrDataBoundary,SrIncidentIdCloud,SrLinkedDateTime,Snapshot=tostring(Snapshot)
| extend pk= bag_pack('Number',SrNumber,'Type',SrType,'Source',SrSource,'DataBoundary',SrDataBoundary,'IncidentIdCloud',SrIncidentIdCloud,'LinkedDateTime',SrLinkedDateTime)
| extend withaction= bag_pack('Number',SrNumber,'Type',SrType,'Source',SrSource,'DataBoundary',SrDataBoundary,'IncidentIdCloud',SrIncidentIdCloud,'LinkedDateTime',SrLinkedDateTime,'Action','Linked')
| summarize ICMLinkChange= make_set(withaction),CurrentValue=make_set(pk),PreviousValue=todynamic('[]') by CaseNumber,Snapshot
|extend Changes=bag_pack('LinkedICMs',bag_pack('PreviousValue',PreviousValue,'CurrentValue',CurrentValue,'ICMLinkChange',ICMLinkChange),'SDPICMLinkageCorrection',bag_pack('PreviousValue','','CurrentValue',correctionReason))
| extend ChangeProperties=todynamic('["LinkedICMs","SDPICMLinkageCorrection"]'),Snapshot=todynamic(Snapshot),EventDateTime=currentTime,SupportRequestNumber=CaseNumber,EventType='propertychange',SdpInternal=bag_pack('IngestorRecievedTime',currentTime,'CornerStoneServiceBusEnqueueTime',currentTime,'CornerStoneEventhubEnqueueTime',currentTime)
|extend ChangeEvent=bag_pack('SupportRequestNumber',SupportRequestNumber,'EventDateTime',EventDateTime,'EventType',EventType,'ChangeProperties',ChangeProperties,'Changes',Changes,'Snapshot',Snapshot,'SdpInternal',SdpInternal)
| project-away PreviousValue,CurrentValue,ICMLinkChange,Snapshot,Changes,ChangeProperties,EventDateTime,SupportRequestNumber,EventType,SdpInternal
);
srinfo| project Key=CaseNumber,Value=tostring(ChangeEvent)
}

.create-or-alter function with (folder = "ADF", docstring = "Get SrSnapshot Purged Linkage Correction Event", skipvalidation = "true") GetSrSnapshotPurgedLinkageCorrectionEvent(executionTime:datetime) {
let maxSnapshotId=toscalar(database('SAData').external_table("msdfm_deletedcasehistory_Public")| summarize max(snapshotid));
let purgeCases=database('SAData').external_table("msdfm_deletedcasehistory_Public")| where snapshotid == maxSnapshotId and strlen( msdfm_ticketnumber)==16 | extend prefix= toint(substring(msdfm_ticketnumber,0,2)) | where prefix >10
| extend PurgedOn=todatetime(modifiedon),CaseNumber=msdfm_ticketnumber
| where PurgedOn > datetime('2025-08-09 00:00:00.0000000')| project CaseNumber,PurgedOn
|join kind=inner SrSnapshotView on CaseNumber | where SREventActionType !='DeleteUpdated'| order by CaseNumber asc | project CaseNumber,PurgedOn;
SrSnapshotView|join kind=inner purgeCases on CaseNumber
| order by EventDateTime 
| project CaseNumber,ModifiedOn=PurgedOn,executionTime
| order by ModifiedOn asc
}'''
        continueOnErrors: true
        forceUpdateTag: forceUpdateTag
    }
    dependsOn:[kustoPICMRatioData4]
}



resource kustoPICMRatioData6 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = if(deployICMRatioData) {
    name: '${PclusterName}/ICMRatioData/PICMRatioData6'
    properties: {
        scriptContent: '''.create-or-alter function with (folder = "ADF", docstring = "Get SrSnapshot Change Feed", skipvalidation = "true") GetSrSnapshotPurgedLinkageCorrectionChangeFeed(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
// 
// let startTime=datetime('2001-01-01');
// let endtime=now();
// let batch=10;
// let correctionReason='restoring purge event linked incidents';
// let index=0;
let skiprow=batch*index;
let dayStringMapping = 
    union 
    (print FieldPath = '$.SDPICMLinkageCorrection', Value = correctionReason, DataType='string' ),
    (print FieldPath = '$.SdpServiceName', Value = 'CXOAI',  DataType='string' );
let MetaData= toscalar(dayStringMapping |extend bag= bag_pack("FieldPath",FieldPath,"Value",Value,"DataType",DataType)| summarize make_list(bag) | project list_bag);
let cs=PurgedLinkageCorrectionPipelineSRs|summarize arg_max(ExecutionTime,*) by CaseNumber|order by ModifiedOn asc| extend rownum=row_number()|where rownum >skiprow|take batch | project PurgedCaseNumber=CaseNumber;
let latestEvents=cs 
| join kind=inner SrSnapshot on $left.PurgedCaseNumber==$right.CaseNumber  |
where EventDateTime between (startTime.. endtime)  and isnotempty(State)
|summarize arg_max(EventDateTime,*) by CaseNumber;
// let bins =
//     SrSnapshot | where CaseNumber in(cs)
//     | extend hourBin = bin(EventDateTime, 6h)
//     | summarize binMaxEvent = max(EventDateTime) by CaseNumber, hourBin;
// let lastnopurgeRecords=bins| summarize latestBin = max(hourBin) by CaseNumber
// | join kind=inner (bins) on CaseNumber
// | where hourBin < latestBin
// | summarize arg_max(hourBin, binMaxEvent) by CaseNumber
// | project CaseNumber, EventDateTime = binMaxEvent;
// let snapshots=lastnopurgeRecords| join kind=inner  SrSnapshot on CaseNumber and EventDateTime
// |project  CaseNumber, LinkedICMs, LinkedIncidents, AdditionalIncidentIds,IncidentId,EventDateTime 
// | order by CaseNumber,EventDateTime ;
// let historicalLinkages=snapshots | extend AdditionalIncidentIds = array_concat(AdditionalIncidentIds, pack_array(IncidentId))
// | mv-apply element = AdditionalIncidentIds on (
//     summarize AdditionalIncidentIds = make_set(element)
//     )
// | mv-expand AdditionalIncidentIds
// | extend IncidentObj = pack(
//                 "IncidentId", tostring(AdditionalIncidentIds),
//                 "DataBoundary", "ICM-WW",
//                 "LinkSource", "DFM",
//                 "LinkCreatedOn", EventDateTime
//               )
// |summarize IncidentObjArray = make_list(IncidentObj) by CaseNumber,EventDateTime,tostring(LinkedICMs),tostring(LinkedIncidents)
// | extend LinkedICMs=todynamic(LinkedICMs),LinkedIncidents=todynamic(LinkedIncidents)
// |extend LinkedIncidents =iff(isempty(LinkedIncidents),IncidentObjArray ,LinkedIncidents) | project-away IncidentObjArray
// | mv-expand LinkedIncidents
// |extend IncidentId=tostring(LinkedIncidents.IncidentId),DataBoundary=tostring(LinkedIncidents.DataBoundary),
//          LinkSource=tostring(LinkedIncidents.LinkSource),LinkCreatedOn=tostring(LinkedIncidents.LinkCreatedOn)
// | where isnotempty(IncidentId)
// | summarize LinkedIncidents=make_set(LinkedIncidents) by CaseNumber;
// let removedIncidents=IncidentLinkage | where CaseNumber in(cs)| where EntityAction =='IncidentLinkDeleted'
// | extend removeObj=pack( "IncidentId",IncidentId ,
//                 "DataBoundary", DataBoundary,
//                 "LinkSource", LinkSource,
//                 "LinkCreatedOn", LinkCreatedOn)| summarize RemoveIncidents=make_list(removeObj) by CaseNumber ;
// let processeddata=historicalLinkages| join kind=leftouter  removedIncidents on CaseNumber| extend RemoveIncidents=todynamic(iff(isempty( RemoveIncidents),'[]',RemoveIncidents)) | project-away CaseNumber1;
// let restoredLinkedIncidents=processeddata | extend ToBeRemoved=RemoveIncidents
// | mv-apply RemoveIncidents on (
//    extend    rIncidentId   = tostring(RemoveIncidents["IncidentId"]),
//              rDataBoundary = tostring(RemoveIncidents["DataBoundary"]),
//              rLinkSource   = tostring(RemoveIncidents["LinkSource"]),
//              rLinkCreatedOn= tostring(RemoveIncidents["LinkCreatedOn"])
//     | extend rKey = strcat(rIncidentId, "-", rDataBoundary, "-", rLinkSource, "-", rLinkCreatedOn)
//     | summarize RemoveKeys = make_set(rKey)
// )
// | mv-expand LinkedIncidents
// | extend lIncidentId    = tostring(LinkedIncidents["IncidentId"]),
//          lDataBoundary  = tostring(LinkedIncidents["DataBoundary"]),
//          lLinkSource    = tostring(LinkedIncidents["LinkSource"]),
//          lLinkCreatedOn = tostring(LinkedIncidents["LinkCreatedOn"])
// | extend lKey = strcat(lIncidentId, "-", lDataBoundary, "-", lLinkSource, "-",lLinkCreatedOn)
// | where not(set_has_element(RemoveKeys, lKey))
// | extend obj = pack(
//     "IncidentId", lIncidentId,
//     "DataBoundary", lDataBoundary,
//     "LinkSource", lLinkSource,
//     "LinkCreatedOn", lLinkCreatedOn
// )
// | summarize CalculatedLinkedIncidents = make_list(obj) by CaseNumber;
let dynamicpurgedCaseNumbers=toscalar(cs|summarize make_list(PurgedCaseNumber));
let restoredLinkedIncidents=database('SAData').ExtractLinkedIncidents(dynamicpurgedCaseNumbers);  
latestEvents| join kind=leftouter restoredLinkedIncidents on CaseNumber //| extend LinkedIncidents=CalculatedLinkedIncidents 
 | project Content,RawProperties,CaseNumber,LinkedIncidents=LinkedIncidents1
| extend UserProperties=todynamic(extract_json("$.UserProperties",tostring(RawProperties)))
|extend UserProperties=bag_merge(bag_remove_keys(UserProperties,dynamic(["EventType"])),bag_pack("EventType","DeleteUpdated"))
|extend RawProperties=bag_merge(bag_remove_keys(RawProperties,dynamic(["UserProperties"])),bag_pack("UserProperties",UserProperties))
| extend Content=bag_merge(bag_remove_keys(Content,dynamic(["LinkedIncidents"])),bag_pack("LinkedIncidents",LinkedIncidents)) //remove
| extend Snapshot= bag_pack("Content",Content,"RawProperties",RawProperties)
| extend Value=bag_pack("Snapshot",Snapshot,"MetaData",MetaData,"Source","raw")
| project Key=CaseNumber,tostring(Value)
}

.create-or-alter function with (folder = "ADF", docstring = "Get SrSnapshot Purged Linkage Correction Change Feed Count", skipvalidation = "true") GetSrSnapshotPurgedLinkageCorrectionChangeFeedCount(startTime:datetime, endtime:datetime, batch:int, index:int) {
let skiprow=batch*index;
let latestSrs=PurgedLinkageCorrectionPipelineSRs|summarize arg_max(ExecutionTime,*) by CaseNumber;
latestSrs|order by ModifiedOn asc| extend rownum=row_number()|where rownum >skiprow|take batch| count
}

.create-or-alter function with (folder = "ADF", docstring = "Get RatioRecords for CaseNumbers", skipvalidation = "true") GetRatioLinkageChangeFeedLocal(startTime:datetime, endtime:datetime, batch:int, correctionReason:string, index:int) {
// let startTime=todatetime('2001-01-01');
// let endtime=now(); 
// let batch=100;
// let correctionReason='';
// let index=0;
let skiprow=batch*index;
let currentTime=now();
let casenumbers=ChangeFeedPipelineRatioLinkage  |summarize arg_max(ExecutionTime,*) by CaseNumber|order by EventDateTime asc| extend rownum=row_number()|where rownum >skiprow|take batch | distinct CaseNumber;
casenumbers 
| join kind=inner RatioLinkageView on CaseNumber  |
where EventDateTime between (startTime.. endtime) 
|order by ModifiedOn asc 
| extend UserProperties=todynamic(extract_json('$.RawProperties.UserProperties',tostring(Content)))
| extend UserProperties= bag_merge(bag_remove_keys(UserProperties,dynamic(["EventDateTime"])),bag_pack("EventDateTime",currentTime))
| extend RawProperties=todynamic(extract_json('$.RawProperties',tostring(Content)))
| extend RawProperties= bag_merge(bag_remove_keys(RawProperties,dynamic(["UserProperties"])),bag_pack("UserProperties",UserProperties))
| extend SdpInternal=bag_pack("IngestorRecievedTime",currentTime,"CornerStoneServiceBusEnqueueTime",currentTime,"CornerStoneEventhubEnqueueTime",currentTime)
| extend Content=bag_merge(bag_remove_keys(Content,dynamic(["RawProperties","SdpInternal","createdOn","modifiedOn"])),bag_pack("RawProperties",RawProperties,"SdpInternal",SdpInternal,"createdOn",currentTime,"modifiedOn",currentTime))
| project Key=CaseNumber,Value=tostring(Content) 
}

.alter table ReconciliationDataHistory policy update
```[{"IsEnabled": true,
    "Source": "ReconciliationData",
    "Query": "ReconciliationData",
    "IsTransactional": true,
    "PropagateIngestionProperties": true,
    "ManagedIdentity": null
  }]```

.create-or-alter  materialized-view  with ( folder='SRICM',docString='SrSnapshot pivoted by ICM Id',autoUpdateSchema=true) SrSnapshotByIcmNumberView on table SrSnapshot { SrSnapshot
    | mv-expand LinkedICMs
    | extend
        SupportRequestNumber = CaseNumber,
        ICMId               = toint(LinkedICMs.Number),
        Type                = tostring(LinkedICMs.Type),
        Source              = tostring(LinkedICMs.Source),
        DataBoundary        = tostring(LinkedICMs.DataBoundary),
        LinkedDateTime      = todatetime(LinkedICMs.LinkedDateTime),
        SRNumIcmId          = strcat(CaseNumber, '-', LinkedICMs.Number, '-', LinkedICMs.DataBoundary)
    | summarize arg_max(EventDateTime, *) 
        by SRNumIcmId }

.create-or-alter   materialized-view  with ( folder='SRICM',docString='SrSnapshot Latest Event',autoUpdateSchema=true) SrSnapshotView on table SrSnapshot { SrSnapshot
        | summarize arg_max(EventDateTime, *)
        by CaseNumber }

.create-or-alter materialized-view  with ( folder='SRICM',docString='IcmL2Aggregate Latest Event',autoUpdateSchema=true) IcmL2AggregateView on table IcmL2Aggregate { IcmL2Aggregate
        | summarize arg_max(EventCreatedTime, *)
        by IncidentIdCloud }

.create-or-alter   materialized-view  with ( folder='SRICM',docString='IncidentLinkage Latest Event',autoUpdateSchema=true) IncidentLinkageView on table IncidentLinkage { IncidentLinkage
        | summarize arg_max(EventDateTime, *)
        by CaseNumber }

.create-or-alter   materialized-view  with ( folder='SRICM',docString='Ratio Linkage Latest Event',autoUpdateSchema=true) RatioLinkageView on table RatioLinkage { RatioLinkage
        | summarize arg_max(EventDateTime, *)
        by CaseNumber }

.create-or-alter   materialized-view  with ( folder='SRICM',docString='SR Change Latest Event',autoUpdateSchema=true) SRChangeView on table SRChange { SRChange
        | summarize arg_max(EventDateTime, *)
        by CaseNumber }'''
        continueOnErrors: true
        forceUpdateTag: forceUpdateTag
    }
    dependsOn:[kustoPICMRatioData5]
}




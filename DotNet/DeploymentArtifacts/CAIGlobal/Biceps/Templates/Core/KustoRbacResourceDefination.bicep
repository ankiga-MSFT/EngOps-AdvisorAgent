param PclusterName string
param PkustoDatabaseName string
param Pprincipleid string
param Ptenantid string
param Prole string //Admin, viewer
param PprincipleType string //App, User, Group,

resource kustoDatabaseRoleAssignment 'Microsoft.Kusto/clusters/databases/principalAssignments@2023-08-15' = {
  name: '${PclusterName}/${PkustoDatabaseName}/${Pprincipleid}'
  properties: {
    principalId: Pprincipleid
    principalType: PprincipleType
    role: Prole
    tenantId: Ptenantid
  }
  }








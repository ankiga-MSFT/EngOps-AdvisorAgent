param keyVaultAppId string
param keyvaultSecureId string
param KeyvaultName string
param keyVaultPrivateIssuer string
param keyvaultProvider string
param cnSubjectName string
param certificateName string
param deployCertificates string

output KeyvaultPublicAccessChanges string='deployCertificates: ${deployCertificates}, cnSubjectName: ${cnSubjectName}, certificateName: ${certificateName}, keyvaultSecureId: ${keyvaultSecureId}, keyVaultAppId: ${keyVaultAppId}, KeyvaultName: ${KeyvaultName}, keyVaultPrivateIssuer: ${keyVaultPrivateIssuer}, keyvaultProviderName: ${keyvaultProvider} '

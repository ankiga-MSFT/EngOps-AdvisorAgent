param PkeyVaultName string
param PcertificateName string
param PdeployCertificate string
var deployCertificate = bool(PdeployCertificate)

resource keyVault 'Microsoft.KeyVault/vaults@2021-04-01-preview' existing= if(deployCertificate) {
  name: PkeyVaultName
}

resource certificate 'Microsoft.KeyVault/vaults/certificates@2021-06-01-preview' = if(deployCertificate) {
  parent: keyVault
  name: PcertificateName
  location: resourceGroup().location
  properties: {
    certificatePolicy: {
      keyProperties: {
        exportable: true
        keyType: 'RSA'
        keySize: 2048
        reuseKey: false
      }
      contentType: 'application/x-pkcs12'
      subject: 'CN=example.com'
      issuerParameters: {
        name: 'Self'
      }
      x509CertificateProperties: {
        validityInMonths: 12
        subjectAlternativeNames: {
          dnsNames: [
            'example.com'
            'www.example.com'
          ]
        }
      }
      secretProperties: {
        contentType: 'application/x-pkcs12'
      }
      lifetimeActions: [
        {
          trigger: {
            daysBeforeExpiry: 90
          }
          action: {
            actionType: 'AutoRenew'
          }
        }
      ]
    }
  }
}


// resource  certificate  'Microsoft.KeyVault/vaults/certificates@2021-06-01-preview' = {
//     parent: keyVault
//     name: 'my-certificate'
//     properties: {
//         certificatePolicy: {
//         issuerParameters: {
//         name: 'Unknown'
//         }
//     keyProperties: {
//         keyType: 'RSA'
//         keySize: 2048
//         reuseKey: false
//         }
//     secretProperties: {
//         contentType: 'application/x-pkcs12'
//         }
//         x509CertificateProperties: {
//             subject: 'CN=my-certificate'
//             validityInMonths: 12
//             }
//         }
//     }
// }

output keyVaultUri string = keyVault.properties.vaultUri
output certificateUri string = '${keyVault.properties.vaultUri}/certificates/${certificate.name}'

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Provider.Interfaces;
using System.Security.Cryptography.X509Certificates;

namespace Provider
{


    public class AzureKeyVaultProvider : IAzureKeyVaultProvider
    {
        private readonly SecretClient _secretClient;

        public AzureKeyVaultProvider(string keyVaultUri)
        {
#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var credential = new ManagedIdentityCredential();
#endif
            _secretClient = new SecretClient(new Uri(keyVaultUri), credential);
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            var secret = await _secretClient.GetSecretAsync(secretName);
            return secret.Value.Value;
        }

        public async Task SetSecretAsync(string secretName, string secretValue)
        {
            await _secretClient.SetSecretAsync(secretName, secretValue);
        }

        public async Task<X509Certificate2> GetCertificateAsync(string certificateName)
        {
            var secret = await _secretClient.GetSecretAsync(certificateName);
            var certBytes = Convert.FromBase64String(secret.Value.Value);

            // Create X509Certificate2 from the PFX bytes
            return new X509Certificate2(certBytes);
        }

        public async Task DeleteSecretAsync(string secretName)
        {
            await _secretClient.StartDeleteSecretAsync(secretName);
        }

        public async Task<string> GetSecretVersionAsync(string secretName, string version)
        {
            var secret = await _secretClient.GetSecretAsync(secretName, version);
            return secret.Value.Value;
        }

        public async Task RestoreDeletedSecretAsync(string secretName)
        {
            await _secretClient.StartRecoverDeletedSecretAsync(secretName);
        }

        public async Task<List<string>> ListSecretsAsync()
        {
            var secretProperties = _secretClient.GetPropertiesOfSecretsAsync();
            var secrets = new List<string>();

            await foreach (var secret in secretProperties)
            {
                secrets.Add(secret.Name);
            }

            return secrets;
        }
    }

}

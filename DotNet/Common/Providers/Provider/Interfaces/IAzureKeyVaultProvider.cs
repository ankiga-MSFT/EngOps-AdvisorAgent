using System.Security.Cryptography.X509Certificates;

namespace Provider.Interfaces
{
    public interface IAzureKeyVaultProvider
    {
        Task DeleteSecretAsync(string secretName);
        Task<string> GetSecretAsync(string secretName);
        Task<X509Certificate2> GetCertificateAsync(string certificateName);
        Task<string> GetSecretVersionAsync(string secretName, string version);
        Task<List<string>> ListSecretsAsync();
        Task RestoreDeletedSecretAsync(string secretName);
        Task SetSecretAsync(string secretName, string secretValue);
    }
}
using System.Security.Cryptography.X509Certificates;

namespace Provider.Model;

/// <summary>
/// Configuration for acquiring tokens via the On-Behalf-Of (OBO) flow with certificate assertion.
/// </summary>
public class TokenAcquisitionConfig
{
    public string AppClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new List<string>();
    public string ClientCertificateName { get; set; } = string.Empty;
    public X509Certificate2? Certificate { get; set; }
}

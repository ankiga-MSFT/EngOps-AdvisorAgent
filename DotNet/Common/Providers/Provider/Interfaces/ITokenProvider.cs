using Provider.Model;

namespace Provider.Interfaces;

/// <summary>
/// Acquires JWT tokens using the On-Behalf-Of (OBO) flow with certificate assertion.
/// Used by Insights API calls that require delegated user context.
/// </summary>
public interface ITokenProvider
{
    Task<string> GetJwtTokenOnBehalfOfUserWithCertificateAssertion(
        TokenAcquisitionConfig tokenAcquisitionConfig,
        string userToken);
}

using InfraService.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Provider.Interfaces;
using Provider.Model;

namespace Provider;

/// <summary>
/// Acquires JWT tokens on behalf of a user using certificate-based client assertion (OBO flow).
/// Used for calling Insights APIs that require delegated user context.
/// </summary>
public class TokenProvider : ITokenProvider
{
    private readonly ILogger<TokenProvider> logger;

    public TokenProvider(ILogger<TokenProvider> logger)
    {
        this.logger = Requires.IsNotNull(logger, nameof(logger));
    }

    public async Task<string> GetJwtTokenOnBehalfOfUserWithCertificateAssertion(
        TokenAcquisitionConfig tokenAcquisitionConfig,
        string userToken)
    {
        Requires.IsNotNullOrEmpty(tokenAcquisitionConfig.AppClientId, nameof(tokenAcquisitionConfig.AppClientId));
        Requires.IsNotNullOrEmpty(tokenAcquisitionConfig.TenantId, nameof(tokenAcquisitionConfig.TenantId));
        Requires.IsNotNullOrEmpty(userToken, nameof(userToken));
        Requires.IsNotNullOrEmpty(tokenAcquisitionConfig.Scopes, nameof(tokenAcquisitionConfig.Scopes));

        if (tokenAcquisitionConfig.Certificate is null)
            throw new ArgumentNullException(nameof(tokenAcquisitionConfig.Certificate));

        userToken = userToken.Replace("Bearer ", "");

        if (string.IsNullOrEmpty(userToken))
            throw new ArgumentException("User token is null or empty after removing 'Bearer ' prefix.", nameof(userToken));

        try
        {
            var clientApplication = ConfidentialClientApplicationBuilder
                .Create(tokenAcquisitionConfig.AppClientId)
                .WithTenantId(tokenAcquisitionConfig.TenantId)
                .WithCertificate(tokenAcquisitionConfig.Certificate)
                .Build();

            var userAssertion = new UserAssertion(userToken);

            var authenticationResult = await clientApplication
                .AcquireTokenOnBehalfOf(tokenAcquisitionConfig.Scopes, userAssertion)
                .WithSendX5C(true)
                .ExecuteAsync();

            logger.LogInformation("Successfully acquired token on behalf of user.");

            return authenticationResult.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetJwtTokenOnBehalfOfUser threw an exception: {Message}", ex.Message);
            throw;
        }
    }
}

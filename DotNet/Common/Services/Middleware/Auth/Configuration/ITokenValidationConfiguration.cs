using Microsoft.IdentityModel.S2S.Configuration;

namespace Middleware.Auth.Configuration
{
    public interface ITokenValidationConfiguration
    {
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Azure Active Directory authentication options.
        /// </summary>
        public AadAuthenticationOptions AadAuthenticationOptions { get; set; }

        /// <inheritdoc />
        /// 
        public string? HealthCheckFunctionNames { get; set; } // HealthCheckHttpTrigger,Function1

        //public List<string> AllowedScopes { get; set; }
    }
}

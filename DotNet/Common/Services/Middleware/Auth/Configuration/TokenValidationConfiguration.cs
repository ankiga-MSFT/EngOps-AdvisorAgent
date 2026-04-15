using Microsoft.IdentityModel.S2S.Configuration;

namespace Middleware.Auth.Configuration
{
#pragma warning disable CS8618
    public class TokenValidationConfiguration : ITokenValidationConfiguration
    {

        public string Name { get; set; }

        public AadAuthenticationOptions AadAuthenticationOptions { get; set; }

        public string? HealthCheckFunctionNames { get; set; } // HealthCheckHttpTrigger,Function1

        //public List<string> AllowedScopes { get; set; }

    }
}


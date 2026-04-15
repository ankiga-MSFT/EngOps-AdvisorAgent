namespace CXOAI.SkillFramework;

/// <summary>
/// Holds per-request authentication context. Registered as <b>Scoped</b> so each
/// function invocation gets its own isolated instance — DI guarantees the Activity,
/// StepService, and Tools all share the same instance within a single request.
/// </summary>
public interface IUserAuthContext
{
    /// <summary>Raw Bearer access token from the Authorization header.</summary>
    string? AccessToken { get; set; }

    /// <summary>AAD Object ID (oid claim) of the authenticated user.</summary>
    string? UserObjectId { get; set; }

    /// <summary>Display name of the authenticated user.</summary>
    string? UserName { get; set; }

    /// <summary>User Principal Name (UPN) of the authenticated user.</summary>
    string? UserPrincipalName { get; set; }
}

/// <inheritdoc />
public class UserAuthContext : IUserAuthContext
{
    public string? AccessToken { get; set; }
    public string? UserObjectId { get; set; }
    public string? UserName { get; set; }
    public string? UserPrincipalName { get; set; }
}

using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Links external identity provider accounts to internal users.
/// Supports OAuth2/OIDC (Google, Microsoft) and SAML2 federated logins.
/// </summary>
public class UserExternalLogin : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = string.Empty; // "Google", "Microsoft", "SAML2"
    public string ProviderKey { get; private set; } = string.Empty; // External user ID
    public string? Email { get; private set; }
    public string? DisplayName { get; private set; }
    public string? ProfilePictureUrl { get; private set; }
    public string? ProviderData { get; private set; } // JSON: additional claims from IdP
    public DateTime? LastLoginAt { get; private set; }

    private UserExternalLogin() { }

    public static UserExternalLogin Create(
        Guid userId,
        string provider,
        string providerKey,
        string? email = null,
        string? displayName = null)
    {
        return new UserExternalLogin
        {
            UserId = userId,
            Provider = provider,
            ProviderKey = providerKey,
            Email = email,
            DisplayName = displayName
        };
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void UpdateProfile(string? email, string? displayName, string? profilePictureUrl)
    {
        Email = email;
        DisplayName = displayName;
        ProfilePictureUrl = profilePictureUrl;
        SetUpdated();
    }
}

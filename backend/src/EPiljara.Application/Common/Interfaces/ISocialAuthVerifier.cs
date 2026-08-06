namespace EPiljara.Application.Common.Interfaces;

/// <summary>Identity extracted from a verified social login token.</summary>
public record ExternalUser(string Email, string Name);

/// <summary>
/// Verifies a provider token (Google ID token / Facebook access token) and returns the
/// authenticated user's email and name. Implementations throw <c>UnauthorizedException</c>
/// when the token is invalid or the provider isn't configured.
/// </summary>
public interface ISocialAuthVerifier
{
    Task<ExternalUser> VerifyGoogleAsync(string idToken, CancellationToken cancellationToken = default);
    Task<ExternalUser> VerifyFacebookAsync(string accessToken, CancellationToken cancellationToken = default);
}

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Verifies social login tokens against the providers. Google ID tokens are validated
/// (signature + audience + verified email); Facebook access tokens are checked with the
/// Graph API's debug_token (proving the token was issued for THIS app) before reading the
/// profile. Client ids/secrets come from the <c>Auth</c> config section.
/// </summary>
public class SocialAuthVerifier(IConfiguration config) : ISocialAuthVerifier
{
    private static readonly HttpClient Http = new();

    public async Task<ExternalUser> VerifyGoogleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = config["Auth:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new UnauthorizedException("Google prijava nije konfigurisana.", "google_not_configured");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
        }
        catch
        {
            throw new UnauthorizedException("Google token nije valjan.", "invalid_google_token");
        }

        // Only trust a verified email — otherwise a Google account with an unverified,
        // attacker-controlled email could hijack an existing account on link/create.
        if (payload.EmailVerified != true || string.IsNullOrWhiteSpace(payload.Email))
            throw new UnauthorizedException("Google email nije verifikovan.", "google_email_unverified");

        return new ExternalUser(payload.Email, payload.Name ?? payload.Email);
    }

    public async Task<ExternalUser> VerifyFacebookAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var appId = config["Auth:Facebook:AppId"];
        var appSecret = config["Auth:Facebook:AppSecret"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            throw new UnauthorizedException("Facebook prijava nije konfigurisana.", "facebook_not_configured");

        // 1) Prove the token is valid AND was issued for our app (blocks tokens minted for other apps).
        FacebookDebug? debug;
        try
        {
            var appToken = $"{appId}|{appSecret}";
            var url =
                "https://graph.facebook.com/debug_token?input_token="
                + Uri.EscapeDataString(accessToken)
                + "&access_token="
                + Uri.EscapeDataString(appToken);
            var wrapper = await Http.GetFromJsonAsync<FacebookDebugWrapper>(url, cancellationToken);
            debug = wrapper?.Data;
        }
        catch
        {
            throw new UnauthorizedException("Facebook token nije valjan.", "invalid_facebook_token");
        }

        if (debug is null || debug.IsValid != true || debug.AppId != appId)
            throw new UnauthorizedException("Facebook token nije za ovu aplikaciju.", "facebook_token_mismatch");

        // 2) Read the profile.
        FacebookMe? me;
        try
        {
            var url =
                "https://graph.facebook.com/me?fields=id,name,email&access_token="
                + Uri.EscapeDataString(accessToken);
            me = await Http.GetFromJsonAsync<FacebookMe>(url, cancellationToken);
        }
        catch
        {
            throw new UnauthorizedException("Facebook token nije valjan.", "invalid_facebook_token");
        }

        if (me is null || string.IsNullOrWhiteSpace(me.Email))
            throw new UnauthorizedException("Facebook nalog nema email adresu.", "facebook_no_email");

        return new ExternalUser(me.Email, me.Name ?? me.Email);
    }

    private sealed record FacebookDebugWrapper(FacebookDebug? Data);

    private sealed record FacebookDebug(
        [property: JsonPropertyName("app_id")] string? AppId,
        [property: JsonPropertyName("is_valid")] bool? IsValid);

    private sealed record FacebookMe(string? Id, string? Name, string? Email);
}

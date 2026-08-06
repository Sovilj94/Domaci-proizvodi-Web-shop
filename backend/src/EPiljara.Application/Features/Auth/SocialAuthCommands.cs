using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Auth;

/* ---------- Social login (Google / Facebook) ---------- */

public record GoogleLoginCommand(string IdToken) : IRequest<AuthResultDto>;

public record FacebookLoginCommand(string AccessToken) : IRequest<AuthResultDto>;

public class GoogleLoginValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginValidator() =>
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("Nedostaje Google token.");
}

public class FacebookLoginValidator : AbstractValidator<FacebookLoginCommand>
{
    public FacebookLoginValidator() =>
        RuleFor(x => x.AccessToken).NotEmpty().WithMessage("Nedostaje Facebook token.");
}

public class GoogleLoginHandler(IAppDbContext db, ISocialAuthVerifier verifier, IJwtTokenService jwt)
    : IRequestHandler<GoogleLoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(GoogleLoginCommand cmd, CancellationToken ct)
    {
        var ext = await verifier.VerifyGoogleAsync(cmd.IdToken, ct);
        return await SocialAuth.FindOrCreateAsync(db, jwt, ext, ct);
    }
}

public class FacebookLoginHandler(IAppDbContext db, ISocialAuthVerifier verifier, IJwtTokenService jwt)
    : IRequestHandler<FacebookLoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(FacebookLoginCommand cmd, CancellationToken ct)
    {
        var ext = await verifier.VerifyFacebookAsync(cmd.AccessToken, ct);
        return await SocialAuth.FindOrCreateAsync(db, jwt, ext, ct);
    }
}

/// <summary>Shared: log a verified external user in, creating a passwordless account on first use.</summary>
internal static class SocialAuth
{
    public static async Task<AuthResultDto> FindOrCreateAsync(
        IAppDbContext db, IJwtTokenService jwt, ExternalUser ext, CancellationToken ct)
    {
        var email = ext.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new User
            {
                Name = string.IsNullOrWhiteSpace(ext.Name) ? email : ext.Name.Trim(),
                Email = email,
                PasswordHash = string.Empty, // social account — password login is impossible by design
                Role = UserRole.Customer,
                EmailConfirmed = true, // provider already verified the email
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        return new AuthResultDto { Token = jwt.CreateToken(user), User = user.ToAuthDto() };
    }
}

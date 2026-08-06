using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Auth;

/* ---------- Confirm email (via the emailed link) ---------- */

public record ConfirmEmailCommand(string Token) : IRequest<AuthResultDto>;

public class ConfirmEmailHandler(IAppDbContext db, IJwtTokenService jwt)
    : IRequestHandler<ConfirmEmailCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(ConfirmEmailCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Token))
            throw new DomainException("Nedostaje token za potvrdu.", "invalid_token");

        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == cmd.Token, ct)
            ?? throw new NotFoundException("Link za potvrdu nije valjan ili je već iskorišten.");

        user.EmailConfirmed = true;
        user.EmailConfirmationToken = null;
        await db.SaveChangesAsync(ct);

        // Confirming logs the user straight in.
        return new AuthResultDto { Token = jwt.CreateToken(user), User = user.ToAuthDto() };
    }
}

/* ---------- Resend the confirmation email ---------- */

public record ResendConfirmationCommand(string Email) : IRequest<Unit>;

public class ResendConfirmationHandler(IAppDbContext db, IEmailService email)
    : IRequestHandler<ResendConfirmationCommand, Unit>
{
    public async Task<Unit> Handle(ResendConfirmationCommand cmd, CancellationToken ct)
    {
        var normalized = cmd.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);

        // Only act for a real, still-unconfirmed account — but always report success so the
        // endpoint never reveals whether an email is registered.
        if (user is not null && !user.EmailConfirmed)
        {
            var token = Guid.NewGuid().ToString("N");
            user.EmailConfirmationToken = token;
            await db.SaveChangesAsync(ct);
            try { await email.SendEmailConfirmationAsync(user, token, ct); }
            catch { /* never surface transport errors here */ }
        }

        return Unit.Value;
    }
}

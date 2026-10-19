using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Auth;

/* ---------- Register ---------- */

public record RegisterCommand(
    string Name,
    string Email,
    string Phone,
    string Password,
    string? Address,
    Guid? DeliveryZoneId,
    string? PreporukaKod = null) : IRequest<RegisterResultDto>;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Ime je obavezno.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Unesite ispravan email.");
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Telefon je obavezan.");
        RuleFor(x => x.Password).MinimumLength(6).WithMessage("Lozinka mora imati najmanje 6 znakova.");
        RuleFor(x => (x.PreporukaKod ?? "").Trim()).MaximumLength(8)
            .OverridePropertyName("preporukaKod")
            .When(x => x.PreporukaKod is not null)
            .WithMessage("Kod preporuke je predugačak.");
    }
}

/// <summary>Jedino mjesto koje kod preporuke pretvara u preporučioca — registracija i društvena prijava (B5).</summary>
internal static class Preporuka
{
    /// <summary>Nepoznat ili prazan kod daje null — preporuka je dodatak, ne uslov (O10).</summary>
    public static async Task<Guid?> NadjiPreporuciocaAsync(IAppDbContext db, string? kod, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kod)) return null;
        var k = kod.Trim().ToUpperInvariant();
        return await db.Users.Where(u => u.PreporukaKod == k).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
    }
}

public class RegisterHandler(IAppDbContext db, IPasswordHasher hasher, IEmailService email, ICurrentUser current)
    : IRequestHandler<RegisterCommand, RegisterResultDto>
{
    public async Task<RegisterResultDto> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
            throw new ConflictException("Nalog sa ovim emailom već postoji.", "email_taken");

        // Preporuka je dodatak, ne uslov: nepoznat ili pogrešno prepisan kod ne smije spriječiti
        // registraciju, pa se tiho ignoriše (O10). Poeni se dodjeljuju tek prvom isporukom.
        var preporucioId = await Preporuka.NadjiPreporuciocaAsync(db, cmd.PreporukaKod, ct);

        var token = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Name = cmd.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = hasher.Hash(cmd.Password),
            Phone = cmd.Phone,
            Address = cmd.Address,
            DeliveryZoneId = cmd.DeliveryZoneId,
            Role = UserRole.Customer,
            EmailConfirmed = false,
            EmailConfirmationToken = token,
            PreporucioId = preporucioId,
            Pismo = current.Pismo,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        // Registration no longer logs in — the user must confirm their email first.
        try { await email.SendEmailConfirmationAsync(user, token, ct); }
        catch { /* email failure must not block registration */ }

        return new RegisterResultDto { Email = user.Email };
    }
}

/* ---------- Login ---------- */

public record LoginCommand(string Email, string Password) : IRequest<AuthResultDto>;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email je obavezan.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Lozinka je obavezna.");
    }
}

public class LoginHandler(IAppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
    : IRequestHandler<LoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !hasher.Verify(cmd.Password, user.PasswordHash))
            throw new UnauthorizedException("Pogrešan email ili lozinka.", "invalid_credentials");

        if (!user.EmailConfirmed)
            throw new UnauthorizedException("Potvrdite email adresu prije prijave.", "email_not_confirmed");

        return new AuthResultDto { Token = jwt.CreateToken(user), User = user.ToAuthDto() };
    }
}

/* ---------- Me ---------- */

public record GetMeQuery : IRequest<AuthUserDto>;

public class GetMeHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetMeQuery, AuthUserDto>
{
    public async Task<AuthUserDto> Handle(GetMeQuery q, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct)
            ?? throw new UnauthorizedException("Niste prijavljeni.");
        return user.ToAuthDto();
    }
}

using System.Security.Cryptography;
using System.Text;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Auth;

public static class ResetLozinkePravila
{
    public static readonly TimeSpan Vazi = TimeSpan.FromHours(1);
    /// <summary>Novi link najranije poslije ovoliko — dugme „Pošalji" ne smije zatrpati sanduče.</summary>
    public static readonly TimeSpan Razmak = TimeSpan.FromMinutes(2);

    public static string Hes(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string NoviToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/* ---------- Zaboravljena lozinka: mejl sa linkom ---------- */

/// <summary>Odgovor je uvijek isti — endpoint ne otkriva da li je adresa registrovana.</summary>
public record ZaboravljenaLozinkaCommand(string Email) : IRequest<Unit>;

public class ZaboravljenaLozinkaHandler(IAppDbContext db, IEmailService email) : IRequestHandler<ZaboravljenaLozinkaCommand, Unit>
{
    public async Task<Unit> Handle(ZaboravljenaLozinkaCommand c, CancellationToken ct)
    {
        var adresa = (c.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (adresa.Length == 0) return Unit.Value;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == adresa, ct);
        if (user is null) return Unit.Value;

        var sada = DateTime.UtcNow;
        if (user.ResetLozinkeIstice is DateTime istice && istice - ResetLozinkePravila.Vazi + ResetLozinkePravila.Razmak > sada)
            return Unit.Value;

        var token = ResetLozinkePravila.NoviToken();
        user.ResetLozinkeHash = ResetLozinkePravila.Hes(token);
        user.ResetLozinkeIstice = sada + ResetLozinkePravila.Vazi;
        await db.SaveChangesAsync(ct);
        await email.SendResetLozinkeAsync(user, token, ct);
        return Unit.Value;
    }
}

/* ---------- Nova lozinka iz linka ---------- */

public record NovaLozinkaCommand(string Token, string Lozinka) : IRequest<AuthResultDto>;

public class NovaLozinkaValidator : AbstractValidator<NovaLozinkaCommand>
{
    public NovaLozinkaValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Link nije potpun.");
        RuleFor(x => x.Lozinka).MinimumLength(6).WithMessage("Lozinka mora imati najmanje 6 znakova.");
    }
}

/// <summary>Jednokratan link: postavlja lozinku, briše token, potvrđuje adresu (link je stigao na nju) i prijavljuje.</summary>
public class NovaLozinkaHandler(IAppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
    : IRequestHandler<NovaLozinkaCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(NovaLozinkaCommand c, CancellationToken ct)
    {
        var hes = ResetLozinkePravila.Hes(c.Token.Trim());
        var sada = DateTime.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.ResetLozinkeHash == hes, ct);
        if (user is null || user.ResetLozinkeIstice is not DateTime istice || istice < sada)
            throw new DomainException("Link za novu lozinku je istekao ili je već iskorišten. Zatraži novi.", "reset_istekao");

        user.PasswordHash = hasher.Hash(c.Lozinka);
        user.ResetLozinkeHash = null;
        user.ResetLozinkeIstice = null;
        user.EmailConfirmed = true;
        user.EmailConfirmationToken = null;
        await db.SaveChangesAsync(ct);
        return new AuthResultDto { Token = jwt.CreateToken(user), User = user.ToAuthDto() };
    }
}

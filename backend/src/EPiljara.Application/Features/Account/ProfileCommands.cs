using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Account;

/// <summary>
/// Kupac mijenja ono malo što smije o sebi: ime i telefon. E-mail je korisničko
/// ime i potvrđena adresa, lozinka ne postoji za one koji ulaze preko Googlea —
/// oboje je zaseban posao. Vraća isti AuthUserDto koji auth/me daje, da klijent
/// osvježi svoj kontekst iz odgovora umjesto drugim pozivom.
/// </summary>
public record AzurirajProfilCommand(string Ime, string Telefon) : IRequest<AuthUserDto>;

public class AzurirajProfilValidator : AbstractValidator<AzurirajProfilCommand>
{
    /// <summary>Prati AppDbContext: User.Name je varchar(160).</summary>
    public const int MaxIme = 160;

    /// <summary>Kolona nema granicu; validator je uvodi da predug unos ne stigne do baze.</summary>
    public const int MaxTelefon = 40;

    public AzurirajProfilValidator()
    {
        RuleFor(x => x.Ime)
            .NotEmpty().WithMessage("Ime je obavezno.")
            .MaximumLength(MaxIme).WithMessage($"Ime može imati najviše {MaxIme} znakova.");

        RuleFor(x => x.Telefon)
            .NotEmpty().WithMessage("Telefon je obavezan.")
            .MaximumLength(MaxTelefon).WithMessage($"Telefon može imati najviše {MaxTelefon} znakova.");
    }
}

public class AzurirajProfilHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<AzurirajProfilCommand, AuthUserDto>
{
    public async Task<AuthUserDto> Handle(AzurirajProfilCommand c, CancellationToken ct)
    {
        var korisnik = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct)
            ?? throw new NotFoundException("Korisnik nije pronađen.");

        korisnik.Name = c.Ime.Trim();
        korisnik.Phone = c.Telefon.Trim();

        await db.SaveChangesAsync(ct);
        return korisnik.ToAuthDto();
    }
}

/// <summary>
/// Prijavljeni kupac pamti pismo na nalogu (P7), da mejlovi i pretplatne narudžbe — koje se
/// šalju bez zahtjeva iz pregledača — stignu na pismu koje je izabrao. Tijelo: { pismo: "Cirilica" }.
/// </summary>
public record PostaviPismoCommand(string Pismo) : IRequest<AuthUserDto>;

public class PostaviPismoValidator : AbstractValidator<PostaviPismoCommand>
{
    internal static bool Validno(string? pismo, out Pismo rez) =>
        Enum.TryParse(pismo, out rez) && Enum.IsDefined(rez) && !int.TryParse(pismo, out _);

    public PostaviPismoValidator()
    {
        RuleFor(x => x.Pismo).Must(p => Validno(p, out _)).WithMessage("Nepoznato pismo.");
    }
}

public class PostaviPismoHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<PostaviPismoCommand, AuthUserDto>
{
    public async Task<AuthUserDto> Handle(PostaviPismoCommand c, CancellationToken ct)
    {
        var korisnik = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct)
            ?? throw new NotFoundException("Korisnik nije pronađen.");

        PostaviPismoValidator.Validno(c.Pismo, out var pismo);
        korisnik.Pismo = pismo;

        await db.SaveChangesAsync(ct);
        return korisnik.ToAuthDto();
    }
}

using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
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

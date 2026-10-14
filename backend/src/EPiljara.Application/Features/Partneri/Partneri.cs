using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Partneri;

internal static class PartnerMapiranje
{
    public static PartnerPrijavaDto ToDto(this PartnerPrijava p) => new()
    {
        Id = p.Id,
        Ime = p.Ime,
        Gazdinstvo = p.Gazdinstvo,
        Mjesto = p.Mjesto,
        Telefon = p.Telefon,
        Email = p.Email,
        Proizvodi = p.Proizvodi,
        Status = p.Status.ToString(),
        AdminNapomena = p.AdminNapomena,
        Kreirana = p.Kreirana,
    };
}

/* ---------- Javno ---------- */

/// <summary>Polje <c>Web</c> je honeypot: čovjek ga ne vidi, bot ga popuni.</summary>
public record PrijavaInput(
    string Ime, string? Gazdinstvo, string Mjesto, string Telefon, string? Email, string Proizvodi, string? Web);

public class PrijavaInputValidator : AbstractValidator<PrijavaInput>
{
    public PrijavaInputValidator()
    {
        // Provjera ide nad trimovanom vrijednošću — handler upisuje trimovano.
        RuleFor(x => (x.Ime ?? "").Trim()).Length(2, 80)
            .WithName("Ime").OverridePropertyName("ime")
            .WithMessage("Ime mora imati od 2 do 80 znakova.");
        RuleFor(x => (x.Gazdinstvo ?? "").Trim()).MaximumLength(120)
            .WithName("Gazdinstvo").OverridePropertyName("gazdinstvo")
            .WithMessage("Naziv gazdinstva može imati najviše 120 znakova.");
        RuleFor(x => (x.Mjesto ?? "").Trim()).Length(2, 80)
            .WithName("Mjesto").OverridePropertyName("mjesto")
            .WithMessage("Mjesto mora imati od 2 do 80 znakova.");
        RuleFor(x => (x.Telefon ?? "").Trim()).Length(6, 30)
            .Matches(@"^[0-9 +/()\-]*$")
            .WithName("Telefon").OverridePropertyName("telefon")
            .WithMessage("Telefon mora imati od 6 do 30 znakova: cifre, razmak i znakovi + / ( ) -.");
        RuleFor(x => (x.Email ?? "").Trim()).MaximumLength(120)
            .WithName("Email").OverridePropertyName("email")
            .WithMessage("Mejl može imati najviše 120 znakova.");
        // Ispravnost se provjerava samo kad je mejl unesen — on je opcion.
        RuleFor(x => (x.Email ?? "").Trim()).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithName("Email").OverridePropertyName("email")
            .WithMessage("Mejl adresa nije ispravna.");
        RuleFor(x => (x.Proizvodi ?? "").Trim()).Length(5, 1000)
            .WithName("Proizvodi").OverridePropertyName("proizvodi")
            .WithMessage("Opis proizvoda mora imati od 5 do 1000 znakova.");
        // Namjerno nema pravila za Web: bot ne smije dobiti 400 zbog honeypota.
    }
}

public record PosaljiPrijavuCommand(PrijavaInput Input) : IRequest<PrijavaPrimljenaDto>;

public class PosaljiPrijavuValidator : AbstractValidator<PosaljiPrijavuCommand>
{
    public PosaljiPrijavuValidator() => RuleFor(x => x.Input).SetValidator(new PrijavaInputValidator());
}

public class PosaljiPrijavuHandler(IAppDbContext db) : IRequestHandler<PosaljiPrijavuCommand, PrijavaPrimljenaDto>
{
    public async Task<PrijavaPrimljenaDto> Handle(PosaljiPrijavuCommand cmd, CancellationToken ct)
    {
        // Honeypot (spec O10/O11): polje koje čovjek ne vidi. Popunjeno = bot. Odgovor je isti
        // kao za uspjeh, da bot ne nauči da je otkriven.
        if (!string.IsNullOrWhiteSpace(cmd.Input.Web))
            return new PrijavaPrimljenaDto();

        var i = cmd.Input;
        db.PartnerPrijave.Add(new PartnerPrijava
        {
            Ime = i.Ime.Trim(),
            Gazdinstvo = (i.Gazdinstvo ?? "").Trim(),
            Mjesto = i.Mjesto.Trim(),
            Telefon = i.Telefon.Trim(),
            Email = (i.Email ?? "").Trim(),
            Proizvodi = i.Proizvodi.Trim(),
            Status = StatusPrijave.Nova,
        });
        await db.SaveChangesAsync(ct);
        return new PrijavaPrimljenaDto();
    }
}

/* ---------- Admin ---------- */

public record GetAdminPartneriQuery(string? Status) : IRequest<IReadOnlyList<PartnerPrijavaDto>>;

public class GetAdminPartneriHandler(IAppDbContext db)
    : IRequestHandler<GetAdminPartneriQuery, IReadOnlyList<PartnerPrijavaDto>>
{
    public async Task<IReadOnlyList<PartnerPrijavaDto>> Handle(GetAdminPartneriQuery q, CancellationToken ct)
    {
        var upit = db.PartnerPrijave.AsQueryable();
        // Nevaljan ili prazan status znači bez filtera, kao kod pretplata.
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<StatusPrijave>(q.Status, ignoreCase: true, out var status))
            upit = upit.Where(p => p.Status == status);
        // Sortiranje u memoriji — isto se ponaša na SQLite i Postgresu.
        var sve = await upit.ToListAsync(ct);
        return sve.OrderByDescending(p => p.Kreirana).Select(p => p.ToDto()).ToList();
    }
}

public record ObradaInput(string Status, string? AdminNapomena);

public class ObradaInputValidator : AbstractValidator<ObradaInput>
{
    public ObradaInputValidator()
    {
        // IsDefined je obavezan: TryParse sam prihvata i brojeve poput "99".
        RuleFor(x => x.Status)
            .Must(v => Enum.TryParse<StatusPrijave>(v, true, out var s) && Enum.IsDefined(s))
            .WithName("Status").OverridePropertyName("status")
            .WithMessage("Status mora biti Nova, UObradi, Prihvacena ili Odbijena.");
        RuleFor(x => (x.AdminNapomena ?? "").Trim()).MaximumLength(1000)
            .WithName("AdminNapomena").OverridePropertyName("adminNapomena")
            .WithMessage("Napomena može imati najviše 1000 znakova.");
    }
}

public record ObradiPrijavuCommand(Guid Id, ObradaInput Input) : IRequest<PartnerPrijavaDto>;

public class ObradiPrijavuValidator : AbstractValidator<ObradiPrijavuCommand>
{
    public ObradiPrijavuValidator() => RuleFor(x => x.Input).SetValidator(new ObradaInputValidator());
}

public class ObradiPrijavuHandler(IAppDbContext db) : IRequestHandler<ObradiPrijavuCommand, PartnerPrijavaDto>
{
    public async Task<PartnerPrijavaDto> Handle(ObradiPrijavuCommand cmd, CancellationToken ct)
    {
        var p = await db.PartnerPrijave.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Prijava nije pronađena.");
        p.Status = Enum.Parse<StatusPrijave>(cmd.Input.Status.Trim(), ignoreCase: true);
        // Bez napomene u tijelu postojeća ostaje; poslata (i prazna) je zamjenjuje.
        if (cmd.Input.AdminNapomena is not null)
            p.AdminNapomena = cmd.Input.AdminNapomena.Trim();
        await db.SaveChangesAsync(ct);
        return p.ToDto();
    }
}

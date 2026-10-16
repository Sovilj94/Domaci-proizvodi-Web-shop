using EPiljara.Application.Features.Account;
using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
// MediatR.Unit i Domain.Enums.Unit se sudaraju (CS0104) — alias bira onaj koji ovdje treba.
using Unit = MediatR.Unit;

namespace EPiljara.Application.Features.Pretplate;

/// <summary>
/// Pretplate prijavljenog kupca: spisak, nova, izmjena, pauza, nastavak, otkaz.
/// Pravila koja dijele sve komande stoje u <see cref="PretplataPravila"/>.
/// </summary>
public static class PretplataPravila
{
    /// <summary>
    /// Učitava pretplatu i potvrđuje da pripada pozivaocu. Vlasništvo se provjerava POSLIJE
    /// dohvata, ne filterom u upitu, da razlika između „ne postoji" i „nije tvoja" ostane
    /// namjerna odluka — isto kao u UpdateAddressHandler.
    /// </summary>
    public static async Task<Pretplata> UcitajMojuAsync(
        IAppDbContext db, ICurrentUser current, Guid id, CancellationToken ct)
    {
        var p = await db.Pretplate
            .Include(x => x.Product)
            .Include(x => x.DeliveryZone)
            .Include(x => x.DeliverySlot)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Pretplata nije pronađena.");

        if (!current.IsAuthenticated || p.UserId != current.Id)
            throw new ForbiddenException("Ova pretplata nije vaša.");

        return p;
    }

    /// <summary>
    /// Provjerava sve što pretplata traži od kataloga i dostave. Zovu je i nova pretplata i
    /// izmjena, jer izmjena može promijeniti svaki od ovih izbora.
    ///
    /// Minimum zone se provjerava OVDJE, a ne tek kad posao napravi narudžbu: pretplata koja
    /// ne može proći checkout bi inače tiho ćutala svake sedmice, a kupac bi mislio da je
    /// pretplaćen.
    /// </summary>
    public static async Task ProvjeriIzborAsync(
        IAppDbContext db, Guid productId, Guid zoneId, Guid slotId, CancellationToken ct)
    {
        var korpica = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (korpica is null || !korpica.IsAvailable)
            throw new DomainException("Korpica nije dostupna.", "korpica_nedostupna");
        if (korpica.Vrsta != VrstaProizvoda.Korpica)
            throw new DomainException("Pretplata je moguća samo na korpicu.", "nije_korpica");

        var zona = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == zoneId && z.IsActive, ct)
            ?? throw new DomainException("Nepoznata zona dostave.", "invalid_zone");

        if (!await db.DeliverySlots.AnyAsync(s => s.Id == slotId, ct))
            throw new DomainException("Nepoznat termin dostave.", "invalid_slot");

        if (korpica.Price < zona.MinOrderAmount)
            throw new DomainException(
                $"Minimalna narudžba za zonu {zona.Name} je {zona.MinOrderAmount:0.##} KM, " +
                "a cijena korpice je niža.", "below_minimum");
    }
}

/* ---------- Spisak ---------- */

public record GetMojePretplateQuery : IRequest<IReadOnlyList<PretplataDto>>;

public class GetMojePretplateHandler(
    IAppDbContext db,
    ICurrentUser current,
    IOptions<KorpiceOpcije> korpiceOpcije,
    IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<GetMojePretplateQuery, IReadOnlyList<PretplataDto>>
{
    public async Task<IReadOnlyList<PretplataDto>> Handle(
        GetMojePretplateQuery q, CancellationToken ct)
    {
        if (!current.IsAuthenticated) return [];

        var moje = await db.Pretplate
            .Include(p => p.Product)
            .Include(p => p.DeliveryZone)
            .Include(p => p.DeliverySlot)
            .Where(p => p.UserId == current.Id)
            .ToListAsync(ct);

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, korpiceOpcije.Value, ct);

        return moje
            // Aktivne prvo, pa pauzirane, pa otkazane; unutar grupe najnovija prva.
            .OrderBy(p => p.Status)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => Sastavi(p, dan, vrijemeOd, cutoff, pretplateOpcije.Value))
            .ToList();
    }

    internal static PretplataDto Sastavi(
        Pretplata p, DayOfWeek dan, string vrijemeOd, int cutoff, PretplateOpcije o)
    {
        var puna = p.Product?.Price ?? 0m;
        var trenutna = PretplatnaCijena.Izracunaj(puna, p.Plan, o);
        var preostalo = PretplatnaCijena.PreostaloObaveze(p.ObavezaIsporuka, p.BrojIsporuka);

        // Sedmica naredne isporuke: ona koju bi narudžba poslata sada isporučila, a za
        // pauziranu pretplatu prva poslije pauze. Otkazana je nema.
        (int Godina, int Sedmica)? sljedeca = null;
        if (p.Status != StatusPretplate.Otkazana)
        {
            var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);
            if (p.PauzaDoGodina is int pg && p.PauzaDoSedmica is int ps
                && SedmicaHelper.SedmicaPrije(g, s, pg, ps))
                sljedeca = (pg, ps);
            else
                sljedeca = (g, s);
        }

        return new PretplataDto
        {
            Id = p.Id,
            Status = p.Status.ToString(),
            ProductId = p.ProductId,
            Naziv = p.Product?.Name ?? string.Empty,
            Slug = p.Product?.Slug ?? string.Empty,
            Tip = p.Product?.TipKorpice is TipKorpice t ? SedmicaHelper.Slug(t) : null,
            Velicina = p.Product?.Velicina?.ToString().ToLowerInvariant(),
            ImageUrl = p.Product?.ImageUrl,
            DeliveryAddress = p.DeliveryAddress,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            DeliveryZoneId = p.DeliveryZoneId,
            DeliveryZoneName = p.DeliveryZone?.Name,
            DeliveryZoneActive = p.DeliveryZone?.IsActive ?? false,
            DeliverySlotId = p.DeliverySlotId,
            DeliverySlotLabel = p.DeliverySlot?.Label,
            Napomena = p.Napomena,
            BrojIsporuka = p.BrojIsporuka,
            PunaCijena = puna,
            TrenutnaCijena = trenutna,
            PopustAktivan = trenutna < puna,
            Plan = p.Plan.ToString(),
            ObavezaIsporuka = p.ObavezaIsporuka,
            PreostaloObaveze = preostalo,
            MozeOtkazati = p.Status != StatusPretplate.Otkazana && preostalo == 0,
            SljedecaGodina = sljedeca?.Godina,
            SljedecaSedmica = sljedeca?.Sedmica,
            SljedecaIsporuka = sljedeca is { } s2
                ? SedmicaHelper.DatumIsporuke(s2.Godina, s2.Sedmica, dan, vrijemeOd)
                : null,
            PauzaDoGodina = p.PauzaDoGodina,
            PauzaDoSedmica = p.PauzaDoSedmica,
            CreatedAt = p.CreatedAt,
        };
    }
}

/* ---------- Nova pretplata ---------- */

public record NapraviPretplatuCommand(
    Guid ProductId,
    Guid DeliveryZoneId,
    Guid DeliverySlotId,
    string DeliveryAddress,
    double? Latitude,
    double? Longitude,
    string? Napomena,
    string? Plan = null) : IRequest<PretplataDto>, IAddressFields
{
    string IAddressFields.FullAddress => DeliveryAddress;
}

/// <summary>Plan sa žice: PascalCase ime enuma; izostanak = Nedjeljna.</summary>
internal static class PlanUlaz
{
    public static bool Validan(string? plan) =>
        plan is null || (Enum.TryParse<PlanPretplate>(plan, out var p) && Enum.IsDefined(p) && !int.TryParse(plan, out _));

    public static PlanPretplate Procitaj(string? plan) =>
        plan is not null && Enum.TryParse<PlanPretplate>(plan, out var p) ? p : PlanPretplate.Nedjeljna;
}

public class NapraviPretplatuValidator : AbstractValidator<NapraviPretplatuCommand>
{
    public NapraviPretplatuValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Odaberite korpicu.");
        RuleFor(x => x.DeliveryZoneId).NotEmpty().WithMessage("Odaberite zonu dostave.");
        RuleFor(x => x.DeliverySlotId).NotEmpty().WithMessage("Odaberite termin dostave.");
        RuleFor(x => x.Napomena).MaximumLength(1000)
            .When(x => x.Napomena is not null).WithMessage("Napomena je predugačka.");
        RuleFor(x => x.Plan).Must(PlanUlaz.Validan).WithMessage("Nepoznat plan pretplate.");
        // Ista pravila za adresu i tačku kao u adresaru — poruke idu pod ključ
        // `fullAddress`, jer obje forme prikazuju grešku ispod polja sa mapom.
        AddressRules.Apply(this);
    }
}

public class NapraviPretplatuHandler(
    IAppDbContext db,
    ICurrentUser current,
    IOptions<KorpiceOpcije> korpiceOpcije,
    IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<NapraviPretplatuCommand, PretplataDto>
{
    public async Task<PretplataDto> Handle(NapraviPretplatuCommand cmd, CancellationToken ct)
    {
        if (!current.IsAuthenticated)
            throw new ForbiddenException("Pretplata traži nalog.");

        await PretplataPravila.ProvjeriIzborAsync(
            db, cmd.ProductId, cmd.DeliveryZoneId, cmd.DeliverySlotId, ct);

        // Baza ovo brani filtriranim indeksom; ovdje se hvata da kupac dobije poruku umjesto
        // 500. Otkazane se namjerno ne broje — kupac se smije vratiti na isti paket.
        var vecPostoji = await db.Pretplate.AnyAsync(
            p => p.UserId == current.Id && p.ProductId == cmd.ProductId
                 && p.Status != StatusPretplate.Otkazana, ct);
        if (vecPostoji)
            throw new ConflictException(
                "Već ste pretplaćeni na ovu korpicu.", "pretplata_vec_postoji");

        var p = new Pretplata
        {
            UserId = current.Id!.Value,
            ProductId = cmd.ProductId,
            Status = StatusPretplate.Aktivna,
            DeliveryZoneId = cmd.DeliveryZoneId,
            DeliverySlotId = cmd.DeliverySlotId,
            DeliveryAddress = cmd.DeliveryAddress.Trim(),
            Latitude = cmd.Latitude,
            Longitude = cmd.Longitude,
            Napomena = string.IsNullOrWhiteSpace(cmd.Napomena) ? null : cmd.Napomena.Trim(),
            Plan = PlanUlaz.Procitaj(cmd.Plan),
        };
        p.ObavezaIsporuka = PretplatnaCijena.Obaveza(p.Plan, pretplateOpcije.Value);

        db.Pretplate.Add(p);
        await db.SaveChangesAsync(ct);

        return await SastaviAsync(db, p, korpiceOpcije.Value, pretplateOpcije.Value, ct);
    }

    internal static async Task<PretplataDto> SastaviAsync(
        IAppDbContext db, Pretplata p, KorpiceOpcije korpiceOpcije, PretplateOpcije o,
        CancellationToken ct)
    {
        // Navigacije se učitavaju eksplicitno: poslije izmjene FK-a EF resetuje IsLoaded, pa
        // bi DTO inače nosio staru zonu ili stari termin (isto kao u UpdateAddressHandler).
        await db.Pretplate.Entry(p).Reference(x => x.Product).LoadAsync(ct);
        await db.Pretplate.Entry(p).Reference(x => x.DeliveryZone).LoadAsync(ct);
        await db.Pretplate.Entry(p).Reference(x => x.DeliverySlot).LoadAsync(ct);

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, korpiceOpcije, ct);
        return GetMojePretplateHandler.Sastavi(p, dan, vrijemeOd, cutoff, o);
    }
}

/* ---------- Izmjena ---------- */

public record IzmijeniPretplatuCommand(
    Guid Id,
    Guid ProductId,
    Guid DeliveryZoneId,
    Guid DeliverySlotId,
    string DeliveryAddress,
    double? Latitude,
    double? Longitude,
    string? Napomena) : IRequest<PretplataDto>, IAddressFields
{
    string IAddressFields.FullAddress => DeliveryAddress;
}

public class IzmijeniPretplatuValidator : AbstractValidator<IzmijeniPretplatuCommand>
{
    public IzmijeniPretplatuValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Odaberite korpicu.");
        RuleFor(x => x.DeliveryZoneId).NotEmpty().WithMessage("Odaberite zonu dostave.");
        RuleFor(x => x.DeliverySlotId).NotEmpty().WithMessage("Odaberite termin dostave.");
        RuleFor(x => x.Napomena).MaximumLength(1000)
            .When(x => x.Napomena is not null).WithMessage("Napomena je predugačka.");
        AddressRules.Apply(this);
    }
}

public class IzmijeniPretplatuHandler(
    IAppDbContext db,
    ICurrentUser current,
    IOptions<KorpiceOpcije> korpiceOpcije,
    IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<IzmijeniPretplatuCommand, PretplataDto>
{
    public async Task<PretplataDto> Handle(IzmijeniPretplatuCommand cmd, CancellationToken ct)
    {
        var p = await PretplataPravila.UcitajMojuAsync(db, current, cmd.Id, ct);

        if (p.Status == StatusPretplate.Otkazana)
            throw new ConflictException(
                "Otkazana pretplata se ne može mijenjati.", "pretplata_otkazana");

        await PretplataPravila.ProvjeriIzborAsync(
            db, cmd.ProductId, cmd.DeliveryZoneId, cmd.DeliverySlotId, ct);

        if (p.ProductId != cmd.ProductId)
        {
            var vecPostoji = await db.Pretplate.AnyAsync(
                x => x.UserId == p.UserId && x.ProductId == cmd.ProductId
                     && x.Id != p.Id && x.Status != StatusPretplate.Otkazana, ct);
            if (vecPostoji)
                throw new ConflictException(
                    "Već ste pretplaćeni na tu korpicu.", "pretplata_vec_postoji");
        }

        // Promjena paketa NE nulira BrojIsporuka: popust je zarađen vjernošću pretplati, ne
        // konkretnoj korpici. Kupac koji pređe sa male na srednju ne počinje iznova.
        p.ProductId = cmd.ProductId;
        p.DeliveryZoneId = cmd.DeliveryZoneId;
        p.DeliverySlotId = cmd.DeliverySlotId;
        p.DeliveryAddress = cmd.DeliveryAddress.Trim();
        p.Latitude = cmd.Latitude;
        p.Longitude = cmd.Longitude;
        p.Napomena = string.IsNullOrWhiteSpace(cmd.Napomena) ? null : cmd.Napomena.Trim();

        await db.SaveChangesAsync(ct);

        return await NapraviPretplatuHandler.SastaviAsync(
            db, p, korpiceOpcije.Value, pretplateOpcije.Value, ct);
    }
}

/* ---------- Pauza i nastavak ---------- */

public record PauzirajPretplatuCommand(Guid Id, int Sedmica) : IRequest<PretplataDto>;

public class PauzirajPretplatuValidator : AbstractValidator<PauzirajPretplatuCommand>
{
    public PauzirajPretplatuValidator()
    {
        // Gornja granica je iz PretplateOpcije.MaksPauzaSedmica; validator ne vidi
        // konfiguraciju, pa stoji podrazumijevana vrijednost. Handler je mjerodavan.
        RuleFor(x => x.Sedmica).InclusiveBetween(1, 8)
            .WithMessage("Pauza može trajati od 1 do 8 sedmica.");
    }
}

public class PauzirajPretplatuHandler(
    IAppDbContext db,
    ICurrentUser current,
    IOptions<KorpiceOpcije> korpiceOpcije,
    IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<PauzirajPretplatuCommand, PretplataDto>
{
    public async Task<PretplataDto> Handle(PauzirajPretplatuCommand cmd, CancellationToken ct)
    {
        var o = pretplateOpcije.Value;
        if (cmd.Sedmica < 1 || cmd.Sedmica > o.MaksPauzaSedmica)
            throw new DomainException(
                $"Pauza može trajati od 1 do {o.MaksPauzaSedmica} sedmica.", "neispravna_pauza");

        var p = await PretplataPravila.UcitajMojuAsync(db, current, cmd.Id, ct);

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, korpiceOpcije.Value, ct);
        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);

        // Pauza se mjeri od sedmice koja bi SLJEDEĆA bila isporučena, ne od današnjeg datuma:
        // kupac koji pauzira „na dvije sedmice" misli na dvije isporuke, a ne na četrnaest dana.
        var (pg, ps) = SedmicaHelper.Pomjeri(g, s, cmd.Sedmica);

        // Predji briše PauzaDo, pa se pauza upisuje POSLIJE prelaza. Obrnuto bi je obrisalo.
        if (p.Status != StatusPretplate.Pauzirana) p.Predji(StatusPretplate.Pauzirana);
        p.PauzaDoGodina = pg;
        p.PauzaDoSedmica = ps;

        await db.SaveChangesAsync(ct);
        return await NapraviPretplatuHandler.SastaviAsync(db, p, korpiceOpcije.Value, o, ct);
    }
}

public record NastaviPretplatuCommand(Guid Id) : IRequest<PretplataDto>;

public class NastaviPretplatuHandler(
    IAppDbContext db,
    ICurrentUser current,
    IOptions<KorpiceOpcije> korpiceOpcije,
    IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<NastaviPretplatuCommand, PretplataDto>
{
    public async Task<PretplataDto> Handle(NastaviPretplatuCommand cmd, CancellationToken ct)
    {
        var p = await PretplataPravila.UcitajMojuAsync(db, current, cmd.Id, ct);

        // Prelaz briše i pauzu — vidi Pretplata.Predji.
        if (p.Status == StatusPretplate.Pauzirana) p.Predji(StatusPretplate.Aktivna);
        else if (p.Status == StatusPretplate.Otkazana)
            throw new ConflictException(
                "Otkazana pretplata se ne može nastaviti.", "pretplata_otkazana");

        await db.SaveChangesAsync(ct);
        return await NapraviPretplatuHandler.SastaviAsync(
            db, p, korpiceOpcije.Value, pretplateOpcije.Value, ct);
    }
}

/* ---------- Otkaz ---------- */

public record OtkaziPretplatuCommand(Guid Id) : IRequest<Unit>;

public partial class OtkaziPretplatuHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<OtkaziPretplatuCommand, Unit>
{
    public async Task<Unit> Handle(OtkaziPretplatuCommand cmd, CancellationToken ct)
    {
        var p = await PretplataPravila.UcitajMojuAsync(db, current, cmd.Id, ct);

        // Red se ne briše: narudžbe koje su iz njega nastale pokazuju na njega, a kupac ima
        // pravo da vidi istoriju onoga što je platio. Otkaz je status, kao svuda u projektu.
        if (p.Status == StatusPretplate.Otkazana) return Unit.Value;

        // Obaveza plana (R2): dok traje, kupac otkazuje preko podrške (AdminOtkaziPretplatu).
        var preostalo = PretplatnaCijena.PreostaloObaveze(p.ObavezaIsporuka, p.BrojIsporuka);
        if (preostalo > 0)
            throw new ConflictException(
                $"Obaveza traje još {preostalo} {IsporukaRijec(preostalo)}. Za otkaz nam se javi.", "obaveza_aktivna");

        p.Predji(StatusPretplate.Otkazana);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public partial class OtkaziPretplatuHandler
{
    /// <summary>1 isporuka, 2–4 isporuke, 5+ i 11–14 isporuka.</summary>
    private static string IsporukaRijec(int n) =>
        n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "isporuke" : "isporuka";
}

/// <summary>Otkaz koji radi admin (podrška) — i dok traje obaveza plana.</summary>
public record AdminOtkaziPretplatuCommand(Guid Id) : IRequest<Unit>;

public class AdminOtkaziPretplatuHandler(IAppDbContext db) : IRequestHandler<AdminOtkaziPretplatuCommand, Unit>
{
    public async Task<Unit> Handle(AdminOtkaziPretplatuCommand cmd, CancellationToken ct)
    {
        var p = await db.Pretplate.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Pretplata nije pronađena.");
        if (p.Status != StatusPretplate.Otkazana) p.Predji(StatusPretplate.Otkazana);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

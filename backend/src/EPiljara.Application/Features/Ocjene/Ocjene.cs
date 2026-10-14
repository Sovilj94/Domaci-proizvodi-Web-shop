using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Utisci;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Ocjene;

internal static class OcjenaPravila
{
    /// <summary>Zajedničko za vlasnika i gosta: provjera statusa, jedna ocjena, upis.</summary>
    public static async Task<OcjenaDto> OcijeniAsync(
        IAppDbContext db, Order order, OcjenaInput input, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Delivered)
            throw new DomainException("Ocijeniti se može samo isporučena narudžba.", "narudzba_nije_isporucena");
        if (await db.OcjeneIsporuke.AnyAsync(o => o.OrderId == order.Id, ct))
            throw new ConflictException("Ova narudžba je već ocijenjena.", "vec_ocijenjeno");

        var ocjena = new OcjenaIsporuke
        {
            OrderId = order.Id,
            Ocjena = input.Ocjena,
            Komentar = (input.Komentar ?? "").Trim(),
            DozvolaZaObjavu = input.DozvolaZaObjavu,
        };
        db.OcjeneIsporuke.Add(ocjena);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Dva istovremena zahtjeva prođu AnyAsync; jedinstven indeks propusti samo prvi.
            // Konflikt prijavljujemo samo ako red zaista postoji; ostale greške propuštamo dalje.
            db.OcjeneIsporuke.Entry(ocjena).State = EntityState.Detached;
            if (await db.OcjeneIsporuke.AnyAsync(o => o.OrderId == order.Id, ct))
                throw new ConflictException("Ova narudžba je već ocijenjena.", "vec_ocijenjeno");
            throw;
        }
        return ocjena.ToDto();
    }

    /// <summary>
    /// „marko marković" -> „Marko M."; jedna riječ -> samo ta riječ. Prvo ime i inicijal ZADNJE
    /// riječi, razmaci sažeti, najviše 60 znakova (granica Utisak.Ime).
    /// </summary>
    internal static string ImeZaUtisak(string? punoIme)
    {
        var rijeci = (punoIme ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (rijeci.Length == 0) return "Kupac";
        var prva = rijeci[0];
        var ime = char.ToUpperInvariant(prva[0]) + prva[1..].ToLowerInvariant();
        if (rijeci.Length > 1)
            ime += " " + char.ToUpperInvariant(rijeci[^1][0]) + ".";
        return ime.Length > 60 ? ime[..60] : ime;
    }
}

public record OcjenaInput(int Ocjena, string? Komentar, bool DozvolaZaObjavu);

public class OcjenaInputValidator : AbstractValidator<OcjenaInput>
{
    public OcjenaInputValidator()
    {
        RuleFor(x => x.Ocjena).InclusiveBetween(1, 5)
            .WithName("Ocjena").OverridePropertyName("ocjena")
            .WithMessage("Ocjena mora biti od 1 do 5.");
        // Provjera ide nad trimovanom vrijednošću — handler upisuje trimovano.
        RuleFor(x => (x.Komentar ?? "").Trim()).MaximumLength(1000)
            .WithName("Komentar").OverridePropertyName("komentar")
            .WithMessage("Komentar može imati najviše 1000 znakova.");
    }
}

/* ---------- Vlasnik ---------- */

public record OcijeniNarudzbuCommand(Guid OrderId, OcjenaInput Input) : IRequest<OcjenaDto>;

public class OcijeniNarudzbuValidator : AbstractValidator<OcijeniNarudzbuCommand>
{
    public OcijeniNarudzbuValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new OcjenaInputValidator()).When(x => x.Input is not null);
    }
}

public class OcijeniNarudzbuHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<OcijeniNarudzbuCommand, OcjenaDto>
{
    public async Task<OcjenaDto> Handle(OcijeniNarudzbuCommand cmd, CancellationToken ct)
    {
        // Tuđa narudžba se ne razlikuje od nepostojeće — ne otkriva se da postoji.
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId && o.CustomerId == current.Id, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        return await OcjenaPravila.OcijeniAsync(db, order, cmd.Input, ct);
    }
}

/* ---------- Gost preko tokena ---------- */

public record OcijeniPrekoTokenaCommand(string Token, OcjenaInput Input) : IRequest<OcjenaDto>;

public class OcijeniPrekoTokenaValidator : AbstractValidator<OcijeniPrekoTokenaCommand>
{
    public OcijeniPrekoTokenaValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new OcjenaInputValidator()).When(x => x.Input is not null);
    }
}

public class OcijeniPrekoTokenaHandler(IAppDbContext db)
    : IRequestHandler<OcijeniPrekoTokenaCommand, OcjenaDto>
{
    public async Task<OcjenaDto> Handle(OcijeniPrekoTokenaCommand cmd, CancellationToken ct)
    {
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.PublicToken == cmd.Token, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        return await OcjenaPravila.OcijeniAsync(db, order, cmd.Input, ct);
    }
}

/* ---------- Admin ---------- */

public record GetAdminOcjeneQuery(int? Ocjena) : IRequest<OcjeneAdminDto>;

public class GetAdminOcjeneHandler(IAppDbContext db) : IRequestHandler<GetAdminOcjeneQuery, OcjeneAdminDto>
{
    public async Task<OcjeneAdminDto> Handle(GetAdminOcjeneQuery q, CancellationToken ct)
    {
        var sve = await db.OcjeneIsporuke
            .Include(o => o.Order).ThenInclude(r => r!.DeliveryZone)
            .ToListAsync(ct);

        // Sažetak je uvijek nad svim ocjenama; filter sužava samo stavke.
        var poBroju = new int[5];
        foreach (var o in sve) poBroju[Math.Clamp(o.Ocjena, 1, 5) - 1]++;
        var prosjek = sve.Count == 0
            ? 0m
            : Math.Round((decimal)sve.Average(o => o.Ocjena), 1, MidpointRounding.AwayFromZero);

        var utisci = await db.Utisci
            .Where(u => u.OcjenaId != null)
            .Select(u => new { OcjenaId = u.OcjenaId!.Value, u.Id })
            .ToListAsync(ct);
        var poOcjeni = utisci.ToDictionary(u => u.OcjenaId, u => u.Id);

        var stavke = sve
            .Where(o => q.Ocjena is null || o.Ocjena == q.Ocjena)
            .OrderByDescending(o => o.Kreirana)
            .Select(o => new OcjenaAdminDto
            {
                Id = o.Id,
                Ocjena = o.Ocjena,
                Komentar = o.Komentar,
                DozvolaZaObjavu = o.DozvolaZaObjavu,
                Kreirana = o.Kreirana,
                OrderId = o.OrderId,
                KodNarudzbe = o.Order?.Code ?? "",
                Kupac = o.Order?.CustomerName ?? "",
                Zona = o.Order?.DeliveryZone?.Name ?? "",
                UtisakId = poOcjeni.TryGetValue(o.Id, out var uid) ? uid : null,
            })
            .ToList();

        return new OcjeneAdminDto
        {
            Sazetak = new OcjeneSazetakDto { Prosjek = prosjek, Broj = sve.Count, PoBroju = poBroju },
            Stavke = stavke,
        };
    }
}

public record NapraviUtisakIzOcjeneCommand(Guid OcjenaId) : IRequest<UtisakDto>;

public class NapraviUtisakIzOcjeneHandler(IAppDbContext db)
    : IRequestHandler<NapraviUtisakIzOcjeneCommand, UtisakDto>
{
    public async Task<UtisakDto> Handle(NapraviUtisakIzOcjeneCommand cmd, CancellationToken ct)
    {
        var ocjena = await db.OcjeneIsporuke
            .Include(o => o.Order).ThenInclude(r => r!.DeliveryZone)
            .FirstOrDefaultAsync(o => o.Id == cmd.OcjenaId, ct)
            ?? throw new NotFoundException("Ocjena nije pronađena.");

        if (await db.Utisci.AnyAsync(u => u.OcjenaId == ocjena.Id, ct))
            throw new ConflictException("Utisak iz ove ocjene već postoji.", "utisak_vec_postoji");

        var komentar = ocjena.Komentar.Trim();
        if (!ocjena.DozvolaZaObjavu)
            throw new DomainException("Kupac nije dozvolio objavu ove ocjene.", "ocjena_nije_za_objavu");
        // Utisak traži najmanje 10 znakova.
        if (komentar.Length < 10)
            throw new DomainException("Komentar je prekratak za utisak.", "ocjena_nije_za_objavu");
        // Utisak ima najviše 500; admin ga ionako uređuje prije objave.
        if (komentar.Length > 500) komentar = komentar[..497] + "…";

        var zona = ocjena.Order?.DeliveryZone?.Name ?? "";
        var utisak = new Utisak
        {
            Ime = OcjenaPravila.ImeZaUtisak(ocjena.Order?.CustomerName),
            Mjesto = zona.Length > 60 ? zona[..60] : zona,
            Tekst = komentar,
            Objavljen = false,
            Redoslijed = 0,
            OcjenaId = ocjena.Id,
        };
        db.Utisci.Add(utisak);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Filtrirani jedinstveni indeks na Utisci.OcjenaId hvata istovremene zahtjeve.
            // Konflikt prijavljujemo samo ako red zaista postoji; ostale greške propuštamo dalje.
            db.Utisci.Entry(utisak).State = EntityState.Detached;
            if (await db.Utisci.AnyAsync(u => u.OcjenaId == ocjena.Id, ct))
                throw new ConflictException("Utisak iz ove ocjene već postoji.", "utisak_vec_postoji");
            throw;
        }
        return utisak.ToDto();
    }
}

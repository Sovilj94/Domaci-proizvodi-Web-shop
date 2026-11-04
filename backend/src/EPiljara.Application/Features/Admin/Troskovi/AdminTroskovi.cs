using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin.Troskovi;

/* ---------- Pregled mjeseca ---------- */

/// <summary>Troškovi mjeseca + sve vrste, ponavljajući troškovi i cijene goriva (za stranicu Troškovi).</summary>
public record GetTroskoviQuery(int Godina, int Mjesec) : IRequest<TroskoviMjesecaDto>;

public class GetTroskoviHandler(IAppDbContext db) : IRequestHandler<GetTroskoviQuery, TroskoviMjesecaDto>
{
    public async Task<TroskoviMjesecaDto> Handle(GetTroskoviQuery q, CancellationToken ct)
    {
        TroskoviPravila.ProvjeriMjesec(q.Godina, q.Mjesec);
        var od = new DateOnly(q.Godina, q.Mjesec, 1);
        var vrste = await db.VrsteTroskova.AsNoTracking().OrderBy(v => v.Redoslijed).ThenBy(v => v.Naziv).ToListAsync(ct);
        var koristene = (await db.Troskovi.Select(t => t.VrstaTroskaId).Distinct().ToListAsync(ct))
            .Concat(await db.PonavljajuciTroskovi.Select(t => t.VrstaTroskaId).Distinct().ToListAsync(ct))
            .ToHashSet();
        var troskovi = await db.Troskovi.AsNoTracking().Include(t => t.Vrsta)
            .Where(t => t.Datum >= od && t.Datum < od.AddMonths(1))
            .OrderBy(t => t.Datum).ThenBy(t => t.Kreiran)
            .ToListAsync(ct);
        var ponavljajuci = await db.PonavljajuciTroskovi.AsNoTracking().Include(t => t.Vrsta).ToListAsync(ct);
        var cijene = await db.CijeneGoriva.AsNoTracking().OrderByDescending(c => c.VaziOd).ToListAsync(ct);

        return new TroskoviMjesecaDto
        {
            Godina = q.Godina,
            Mjesec = q.Mjesec,
            Troskovi = troskovi.Select(TroskoviPravila.ToDto).ToList(),
            Ponavljajuci = ponavljajuci
                .OrderByDescending(p => p.VaziU(q.Godina, q.Mjesec)).ThenBy(p => p.Vrsta!.Redoslijed).ThenBy(p => p.Opis)
                .Select(p => TroskoviPravila.ToDto(p, q.Godina, q.Mjesec)).ToList(),
            Vrste = vrste.Select(v => new VrstaTroskaDto
            {
                Id = v.Id, Naziv = v.Naziv, JeGorivo = v.JeGorivo, Redoslijed = v.Redoslijed, Aktivna = v.Aktivna,
                UUpotrebi = koristene.Contains(v.Id),
            }).ToList(),
            CijeneGoriva = cijene.Select(c => new CijenaGorivaDto
            {
                Id = c.Id, VaziOd = c.VaziOd, CijenaPoLitru = c.CijenaPoLitru, PotrosnjaL100km = c.PotrosnjaL100km,
            }).ToList(),
        };
    }
}

internal static class TroskoviPravila
{
    public static void ProvjeriMjesec(int godina, int mjesec)
    {
        if (mjesec is < 1 or > 12) throw new DomainException("Mjesec mora biti od 1 do 12.", "mjesec_neispravan");
        if (godina is < 2000 or > 2100) throw new DomainException("Godina nije ispravna.", "godina_neispravna");
    }

    public static void ProvjeriIznos(decimal iznos)
    {
        if (iznos <= 0) throw new DomainException("Iznos mora biti veći od nule.", "iznos_neispravan");
        if (iznos > 1_000_000) throw new DomainException("Iznos je prevelik.", "iznos_neispravan");
    }

    public static async Task<VrstaTroska> VrstaAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.VrsteTroskova.FirstOrDefaultAsync(v => v.Id == id, ct)
        ?? throw new NotFoundException("Vrsta troška nije pronađena.");

    public static string Opis(string? opis) => (opis ?? string.Empty).Trim() is var o && o.Length > 300 ? o[..300] : o;

    public static TrosakDto ToDto(Trosak t) => new()
    {
        Id = t.Id, Datum = t.Datum, VrstaId = t.VrstaTroskaId, Vrsta = t.Vrsta?.Naziv ?? string.Empty,
        JeGorivo = t.Vrsta?.JeGorivo ?? false, Iznos = t.Iznos, Opis = t.Opis, RacunUrl = t.RacunUrl,
    };

    public static PonavljajuciTrosakDto ToDto(PonavljajuciTrosak p, int godina, int mjesec) => new()
    {
        Id = p.Id, VrstaId = p.VrstaTroskaId, Vrsta = p.Vrsta?.Naziv ?? string.Empty, Iznos = p.Iznos, Opis = p.Opis,
        OdGodina = p.OdGodina, OdMjesec = p.OdMjesec, DoGodina = p.DoGodina, DoMjesec = p.DoMjesec,
        VaziUMjesecu = p.VaziU(godina, mjesec),
    };
}

/* ---------- Vrste ---------- */

public record SacuvajVrstuTroskaCommand(Guid? Id, string Naziv, bool JeGorivo, int Redoslijed, bool Aktivna = true)
    : IRequest<Guid>;

public class SacuvajVrstuTroskaHandler(IAppDbContext db) : IRequestHandler<SacuvajVrstuTroskaCommand, Guid>
{
    public async Task<Guid> Handle(SacuvajVrstuTroskaCommand c, CancellationToken ct)
    {
        var naziv = (c.Naziv ?? string.Empty).Trim();
        if (naziv.Length is 0 or > 80)
            throw new DomainException("Naziv vrste je obavezan (do 80 znakova).", "naziv_neispravan");
        var lower = naziv.ToLower();
        if (await db.VrsteTroskova.AnyAsync(v => v.Id != c.Id && v.Naziv.ToLower() == lower, ct))
            throw new ConflictException($"Vrsta „{naziv}\" već postoji.", "vrsta_postoji");

        var v = c.Id is Guid id ? await TroskoviPravila.VrstaAsync(db, id, ct) : new VrstaTroska();
        if (c.Id is null) db.VrsteTroskova.Add(v);
        v.Naziv = naziv;
        v.JeGorivo = c.JeGorivo;
        v.Redoslijed = c.Redoslijed;
        v.Aktivna = c.Aktivna;
        await db.SaveChangesAsync(ct);
        return v.Id;
    }
}

public record ObrisiVrstuTroskaCommand(Guid Id) : IRequest<Guid>;

public class ObrisiVrstuTroskaHandler(IAppDbContext db) : IRequestHandler<ObrisiVrstuTroskaCommand, Guid>
{
    public async Task<Guid> Handle(ObrisiVrstuTroskaCommand c, CancellationToken ct)
    {
        var v = await TroskoviPravila.VrstaAsync(db, c.Id, ct);
        if (await db.Troskovi.AnyAsync(t => t.VrstaTroskaId == c.Id, ct)
            || await db.PonavljajuciTroskovi.AnyAsync(t => t.VrstaTroskaId == c.Id, ct))
            throw new ConflictException("Vrsta ima unesene troškove — umjesto brisanja je isključi.", "vrsta_u_upotrebi");
        db.VrsteTroskova.Remove(v);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }
}

/* ---------- Jednokratni troškovi ---------- */

public record SacuvajTrosakCommand(Guid? Id, DateOnly Datum, Guid VrstaId, decimal Iznos, string? Opis, string? RacunUrl)
    : IRequest<TrosakDto>;

public class SacuvajTrosakHandler(IAppDbContext db) : IRequestHandler<SacuvajTrosakCommand, TrosakDto>
{
    public async Task<TrosakDto> Handle(SacuvajTrosakCommand c, CancellationToken ct)
    {
        TroskoviPravila.ProvjeriIznos(c.Iznos);
        TroskoviPravila.ProvjeriMjesec(c.Datum.Year, c.Datum.Month);
        var vrsta = await TroskoviPravila.VrstaAsync(db, c.VrstaId, ct);

        var t = c.Id is Guid id
            ? await db.Troskovi.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Trošak nije pronađen.")
            : new Trosak();
        if (c.Id is null) db.Troskovi.Add(t);
        t.Datum = c.Datum;
        t.VrstaTroskaId = vrsta.Id;
        t.Vrsta = vrsta;
        t.Iznos = Math.Round(c.Iznos, 2);
        t.Opis = TroskoviPravila.Opis(c.Opis);
        t.RacunUrl = string.IsNullOrWhiteSpace(c.RacunUrl) ? null : c.RacunUrl.Trim();
        await db.SaveChangesAsync(ct);
        return TroskoviPravila.ToDto(t);
    }
}

public record ObrisiTrosakCommand(Guid Id) : IRequest<Guid>;

public class ObrisiTrosakHandler(IAppDbContext db) : IRequestHandler<ObrisiTrosakCommand, Guid>
{
    public async Task<Guid> Handle(ObrisiTrosakCommand c, CancellationToken ct)
    {
        var t = await db.Troskovi.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Trošak nije pronađen.");
        db.Troskovi.Remove(t);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }
}

/* ---------- Ponavljajući troškovi ---------- */

public record DodajPonavljajuciTrosakCommand(Guid VrstaId, decimal Iznos, string? Opis, int OdGodina, int OdMjesec)
    : IRequest<Guid>;

public class DodajPonavljajuciTrosakHandler(IAppDbContext db) : IRequestHandler<DodajPonavljajuciTrosakCommand, Guid>
{
    public async Task<Guid> Handle(DodajPonavljajuciTrosakCommand c, CancellationToken ct)
    {
        TroskoviPravila.ProvjeriIznos(c.Iznos);
        TroskoviPravila.ProvjeriMjesec(c.OdGodina, c.OdMjesec);
        var vrsta = await TroskoviPravila.VrstaAsync(db, c.VrstaId, ct);
        var p = new PonavljajuciTrosak
        {
            VrstaTroskaId = vrsta.Id, Iznos = Math.Round(c.Iznos, 2), Opis = TroskoviPravila.Opis(c.Opis),
            OdGodina = c.OdGodina, OdMjesec = c.OdMjesec,
        };
        db.PonavljajuciTroskovi.Add(p);
        await db.SaveChangesAsync(ct);
        return p.Id;
    }
}

/// <summary>
/// Zaustavlja ponavljajući trošak: posljednji mjesec u kom važi je mjesec PRIJE <paramref name="OdGodina"/>/
/// <paramref name="OdMjesec"/>. Zaustavljanje prije prvog mjeseca znači da nikad nije važio — red se briše.
/// </summary>
public record ZaustaviPonavljajuciTrosakCommand(Guid Id, int OdGodina, int OdMjesec) : IRequest<Guid>;

public class ZaustaviPonavljajuciTrosakHandler(IAppDbContext db) : IRequestHandler<ZaustaviPonavljajuciTrosakCommand, Guid>
{
    public async Task<Guid> Handle(ZaustaviPonavljajuciTrosakCommand c, CancellationToken ct)
    {
        TroskoviPravila.ProvjeriMjesec(c.OdGodina, c.OdMjesec);
        var p = await db.PonavljajuciTroskovi.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                ?? throw new NotFoundException("Ponavljajući trošak nije pronađen.");
        var (dg, dm) = c.OdMjesec == 1 ? (c.OdGodina - 1, 12) : (c.OdGodina, c.OdMjesec - 1);
        if (dg * 12 + dm < p.OdGodina * 12 + p.OdMjesec) db.PonavljajuciTroskovi.Remove(p);
        else (p.DoGodina, p.DoMjesec) = (dg, dm);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }
}

public record ObrisiPonavljajuciTrosakCommand(Guid Id) : IRequest<Guid>;

public class ObrisiPonavljajuciTrosakHandler(IAppDbContext db) : IRequestHandler<ObrisiPonavljajuciTrosakCommand, Guid>
{
    public async Task<Guid> Handle(ObrisiPonavljajuciTrosakCommand c, CancellationToken ct)
    {
        var p = await db.PonavljajuciTroskovi.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                ?? throw new NotFoundException("Ponavljajući trošak nije pronađen.");
        db.PonavljajuciTroskovi.Remove(p);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }
}

/* ---------- Cijene goriva ---------- */

public record DodajCijenuGorivaCommand(DateOnly VaziOd, decimal CijenaPoLitru, decimal PotrosnjaL100km) : IRequest<Guid>;

public class DodajCijenuGorivaHandler(IAppDbContext db) : IRequestHandler<DodajCijenuGorivaCommand, Guid>
{
    public async Task<Guid> Handle(DodajCijenuGorivaCommand c, CancellationToken ct)
    {
        if (c.CijenaPoLitru is <= 0 or > 20)
            throw new DomainException("Cijena goriva mora biti između 0 i 20 KM po litru.", "cijena_goriva_neispravna");
        if (c.PotrosnjaL100km is <= 0 or > 50)
            throw new DomainException("Potrošnja mora biti između 0 i 50 l/100 km.", "potrosnja_neispravna");
        var postojeca = await db.CijeneGoriva.FirstOrDefaultAsync(x => x.VaziOd == c.VaziOd, ct);
        var cijena = postojeca ?? new CijenaGoriva { VaziOd = c.VaziOd };
        if (postojeca is null) db.CijeneGoriva.Add(cijena);
        cijena.CijenaPoLitru = c.CijenaPoLitru;
        cijena.PotrosnjaL100km = c.PotrosnjaL100km;
        await db.SaveChangesAsync(ct);
        return cijena.Id;
    }
}

public record ObrisiCijenuGorivaCommand(Guid Id) : IRequest<Guid>;

public class ObrisiCijenuGorivaHandler(IAppDbContext db) : IRequestHandler<ObrisiCijenuGorivaCommand, Guid>
{
    public async Task<Guid> Handle(ObrisiCijenuGorivaCommand c, CancellationToken ct)
    {
        var x = await db.CijeneGoriva.FirstOrDefaultAsync(v => v.Id == c.Id, ct)
                ?? throw new NotFoundException("Cijena goriva nije pronađena.");
        db.CijeneGoriva.Remove(x);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }
}

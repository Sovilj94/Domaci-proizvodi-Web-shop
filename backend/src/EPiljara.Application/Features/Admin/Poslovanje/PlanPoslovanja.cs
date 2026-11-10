using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/*
 * Plan poslovanja (admin → Poslovanje → Plan): šta donosi mjesec sa N narudžbi.
 * Prosječna narudžba se uzima iz STVARNIH isporučenih narudžbi zadnja 3 puna mjeseca (koje korpice, koliko
 * Špajza, dostava, popusti), ali se preračunava po DANAŠNJIM cijenama: korpica po sadašnjoj cijeni (pretplatnik
 * po pretplatnoj), njena nabavna kao tipičan sadržaj u mjesecu plana (isti račun kao Obračun cijena) plus kutija;
 * Špajz po sadašnjoj cijeni i nabavnoj. Gorivo po narudžbi i ostali jednokratni troškovi su prosjek osnovnih
 * mjeseci iz analitike; ponavljajući troškovi su oni koji važe u mjesecu plana.
 */

/// <summary>Prosječna narudžba, po današnjim cijenama.</summary>
public sealed record PlanNarudzba(
    decimal Korpice, decimal NabavnoKorpica, decimal KutijeKorpica,
    decimal Spajz, decimal NabavnoSpajz, decimal AmbalazaSpajz,
    decimal Dostava, decimal Popusti, decimal Gorivo)
{
    public decimal Neto => Korpice + Spajz + Dostava - Popusti;
    public decimal Varijabilno => NabavnoKorpica + KutijeKorpica + NabavnoSpajz + AmbalazaSpajz + Gorivo;
    /// <summary>Koliko jedna narudžba ostavlja za fiksne troškove i dobit.</summary>
    public decimal Doprinos => Neto - Varijabilno;
}

public sealed record PlanScenarijDto(int Narudzbi, decimal Neto, decimal Nabavno, decimal Ambalaza, decimal Gorivo,
    decimal Varijabilno, decimal Fiksni, decimal Dobit, decimal? MarzaDobiti);

public static class PlanRacun
{
    public static PlanScenarijDto Scenarij(PlanNarudzba n, int narudzbi, decimal fiksni)
    {
        var neto = Math.Round(n.Neto * narudzbi, 2);
        var varijabilno = Math.Round(n.Varijabilno * narudzbi, 2);
        var dobit = neto - varijabilno - fiksni;
        return new PlanScenarijDto(narudzbi, neto,
            Math.Round((n.NabavnoKorpica + n.NabavnoSpajz) * narudzbi, 2),
            Math.Round((n.KutijeKorpica + n.AmbalazaSpajz) * narudzbi, 2),
            Math.Round(n.Gorivo * narudzbi, 2),
            varijabilno, fiksni, dobit, neto == 0 ? null : Math.Round(dobit / neto, 4));
    }

    /// <summary>Najmanje narudžbi mjesečno da se pokriju fiksni troškovi; null kad narudžba ne pokriva ni svoje troškove.</summary>
    public static int? Prag(PlanNarudzba n, decimal fiksni) =>
        n.Doprinos <= 0 ? null : (int)Math.Ceiling(fiksni / n.Doprinos);
}

public record PlanKorpiceDto(string Naziv, decimal NaNarudzbu, decimal Cijena, decimal Nabavno, decimal Kutija, decimal Zarada);

public record PlanFiksniDto(string Vrsta, decimal Iznos, bool Ponavljajuci);

public record PlanOsnovaMjesecaDto(int Godina, int Mjesec, int Narudzbi, decimal Neto, decimal Dobit);

public record PlanPoslovanjaDto
{
    public int Narudzbi { get; init; }
    public int Godina { get; init; }
    public int Mjesec { get; init; }
    public PlanNarudzba Prosjek { get; init; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public decimal ProsjekNeto { get; init; }
    public decimal ProsjekDoprinos { get; init; }
    public PlanScenarijDto Plan { get; init; } = new(0, 0, 0, 0, 0, 0, 0, 0, null);
    public IReadOnlyList<PlanScenarijDto> Scenariji { get; init; } = [];
    public int? Prag { get; init; }
    public decimal Fiksni { get; init; }
    public IReadOnlyList<PlanFiksniDto> FiksniStavke { get; init; } = [];
    public IReadOnlyList<PlanKorpiceDto> Korpice { get; init; } = [];
    public IReadOnlyList<PlanOsnovaMjesecaDto> Osnova { get; init; } = [];
    public int NarudzbiOsnova { get; init; }
    /// <summary>Koliko narudžbi sedmično može dostaviti (vozači × dani dostave × stanica po danu).</summary>
    public int KapacitetSedmicno { get; init; }
    public decimal NarudzbiSedmicno { get; init; }
}

/// <param name="Narudzbi">Narudžbi mjesečno (podrazumijevano 200).</param>
/// <param name="Godina">Mjesec plana; podrazumijevano sljedeći mjesec.</param>
/// <param name="Dodatno">Mjesečni troškovi koji nisu u Troškovima (plate, doprinosi…), samo za ovaj plan.</param>
public record GetPlanPoslovanjaQuery(int Narudzbi = 200, int? Godina = null, int? Mjesec = null, decimal Dodatno = 0m) : IRequest<PlanPoslovanjaDto>;

public class GetPlanPoslovanjaValidator : AbstractValidator<GetPlanPoslovanjaQuery>
{
    public GetPlanPoslovanjaValidator()
    {
        RuleFor(x => x.Narudzbi).InclusiveBetween(1, 100_000).WithMessage("Broj narudžbi mora biti od 1 do 100.000.");
        RuleFor(x => x.Mjesec).InclusiveBetween(1, 12).When(x => x.Mjesec is not null).WithMessage("Mjesec mora biti od 1 do 12.");
        RuleFor(x => x.Dodatno).InclusiveBetween(0m, 1_000_000m).WithMessage("Dodatni troškovi moraju biti od 0 do 1.000.000 KM.");
    }
}

public class GetPlanPoslovanjaHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije, IOptions<PretplateOpcije> pretplate)
    : IRequestHandler<GetPlanPoslovanjaQuery, PlanPoslovanjaDto>
{
    public async Task<PlanPoslovanjaDto> Handle(GetPlanPoslovanjaQuery q, CancellationToken ct)
    {
        var danas = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SedmicaHelper.Zona);
        var tekuci = new DateTime(danas.Year, danas.Month, 1);
        var plan = q.Mjesec is int pm ? new DateTime(q.Godina ?? danas.Year, pm, 1) : tekuci.AddMonths(1);

        // Osnova: zadnja 3 puna mjeseca, iz analitike (narudžbe, neto, dobit, gorivo, ostali troškovi).
        var osnova = new List<(DateTime M, PoslovanjeMjesecaDto D)>();
        for (var i = 3; i >= 1; i--)
        {
            var m = tekuci.AddMonths(-i);
            osnova.Add((m, await new GetPoslovanjeHandler(db, opcije).Handle(new GetPoslovanjeQuery(m.Year, m.Month), ct)));
        }
        var narudzbiOsnova = osnova.Sum(x => x.D.Prihod.BrojNarudzbi);

        // Stavke isporučenih narudžbi osnove, po mjesecu isporuke (isto kao analitika: ISO sedmica → mjesec).
        var od = DateOnly.FromDateTime(tekuci.AddMonths(-3));
        var doIskljucivo = DateOnly.FromDateTime(tekuci);
        var narudzbe = (await db.Orders.AsNoTracking().Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Delivered && o.Godina >= od.Year - 1)
                .ToListAsync(ct))
            .Where(o =>
            {
                var d = DateOnly.FromDateTime(SedmicaHelper.DatumIsporuke(o.Godina, o.Sedmica, DayOfWeek.Thursday, "12:00"));
                return d >= od && d < doIskljucivo;
            })
            .ToList();
        var n = Math.Max(narudzbe.Count, 1);

        var katalog = await db.Products.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var planovi = await db.Pretplate.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Plan, ct);

        // Nabavno korpice = tipičan sadržaj mjeseca plana (marža i popust ne utiču na nabavno).
        var obracun = await new GetObracunCijenaKorpicaHandler(db, opcije)
            .Handle(new GetObracunCijenaKorpicaQuery(0.35m, 0.10m, plan.Month), ct);
        var poKorpici = obracun.Korpice.ToDictionary(k => k.Id);

        decimal korpice = 0, nabKorpica = 0, kutije = 0, spajz = 0, nabSpajz = 0, ambSpajz = 0;
        var udio = new Dictionary<Guid, decimal>();
        foreach (var o in narudzbe)
        foreach (var i in o.Items)
        {
            if (!katalog.TryGetValue(i.ProductId, out var p)) continue;
            if (p.Vrsta == VrstaProizvoda.Korpica)
            {
                var cijena = o.PretplataId is Guid pid && planovi.TryGetValue(pid, out var plan2)
                    ? PretplatnaCijena.Izracunaj(p.Price, plan2, pretplate.Value)
                    : p.Price;
                korpice += cijena * i.Quantity;
                if (poKorpici.TryGetValue(p.Id, out var ok))
                {
                    nabKorpica += ok.Izabrani.Nabavno * i.Quantity;
                    kutije += ok.Ambalaza * i.Quantity;
                }
                udio[p.Id] = udio.GetValueOrDefault(p.Id) + i.Quantity;
            }
            else
            {
                spajz += p.Price * i.Quantity;
                nabSpajz += (p.NabavnaCijena ?? i.NabavnaCijena ?? 0m) * i.Quantity;
                ambSpajz += (p.AmbalazaCijena ?? i.AmbalazaCijena ?? 0m) * i.Quantity;
            }
        }

        var gorivo = narudzbiOsnova == 0 ? 0m : osnova.Sum(x => x.D.Troskovi.Gorivo.UDobiti) / narudzbiOsnova;
        var prosjek = new PlanNarudzba(
            R(korpice / n), R(nabKorpica / n), R(kutije / n), R(spajz / n), R(nabSpajz / n), R(ambSpajz / n),
            R(narudzbe.Sum(o => o.DeliveryFee) / n), R(narudzbe.Sum(o => o.Popust + o.PoeniPopust) / n), R(gorivo));

        // Fiksni: ponavljajući koji važe u mjesecu plana + prosjek jednokratnih (bez goriva) iz osnove.
        var ponavljajuci = (await db.PonavljajuciTroskovi.AsNoTracking().Include(t => t.Vrsta).ToListAsync(ct))
            .Where(t => t.VaziU(plan.Year, plan.Month) && !(t.Vrsta?.JeGorivo ?? false))
            .GroupBy(t => t.Vrsta?.Naziv ?? "Ostalo")
            .Select(g => new PlanFiksniDto(g.Key, g.Sum(t => t.Iznos), true));
        var jednokratni = osnova.SelectMany(x => x.D.Troskovi.PoVrsti.Where(v => !v.JeGorivo && v.Jednokratni > 0))
            .GroupBy(v => v.Vrsta)
            .Select(g => new PlanFiksniDto(g.Key, R(g.Sum(v => v.Jednokratni) / osnova.Count), false));
        var dodatno = q.Dodatno > 0 ? new[] { new PlanFiksniDto("Plate i ostalo (uneseno u planu)", R(q.Dodatno), true) } : [];
        var fiksniStavke = ponavljajuci.Concat(jednokratni).Concat(dodatno).OrderByDescending(f => f.Iznos).ToList();
        var fiksni = fiksniStavke.Sum(f => f.Iznos);

        var brojevi = new[] { 100, 150, 200, 250, 300, q.Narudzbi }.Distinct().Order();

        // Kapacitet: aktivni vozači × dani dostave u sedmici × stanica po vozaču dnevno.
        var vozaca = await db.Drivers.CountAsync(d => d.Aktivan, ct);
        var dana = (await db.DeliverySlots.AsNoTracking().Select(s => s.DayOfWeek).ToListAsync(ct)).Distinct().Count();

        return new PlanPoslovanjaDto
        {
            Narudzbi = q.Narudzbi, Godina = plan.Year, Mjesec = plan.Month,
            Prosjek = prosjek, ProsjekNeto = prosjek.Neto, ProsjekDoprinos = prosjek.Doprinos,
            Plan = PlanRacun.Scenarij(prosjek, q.Narudzbi, fiksni),
            Scenariji = brojevi.Select(b => PlanRacun.Scenarij(prosjek, b, fiksni)).ToList(),
            Prag = PlanRacun.Prag(prosjek, fiksni),
            Fiksni = fiksni, FiksniStavke = fiksniStavke,
            Korpice = udio.OrderByDescending(x => x.Value).Select(x =>
            {
                var p = katalog[x.Key];
                var ok = poKorpici.GetValueOrDefault(x.Key);
                var nab = ok?.Izabrani.Nabavno ?? 0m;
                var kut = ok?.Ambalaza ?? 0m;
                return new PlanKorpiceDto(p.Name, R(x.Value / n), p.Price, nab, kut, R(p.Price - nab - kut));
            }).ToList(),
            Osnova = osnova.Select(x => new PlanOsnovaMjesecaDto(x.M.Year, x.M.Month, x.D.Prihod.BrojNarudzbi, x.D.Prihod.Neto, x.D.Troskovi.Dobit)).ToList(),
            NarudzbiOsnova = narudzbe.Count,
            KapacitetSedmicno = vozaca * dana * Domain.Entities.Driver.MaxStanicaPoDanu,
            NarudzbiSedmicno = Math.Round(q.Narudzbi * 12m / 52m, 1),
        };
    }

    private static decimal R(decimal x) => Math.Round(x, 2);
}

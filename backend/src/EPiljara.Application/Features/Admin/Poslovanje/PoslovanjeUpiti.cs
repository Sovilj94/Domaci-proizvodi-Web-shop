using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>Analitika mjeseca. Bez parametara: tekući mjesec (po Sarajevu).</summary>
public record GetPoslovanjeQuery(int? Godina = null, int? Mjesec = null) : IRequest<PoslovanjeMjesecaDto>;

public class GetPoslovanjeHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetPoslovanjeQuery, PoslovanjeMjesecaDto>
{
    public async Task<PoslovanjeMjesecaDto> Handle(GetPoslovanjeQuery q, CancellationToken ct)
    {
        var sada = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SedmicaHelper.Zona);
        var godina = q.Godina ?? sada.Year;
        var mjesec = q.Mjesec ?? sada.Month;
        if (mjesec is < 1 or > 12)
            throw new DomainException("Mjesec mora biti od 1 do 12.", "mjesec_neispravan");

        var (dan, _, _) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var (pg, pm) = PoslovanjeKalendar.Prethodni(godina, mjesec);
        var ove = PoslovanjeKalendar.SedmiceMjeseca(godina, mjesec, dan);
        var prosle = PoslovanjeKalendar.SedmiceMjeseca(pg, pm, dan);

        var narudzbe = await PoslovanjePodaci.NarudzbeAsync(db, [.. ove, .. prosle], ct);
        var uMjesecu = narudzbe.Where(o => ove.Contains((o.Godina, o.Sedmica))).ToList();
        var uProslom = narudzbe.Where(o => prosle.Contains((o.Godina, o.Sedmica))).ToList();
        var isporucene = PoslovanjeRacun.Isporucene(uMjesecu).ToList();
        var prosleIsporucene = PoslovanjeRacun.Isporucene(uProslom).ToList();

        var racun = new PoslovanjeRacun(await PoslovanjePodaci.KatalogAsync(db, ct));
        var prihod = racun.Prihod(isporucene);
        var prethodni = racun.Prihod(prosleIsporucene);
        var troskovi = await PoslovanjePodaci.TroskoviAsync(
            db, new DateOnly(pg, pm, 1), new DateOnly(godina, mjesec, 1).AddMonths(1), ct);
        return new PoslovanjeMjesecaDto
        {
            Godina = godina,
            Mjesec = mjesec,
            Prihod = prihod,
            Prethodni = prethodni,
            Troskovi = racun.Troskovi(godina, mjesec, isporucene, prihod, troskovi),
            PrethodniTroskovi = racun.Troskovi(pg, pm, prosleIsporucene, prethodni, troskovi),
            Kupci = racun.Kupci(
                isporucene, prosleIsporucene,
                await PoslovanjePodaci.PrviMjeseciAsync(db, dan, ct),
                PoslovanjeKalendar.Indeks(godina, mjesec),
                await PoslovanjePodaci.PreporuceniAsync(db, ct)),
            Prodaja = racun.Prodaja(isporucene),
            Dostava = racun.Dostava(uMjesecu),
            Gazdinstva = racun.Gazdinstva(isporucene),
            Pretplate = PoslovanjeRacun.Pretplate(
                await db.Pretplate.AsNoTracking().ToListAsync(ct), godina, mjesec,
                tekuci: godina == sada.Year && mjesec == sada.Month, isporucene),
        };
    }
}

/// <summary>Godišnji pregled: glavni brojevi po mjesecima + prihod po kategoriji.</summary>
public record GetPoslovanjeGodinaQuery(int? Godina = null) : IRequest<PoslovanjeGodineDto>;

public class GetPoslovanjeGodinaHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetPoslovanjeGodinaQuery, PoslovanjeGodineDto>
{
    public async Task<PoslovanjeGodineDto> Handle(GetPoslovanjeGodinaQuery q, CancellationToken ct)
    {
        var sada = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SedmicaHelper.Zona);
        var godina = q.Godina ?? sada.Year;
        var (dan, _, _) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var sedmicePoMjesecu = Enumerable.Range(1, 12)
            .ToDictionary(m => m, m => PoslovanjeKalendar.SedmiceMjeseca(godina, m, dan));
        var narudzbe = await PoslovanjePodaci.NarudzbeAsync(db, sedmicePoMjesecu.Values.SelectMany(s => s).ToList(), ct);
        var racun = new PoslovanjeRacun(await PoslovanjePodaci.KatalogAsync(db, ct));
        var pretplate = await db.Pretplate.AsNoTracking().ToListAsync(ct);
        var troskovi = await PoslovanjePodaci.TroskoviAsync(db, new DateOnly(godina, 1, 1), new DateOnly(godina + 1, 1, 1), ct);

        var mjeseci = new List<MjesecGodineDto>();
        var sezona = new Dictionary<string, decimal[]>();
        for (var m = 1; m <= 12; m++)
        {
            var sedmice = sedmicePoMjesecu[m];
            var isporucene = PoslovanjeRacun.Isporucene(narudzbe.Where(o => sedmice.Contains((o.Godina, o.Sedmica)))).ToList();
            var prihod = racun.Prihod(isporucene);
            var t = racun.Troskovi(godina, m, isporucene, prihod, troskovi);
            var (sg, sm) = m == 12 ? (godina + 1, 1) : (godina, m + 1);
            mjeseci.Add(new MjesecGodineDto
            {
                Mjesec = m,
                Neto = prihod.Neto,
                Zarada = prihod.Zarada,
                Marza = prihod.Marza,
                Narudzbi = prihod.BrojNarudzbi,
                AktivniKupci = isporucene
                    .Select(o => PoslovanjeRacun.KljucKupca(o.CustomerId, o.CustomerEmail, o.CustomerPhone, o.Id))
                    .Distinct().Count(),
                AktivnePretplate = PoslovanjeKalendar.Indeks(godina, m) > PoslovanjeKalendar.Indeks(sada.Year, sada.Month)
                    ? null
                    : PoslovanjeRacun.AktivnePretplate(pretplate, PoslovanjeKalendar.PocetakUtc(sg, sm)),
                Procjena = prihod.Procjena,
                Troskovi = t.Ukupno,
                Dobit = t.Dobit,
            });
            foreach (var l in isporucene.SelectMany(racun.Linije))
            {
                var kat = racun.KategorijaArtikla(l.ProductId);
                if (!sezona.TryGetValue(kat, out var niz)) sezona[kat] = niz = new decimal[12];
                niz[m - 1] += l.Prihod;
            }
        }

        return new PoslovanjeGodineDto
        {
            Godina = godina,
            Mjeseci = mjeseci,
            Sezona = sezona
                .Select(kv => new SezonaKategorijeDto { Kategorija = kv.Key, Mjeseci = kv.Value.Select(x => Math.Round(x, 2)).ToList() })
                .OrderByDescending(s => s.Mjeseci.Sum()).ThenBy(s => s.Kategorija)
                .ToList(),
        };
    }
}

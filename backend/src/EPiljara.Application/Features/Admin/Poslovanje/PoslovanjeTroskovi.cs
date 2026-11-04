using EPiljara.Application.DTOs;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>
/// Sirovi troškovi za opseg mjeseci: ručni (sa vrstom), ponavljajući (sa vrstom), rute (sa stanicama i
/// narudžbama) i cijene goriva. Račun po mjesecu filtrira sam.
/// </summary>
public sealed record TroskoviPodaci(
    IReadOnlyList<Trosak> Troskovi,
    IReadOnlyList<PonavljajuciTrosak> Ponavljajuci,
    IReadOnlyList<DeliveryRoute> Rute,
    IReadOnlyList<CijenaGoriva> CijeneGoriva)
{
    public static readonly TroskoviPodaci Prazno = new([], [], [], []);
}

public sealed partial class PoslovanjeRacun
{
    /// <summary>Vozilo je izašlo na rutu ako je bar jedna narudžba isporučena ili neuspjela.</summary>
    private static bool Vozena(DeliveryRoute r) =>
        r.Stops.Any(s => s.Order?.Status is OrderStatus.Delivered or OrderStatus.FailedDelivery);

    /// <summary>Troškovi i dobit kalendarskog mjeseca (spec 2026-11-04-troskovi-design.md).</summary>
    public TroskoviDto Troskovi(int godina, int mjesec, IReadOnlyCollection<Order> isporucene, PrihodDto prihod, TroskoviPodaci t)
    {
        // Ambalaža: snimak po stavci; stara narudžba bez snimka → današnja ambalaža artikla (procjena).
        decimal ambalaza = 0m;
        var procjena = false;
        foreach (var i in isporucene.SelectMany(o => o.Items))
        {
            var a = i.AmbalazaCijena;
            if (a is null && Katalog.TryGetValue(i.ProductId, out var p) && p.AmbalazaCijena is decimal danas)
            {
                a = danas;
                procjena = true;
            }
            ambalaza += (a ?? 0m) * i.Quantity;
        }
        ambalaza = Math.Round(ambalaza, 2);

        // Gorivo po rutama: km cijele ture × potrošnja × cijena važeća na dan rute.
        var rute = t.Rute.Where(r => r.Date.Year == godina && r.Date.Month == mjesec && Vozena(r)).ToList();
        var cijene = t.CijeneGoriva.OrderBy(c => c.VaziOd).ToList();
        decimal poRutama = 0m;
        var bezCijene = false;
        var saCijenom = 0;
        double km = 0;
        foreach (var r in rute)
        {
            var kmRute = (r.KilometrazaMetara ?? r.DuzinaMetara ?? 0) / 1000.0;
            km += kmRute;
            var c = cijene.LastOrDefault(x => x.VaziOd <= r.Date);
            if (c is null) { bezCijene = true; continue; }
            saCijenom++;
            poRutama += (decimal)kmRute * c.PotrosnjaL100km / 100m * c.CijenaPoLitru;
        }

        // Ručni: jednokratni u mjesecu + ponavljajući koji važe u mjesecu, po vrsti.
        var jednokratni = t.Troskovi.Where(x => x.Datum.Year == godina && x.Datum.Month == mjesec)
            .Select(x => (Vrsta: x.Vrsta!, Jednokratni: x.Iznos, Ponavljajuci: 0m));
        var ponavljajuci = t.Ponavljajuci.Where(x => x.VaziU(godina, mjesec))
            .Select(x => (Vrsta: x.Vrsta!, Jednokratni: 0m, Ponavljajuci: x.Iznos));
        var poVrsti = jednokratni.Concat(ponavljajuci)
            .GroupBy(x => x.Vrsta.Id)
            .Select(g =>
            {
                var v = g.First().Vrsta;
                var j = g.Sum(x => x.Jednokratni);
                var pt = g.Sum(x => x.Ponavljajuci);
                return (v.Redoslijed, Dto: new VrstaTroskaMjesecaDto
                {
                    Vrsta = v.Naziv, JeGorivo = v.JeGorivo, Jednokratni = j, Ponavljajuci = pt, Iznos = j + pt,
                });
            })
            .OrderBy(x => x.Redoslijed).ThenByDescending(x => x.Dto.Iznos).ThenBy(x => x.Dto.Vrsta)
            .Select(x => x.Dto)
            .ToList();

        var poRacunima = poVrsti.Where(v => v.JeGorivo).Sum(v => v.Iznos);
        var imaRacuna = t.Troskovi.Any(x => x.Datum.Year == godina && x.Datum.Month == mjesec && x.Vrsta!.JeGorivo)
                        || t.Ponavljajuci.Any(x => x.VaziU(godina, mjesec) && x.Vrsta!.JeGorivo);
        decimal? rutama = rute.Count > 0 && saCijenom == 0 ? null : Math.Round(poRutama, 2);
        var gorivoUDobiti = imaRacuna ? poRacunima : rutama ?? 0m;
        var ostali = poVrsti.Where(v => !v.JeGorivo).Sum(v => v.Iznos);
        var ukupno = Math.Round(ambalaza + gorivoUDobiti + ostali, 2);
        var dobit = Math.Round(prihod.Neto - prihod.Nabavno - ukupno, 2);
        var n = isporucene.Count;

        return new TroskoviDto
        {
            Ambalaza = ambalaza,
            AmbalazaProcjena = procjena,
            Gorivo = new GorivoDto
            {
                Km = Math.Round(km, 1),
                BrojRuta = rute.Count,
                PoRutama = rutama,
                PoRacunima = poRacunima,
                Izvor = imaRacuna ? "racuni" : "rute",
                UDobiti = gorivoUDobiti,
                BezCijene = bezCijene,
            },
            PoVrsti = poVrsti,
            Ostali = ostali,
            Ukupno = ukupno,
            Dobit = dobit,
            MarzaDobiti = prihod.Neto == 0 ? null : Udio(dobit, prihod.Neto),
            VarijabilniPoNarudzbi = n == 0 ? 0m : Math.Round((ambalaza + gorivoUDobiti) / n, 2),
        };
    }
}

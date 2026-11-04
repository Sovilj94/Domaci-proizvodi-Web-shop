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
    IReadOnlyList<CijenaGoriva> CijeneGoriva,
    IReadOnlyList<DnevnaVoznja>? Voznje = null)
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

        // Gorivo po vozaču i danu: jedna dnevna tura (magacin → sve stanice → magacin) × potrošnja ×
        // cijena važeća tog dana. Iznos sa pumpe koji je vozač unio zamjenjuje procjenu za taj dan.
        // Dan bez reda DnevnaVoznja (podaci od prije dnevnih procjena) sabira kilometražu svojih ruta.
        var rute = t.Rute.Where(r => r.Date.Year == godina && r.Date.Month == mjesec && Vozena(r)).ToList();
        var cijene = t.CijeneGoriva.OrderBy(c => c.VaziOd).ToList();
        var voznje = (t.Voznje ?? []).ToDictionary(v => (v.DriverId, v.Datum));
        decimal poRutama = 0m;
        decimal procijenjeno = 0m;
        decimal placenoVozaci = 0m;
        var danaSaUnosom = 0;
        var bezCijene = false;
        var saIznosom = 0;
        double km = 0;
        var dani = rute.GroupBy(r => (r.DriverId, r.Date)).ToList();
        foreach (var dan in dani)
        {
            voznje.TryGetValue(dan.Key, out var v);
            var kmDana = (v?.ProcjenaMetara ?? dan.Sum(r => r.KilometrazaMetara ?? r.DuzinaMetara ?? 0)) / 1000.0;
            km += kmDana;
            var c = cijene.LastOrDefault(x => x.VaziOd <= dan.Key.Date);
            decimal? procjenaDana = c is null ? null : (decimal)kmDana * c.PotrosnjaL100km / 100m * c.CijenaPoLitru;
            if (procjenaDana is decimal p) procijenjeno += p;
            if (v?.PlacenoGorivo is decimal placeno)
            {
                danaSaUnosom++;
                placenoVozaci += placeno;
                poRutama += placeno;
                saIznosom++;
            }
            else if (procjenaDana is decimal pr)
            {
                poRutama += pr;
                saIznosom++;
            }
            else bezCijene = true;
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
        decimal? rutama = dani.Count > 0 && saIznosom == 0 ? null : Math.Round(poRutama, 2);
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
                BrojDana = dani.Count,
                PoRutama = rutama,
                Procijenjeno = Math.Round(procijenjeno, 2),
                PlacenoVozaci = Math.Round(placenoVozaci, 2),
                DanaSaUnosom = danaSaUnosom,
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

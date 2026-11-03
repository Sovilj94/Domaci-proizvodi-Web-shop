using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>
/// Sav račun analitike, nad narudžbama u memoriji — bez baze, da se pravila testiraju direktno.
/// Pravila su u specifikaciji (docs/superpowers/specs/2026-11-03-poslovanje-analitika-design.md).
/// </summary>
public sealed class PoslovanjeRacun(IReadOnlyDictionary<Guid, Product> katalog)
{
    /// <summary>Jedan red robe: stavka Špajza ili red sadržaja korpice (pomnožen brojem kutija).</summary>
    internal sealed record Linija(
        Order Narudzba, Guid ProductId, string Naziv, string Jedinica, decimal Kolicina,
        decimal Prihod, decimal? NabavnaJedinicna, bool Procjena, bool IzKorpice)
    {
        public decimal? Nabavno => NabavnaJedinicna is decimal c ? Kolicina * c : null;
    }

    public static decimal Neto(Order o) => o.Subtotal + o.DeliveryFee - o.Popust - o.PoeniPopust;

    public static IEnumerable<Order> Isporucene(IEnumerable<Order> narudzbe) =>
        narudzbe.Where(o => o.Status == OrderStatus.Delivered);

    internal static bool JeKorpica(OrderItem i, IReadOnlyDictionary<Guid, Product> katalog) =>
        i.KorpicaStavke.Count > 0
        || (katalog.TryGetValue(i.ProductId, out var p) && p.Vrsta == VrstaProizvoda.Korpica);

    internal IEnumerable<Linija> Linije(Order o)
    {
        foreach (var i in o.Items)
        {
            if (JeKorpica(i, katalog))
            {
                foreach (var s in i.KorpicaStavke)
                {
                    var (nabavna, procjena) = Nabavna(s.NabavnaCijena, s.ProductId);
                    var kol = s.Kolicina * i.Quantity;
                    yield return new Linija(o, s.ProductId, s.ProductName, s.Unit.ToApi(), kol,
                        kol * s.JedinicnaCijena, nabavna, procjena, IzKorpice: true);
                }
            }
            else
            {
                var (nabavna, procjena) = Nabavna(i.NabavnaCijena, i.ProductId);
                yield return new Linija(o, i.ProductId, i.ProductName, i.Unit.ToApi(), i.Quantity,
                    i.Quantity * i.UnitPrice, nabavna, procjena, IzKorpice: false);
            }
        }
    }

    private (decimal? Cijena, bool Procjena) Nabavna(decimal? snimak, Guid productId)
    {
        if (snimak is decimal s) return (s, false);
        return katalog.TryGetValue(productId, out var p) && p.NabavnaCijena is decimal c ? (c, true) : (null, false);
    }

    internal static decimal? Udio(decimal dio, decimal cijelo, int decimala = 4) =>
        cijelo == 0 ? null : Math.Round(dio / cijelo, decimala);

    /// <summary>Registrovan kupac po nalogu; gost po emailu, pa telefonu, pa (bez kontakta) po narudžbi.</summary>
    public static string KljucKupca(Guid? customerId, string? email, string? telefon, Guid orderId)
    {
        if (customerId is Guid id) return id.ToString();
        var e = email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(e)) return "g:" + e;
        var t = new string((telefon ?? string.Empty).Where(char.IsDigit).ToArray());
        return t.Length > 0 ? "g:" + t : "g:" + orderId;
    }

    private static string Kljuc(Order o) => KljucKupca(o.CustomerId, o.CustomerEmail, o.CustomerPhone, o.Id);

    public KupciDto Kupci(
        IReadOnlyCollection<Order> isporucene, IReadOnlyCollection<Order> prosleIsporucene,
        IReadOnlyDictionary<string, int> prviMjesec, int indeksMjeseca, IReadOnlySet<Guid> preporuceni)
    {
        var poKupcu = isporucene.GroupBy(Kljuc).ToList();
        var aktivni = poKupcu.Select(g => g.Key).ToHashSet();
        var prosli = prosleIsporucene.Select(Kljuc).ToHashSet();
        var novi = aktivni.Where(k => prviMjesec.TryGetValue(k, out var m) && m == indeksMjeseca).ToHashSet();
        var neto = isporucene.Sum(Neto);
        return new KupciDto
        {
            Aktivni = aktivni.Count,
            AktivniPrethodni = prosli.Count,
            Novi = novi.Count,
            Povratni = aktivni.Count - novi.Count,
            Zadrzavanje = Udio(prosli.Count(aktivni.Contains), prosli.Count),
            PotrosnjaPoKupcu = aktivni.Count == 0 ? 0m : Math.Round(neto / aktivni.Count, 2),
            NarudzbiPoKupcu = aktivni.Count == 0 ? 0m : Math.Round((decimal)isporucene.Count / aktivni.Count, 2),
            Gosti = aktivni.Count(k => k.StartsWith("g:")),
            Registrovani = aktivni.Count(k => !k.StartsWith("g:")),
            NoviPrekoPreporuke = novi.Count(k => Guid.TryParse(k, out var id) && preporuceni.Contains(id)),
            TopKupci = poKupcu
                .Select(g => new KupacAnalitikeDto
                {
                    Ime = g.OrderByDescending(o => o.CreatedAt).First().CustomerName,
                    Gost = g.Key.StartsWith("g:"),
                    Narudzbi = g.Count(),
                    Neto = g.Sum(Neto),
                })
                .OrderByDescending(k => k.Neto).ThenBy(k => k.Ime)
                .Take(10)
                .ToList(),
        };
    }

    public ProdajaDto Prodaja(IReadOnlyCollection<Order> isporucene)
    {
        var korpice = isporucene
            .SelectMany(o => o.Items.Where(i => JeKorpica(i, katalog)))
            .GroupBy(i => i.ProductId)
            .Select(g =>
            {
                katalog.TryGetValue(g.Key, out var p);
                var prihod = g.Sum(i => i.Quantity * i.UnitPrice);
                var nabavno = g.SelectMany(i => i.KorpicaStavke.Select(s =>
                    (Nabavna(s.NabavnaCijena, s.ProductId).Cijena ?? 0m) * s.Kolicina * i.Quantity)).Sum();
                return new KorpicaProdajeDto
                {
                    Naziv = p?.Name ?? g.First().ProductName,
                    Tip = p?.TipKorpice is { } t ? SedmicaHelper.Slug(t) : string.Empty,
                    Velicina = p?.Velicina?.ToString() ?? string.Empty,
                    Komada = g.Sum(i => i.Quantity),
                    Prihod = prihod,
                    Nabavno = Math.Round(nabavno, 2),
                    Marza = Udio(prihod - nabavno, prihod),
                };
            })
            .OrderByDescending(k => k.Prihod)
            .ToList();

        var spajz = isporucene.SelectMany(Linije)
            .Where(l => !l.IzKorpice)
            .GroupBy(l => KategorijaArtikla(l.ProductId))
            .Select(g =>
            {
                var prihod = g.Sum(l => l.Prihod);
                var nabavno = g.Sum(l => l.Nabavno ?? 0m);
                return new KategorijaProdajeDto
                {
                    Kategorija = g.Key, Prihod = prihod, Nabavno = Math.Round(nabavno, 2), Marza = Udio(prihod - nabavno, prihod),
                };
            })
            .OrderByDescending(k => k.Prihod)
            .ToList();

        var artikli = isporucene.SelectMany(Linije)
            .GroupBy(l => l.ProductId)
            .Select(g => new ArtikalProdajeDto
            {
                Naziv = katalog.TryGetValue(g.Key, out var p) ? p.Name : g.First().Naziv,
                Jedinica = g.First().Jedinica,
                Kolicina = g.Sum(l => l.Kolicina),
                Prihod = Math.Round(g.Sum(l => l.Prihod), 2),
                Zarada = Math.Round(g.Sum(l => l.Prihod - (l.Nabavno ?? 0m)), 2),
            })
            .ToList();
        var top = artikli.OrderByDescending(a => a.Prihod).ThenBy(a => a.Naziv).Take(10).ToList();
        var najslabiji = artikli.Except(top).OrderBy(a => a.Prihod).ThenBy(a => a.Naziv).Take(10).ToList();

        return new ProdajaDto { Korpice = korpice, Spajz = spajz, TopArtikli = top, NajslabijiArtikli = najslabiji };
    }

    internal string KategorijaArtikla(Guid productId) =>
        katalog.TryGetValue(productId, out var p) && p.Category is { } c ? c.Name : "Bez kategorije";

    public IReadOnlyList<GazdinstvoDto> Gazdinstva(IReadOnlyCollection<Order> isporucene) =>
        isporucene.SelectMany(Linije)
            .GroupBy(l => katalog.TryGetValue(l.ProductId, out var p) && p.Producer is { } pr
                ? (pr.Name, pr.Village) : ("Nepoznat proizvođač", string.Empty))
            .Select(g => new GazdinstvoDto
            {
                Naziv = g.Key.Item1,
                Selo = g.Key.Item2,
                Nabavno = Math.Round(g.Sum(l => l.Nabavno ?? 0m), 2),
                Artikli = g.GroupBy(l => (l.ProductId, l.Jedinica))
                    .Select(a => new ArtikalGazdinstvaDto
                    {
                        Naziv = katalog.TryGetValue(a.Key.ProductId, out var p) ? p.Name : a.First().Naziv,
                        Jedinica = a.Key.Jedinica,
                        Kolicina = a.Sum(l => l.Kolicina),
                        Nabavno = Math.Round(a.Sum(l => l.Nabavno ?? 0m), 2),
                    })
                    .OrderByDescending(a => a.Nabavno).ThenBy(a => a.Naziv)
                    .ToList(),
            })
            .OrderByDescending(g => g.Nabavno).ThenBy(g => g.Naziv)
            .ToList();

    public DostavaAnalitikaDto Dostava(IReadOnlyCollection<Order> sveUMjesecu)
    {
        var isporucene = Isporucene(sveUMjesecu).ToList();
        var neuspjele = sveUMjesecu.Count(o => o.Status == OrderStatus.FailedDelivery);
        var otkazane = sveUMjesecu.Where(o => o.Status == OrderStatus.Cancelled).ToList();
        var ocjene = isporucene.Where(o => o.Ocjena is not null).Select(o => o.Ocjena!.Ocjena).ToList();
        return new DostavaAnalitikaDto
        {
            PoZoni = isporucene
                .GroupBy(o => o.DeliveryZone?.Name ?? "Bez zone")
                .Select(g => new ZonaAnalitikeDto { Zona = g.Key, Narudzbi = g.Count(), Neto = g.Sum(Neto) })
                .OrderByDescending(z => z.Narudzbi).ThenByDescending(z => z.Neto).ThenBy(z => z.Zona)
                .ToList(),
            Isporucene = isporucene.Count,
            Neuspjele = neuspjele,
            Uspjesnost = Udio(isporucene.Count, isporucene.Count + neuspjele),
            Otkazane = otkazane.Count,
            OtkazaneVrijednost = otkazane.Sum(o => o.TotalAmount),
            OtkazaneUdio = Udio(otkazane.Count, sveUMjesecu.Count),
            ProsjecnaOcjena = ocjene.Count == 0 ? null : Math.Round((decimal)ocjene.Average(), 2),
            BrojOcjena = ocjene.Count,
        };
    }

    public static int AktivnePretplate(IEnumerable<Pretplata> sve, DateTime trenutakUtc) =>
        sve.Count(p => p.CreatedAt < trenutakUtc && (p.OtkazanaU is null || p.OtkazanaU >= trenutakUtc));

    public static PretplateAnalitikaDto Pretplate(
        IReadOnlyCollection<Pretplata> sve, int godina, int mjesec, bool tekuci, IReadOnlyCollection<Order> isporucene)
    {
        var pocetak = PoslovanjeKalendar.PocetakUtc(godina, mjesec);
        var (sg, sm) = mjesec == 12 ? (godina + 1, 1) : (godina, mjesec + 1);
        var kraj = PoslovanjeKalendar.PocetakUtc(sg, sm);
        var naPocetku = AktivnePretplate(sve, pocetak);
        var otkazane = sve.Count(p => p.OtkazanaU >= pocetak && p.OtkazanaU < kraj);
        var neto = isporucene.Sum(Neto);
        return new PretplateAnalitikaDto
        {
            AktivneNaKraju = AktivnePretplate(sve, kraj),
            Nove = sve.Count(p => p.CreatedAt >= pocetak && p.CreatedAt < kraj),
            Otkazane = otkazane,
            StopaOtkazivanja = Udio(otkazane, naPocetku),
            Pauzirane = tekuci ? sve.Count(p => p.Status == StatusPretplate.Pauzirana) : null,
            PoPlanu = sve.Where(p => p.CreatedAt < kraj && (p.OtkazanaU is null || p.OtkazanaU >= kraj))
                .GroupBy(p => p.Plan)
                .Select(g => new PlanAnalitikeDto { Plan = g.Key.ToString(), Aktivne = g.Count() })
                .OrderBy(x => x.Plan)
                .ToList(),
            UdioPrihoda = Udio(isporucene.Where(o => o.PretplataId is not null).Sum(Neto), neto) ?? 0m,
        };
    }

    public PrihodDto Prihod(IReadOnlyCollection<Order> isporucene)
    {
        var linije = isporucene.SelectMany(Linije).ToList();
        var promet = isporucene.Sum(o => o.Subtotal);
        var nabavno = linije.Sum(l => l.Nabavno ?? 0m);
        var zarada = promet - nabavno;
        var stavke = isporucene.SelectMany(o => o.Items).ToList();
        var ukupnoStavki = stavke.Sum(i => i.Quantity * i.UnitPrice);
        var korpice = stavke.Where(i => JeKorpica(i, katalog)).Sum(i => i.Quantity * i.UnitPrice);
        var neto = isporucene.Sum(Neto);
        var n = isporucene.Count;
        return new PrihodDto
        {
            PrometRobe = promet,
            Dostava = isporucene.Sum(o => o.DeliveryFee),
            PromoPopust = isporucene.Sum(o => o.Popust),
            PoeniPopust = isporucene.Sum(o => o.PoeniPopust),
            Neto = neto,
            Nabavno = Math.Round(nabavno, 2),
            Zarada = Math.Round(zarada, 2),
            Marza = linije.Any(l => l.Nabavno is not null) ? Udio(zarada, promet) : null,
            BrojNarudzbi = n,
            ProsjecnaNarudzba = n == 0 ? 0m : Math.Round(neto / n, 2),
            UdioKorpica = Udio(korpice, ukupnoStavki) ?? 0m,
            UdioSpajza = ukupnoStavki == 0 ? 0m : 1m - (Udio(korpice, ukupnoStavki) ?? 0m),
            Procjena = linije.Any(l => l.Procjena),
            BezNabavneCijene = linije.Count(l => l.NabavnaJedinicna is null),
        };
    }
}

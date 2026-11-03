using EPiljara.Application.DTOs;
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

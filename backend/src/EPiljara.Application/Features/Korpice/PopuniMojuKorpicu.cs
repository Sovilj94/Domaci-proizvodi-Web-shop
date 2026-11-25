using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// „Popuni umjesto mene" u kupčevom editoru (korpica po budžetu): dopunjava NACRT dok vrijednost ne
/// uđe u 95–100 % cilja. Ne čuva ništa — vraća predlog, a kupac ga pregleda i sačuva PUT-om, koji
/// ponovo provjerava sva pravila.
///
/// Redom, u krugovima: (1) svaka osnovna namirnica (prioritet 0) za jedan korak, (2) jedna nova
/// kultura u najmanjoj količini (kandidati iz <see cref="AutoZamjena.KandidatiAsync"/>, bez kupčevog
/// spiska „ne stavljaj mi"), (3) ostale namirnice za jedan korak. Svaki korak samo ako vrijednost ostaje
/// ≤ 100 %, marža prolazi i ima zalihe. Nacrt preko 100 % se vraća kakav jeste.
/// </summary>
public record PopuniMojuKorpicuCommand(Guid OrderItemId, IReadOnlyList<StavkaKorpiceUnos> Stavke)
    : IRequest<IReadOnlyList<StavkaKorpiceUnos>>;

public class PopuniMojuKorpicuHandler(IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<PopuniMojuKorpicuCommand, IReadOnlyList<StavkaKorpiceUnos>>
{
    /// <summary>Dopuna ne diže namirnicu preko dvostruke zadate količine — 6 kg krompira nije korpica.</summary>
    internal const decimal NajviseZadatih = 2m;

    private sealed class Stavka(Guid productId, decimal kolicina, decimal cijena, decimal nabavna, Product? artikal)
    {
        public Guid ProductId { get; } = productId;
        public decimal Kolicina { get; set; } = kolicina;
        public decimal Cijena { get; } = cijena;
        public decimal Nabavna { get; } = nabavna;
        public Product? Artikal { get; } = artikal;
    }

    public async Task<IReadOnlyList<StavkaKorpiceUnos>> Handle(PopuniMojuKorpicuCommand cmd, CancellationToken ct)
    {
        var o = opcije.Value;
        var (red, korpica, sadrzaj) = await MojaKorpicaPravila.UcitajZaIzmjenuAsync(db, current, cmd.OrderItemId, o, ct);

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order!.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00").Month;
        var kandidati = await AutoZamjena.KandidatiAsync(db, sadrzaj, korpica, mjesec, ct);
        var izbjegnuti = (await db.IzbjegnutiArtikli
            .Where(x => x.UserId == current.Id).Select(x => x.ProductId).ToListAsync(ct)).ToHashSet();
        var zalihe = await Zalihe.UcitajAsync(db, red.Order.Godina, red.Order.Sedmica, ct, red.Id);
        decimal Slobodno(Product p) => zalihe.Slobodno(p) / red.Quantity;

        var idjevi = cmd.Stavke.Select(s => s.ProductId).ToList();
        var katalog = await db.Products.IgnoreQueryFilters()
            .Where(p => idjevi.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var redovi = red.KorpicaStavke.ToDictionary(s => s.ProductId);

        // Cijena i nabavna kao u PUT-u: snimak reda, pa sadržaj sedmice, pa katalog.
        var nacrt = cmd.Stavke.Select(u =>
        {
            katalog.TryGetValue(u.ProductId, out var p);
            redovi.TryGetValue(u.ProductId, out var r);
            var cijena = r?.JedinicnaCijena ?? (p is null ? 0m : AutoZamjena.Cijena(sadrzaj, p));
            var nabavna = r?.NabavnaCijena ?? p?.NabavnaCijena ?? 0m;
            var kolicina = u.Kolicina ?? r?.Kolicina ?? (p is null ? 0m : IzmijeniMojuKorpicuHandler.PodrazumijevanaKolicina(p));
            return new Stavka(u.ProductId, kolicina, cijena, nabavna, p);
        }).ToList();

        var gornja = Math.Round(sadrzaj.CiljnaVrijednost * IzmijeniMojuKorpicuHandler.GornjaGranica, 2);
        var donja = Math.Round(sadrzaj.CiljnaVrijednost * IzmijeniMojuKorpicuHandler.DonjaGranica, 2);
        var nabavniBudzet = red.UnitPrice * (1 - o.MinimalnaMarza);
        var nabavnoPrije = red.KorpicaStavke.Sum(s => s.Kolicina * (s.NabavnaCijena ?? 0m));

        decimal V() => nacrt.Sum(s => s.Kolicina * s.Cijena);
        decimal N() => nacrt.Sum(s => s.Kolicina * s.Nabavna);
        bool Staje(decimal dV, decimal dN) =>
            Math.Round(V() + dV, 2) <= gornja
            && (N() + dN <= nabavniBudzet + 0.005m || N() + dN <= nabavnoPrije + 0.005m);

        bool Povecaj(Stavka s)
        {
            if (s.Artikal is null) return false;
            var korak = s.Artikal.KorakUKorpici();
            var nova = s.Kolicina + korak;
            if (s.Artikal.KolicinaUKorpici(korpica.Velicina) is decimal zadata && nova > zadata * NajviseZadatih) return false;
            if (nova > Slobodno(s.Artikal)) return false;
            if (!Staje(korak * s.Cijena, korak * s.Nabavna)) return false;
            s.Kolicina = nova;
            return true;
        }

        bool DodajNovu()
        {
            foreach (var p in kandidati)
            {
                if (izbjegnuti.Contains(p.Id) || nacrt.Any(s => s.ProductId == p.Id)) continue;
                var kolicina = p.NajmanjaKolicinaUKorpici();
                var cijena = AutoZamjena.Cijena(sadrzaj, p);
                var nabavna = p.NabavnaCijena ?? 0m;
                if (cijena <= 0 || kolicina > Slobodno(p)) continue;
                if (!Staje(kolicina * cijena, kolicina * nabavna)) continue;
                nacrt.Add(new Stavka(p.Id, kolicina, cijena, nabavna, p));
                return true;
            }
            return false;
        }

        // Krugovi su ograničeni brojem — svaki krug ili napreduje ili prekida petlju, ali granica
        // čuva od beskonačne petlje ako bi se pravilo ikad promijenilo.
        for (var krug = 0; krug < 500 && V() < donja; krug++)
        {
            var napredak = false;
            foreach (var s in nacrt.Where(s => s.Artikal?.PrioritetUKorpici <= 0).ToList())
                napredak |= Povecaj(s);
            if (napredak) continue;
            if (DodajNovu()) continue;
            foreach (var s in nacrt.Where(s => s.Artikal?.PrioritetUKorpici > 0).ToList())
                napredak |= Povecaj(s);
            if (!napredak) break;
        }

        return nacrt.Select(s => new StavkaKorpiceUnos(s.ProductId, s.Kolicina)).ToList();
    }
}

using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

public record RezultatGenerisanjaDto
{
    public Guid SadrzajId { get; init; }
    public Guid KorpicaId { get; init; }
    public string KorpicaNaziv { get; init; } = string.Empty;
    public int BrojArtikala { get; init; }
    public decimal Vrijednost { get; init; }
    public decimal CiljGenerisanja { get; init; }

    /// <summary>Popunjeno kad bazen nije mogao ispuniti minimum. Nije greška — admin dorađuje.</summary>
    public string? Upozorenje { get; init; }
}

/// <summary>
/// Sastavlja PRIJEDLOG sadržaja za zadatu sedmicu. Rezultat je uvijek Nacrt —
/// admin je posljednja instanca. Korpice koje za tu sedmicu već imaju sadržaj se preskaču.
/// </summary>
public record GenerisiSedmicniSadrzajCommand(int Godina, int Sedmica, Guid? KorpicaId)
    : IRequest<IReadOnlyList<RezultatGenerisanjaDto>>;

public class GenerisiSedmicniSadrzajHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GenerisiSedmicniSadrzajCommand, IReadOnlyList<RezultatGenerisanjaDto>>
{
    private readonly KorpiceOpcije _o = opcije.Value;

    public async Task<IReadOnlyList<RezultatGenerisanjaDto>> Handle(
        GenerisiSedmicniSadrzajCommand c, CancellationToken ct)
    {
        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable)
            .Where(p => c.KorpicaId == null || p.Id == c.KorpicaId)
            .ToListAsync(ct);

        var vecPostoje = await db.SedmicniSadrzaji
            .Where(s => s.Godina == c.Godina && s.Sedmica == c.Sedmica)
            .Select(s => s.KorpicaId)
            .ToListAsync(ct);

        var veze = await db.KorpicaKategorije.ToListAsync(ct);
        var mjesec = MjesecSedmice(c.Godina, c.Sedmica);
        var rezultati = new List<RezultatGenerisanjaDto>();

        foreach (var korpica in korpice.Where(k => !vecPostoje.Contains(k.Id)))
        {
            var kategorije = veze
                .Where(v => v.TipKorpice == korpica.TipKorpice)
                .Select(v => v.CategoryId)
                .ToHashSet();

            // Mjeseci su CSV u bazi, pa se sezona filtrira u memoriji nakon učitavanja.
            var bazen = (await db.Products
                    .Where(p => p.Vrsta == VrstaProizvoda.Obican
                                && p.MozeUKorpicu
                                && p.IsAvailable
                                && p.Stock > 0
                                && kategorije.Contains(p.CategoryId))
                    .ToListAsync(ct))
                .Where(p => p.Mjeseci.Length == 0 || p.Mjeseci.Contains(mjesec))
                .OrderBy(p => p.PrioritetUKorpici)
                .ThenBy(_ => Guid.NewGuid())      // nasumično unutar istog prioriteta
                .ToList();

            var minimum = _o.MinimalanBrojArtikala.GetValueOrDefault(
                korpica.Velicina ?? VelicinaKorpice.Mala, 5);
            var cilj = korpica.Price * _o.KoeficijentVrijednosti;

            // Budžet po stavci dok se ne ispuni minimum. Bez ovoga prvi artikal
            // pojede cio cilj i korpica ispadne sa jednom stavkom.
            var poStavci = cilj / Math.Max(minimum, 1);

            var sadrzaj = new SedmicniSadrzaj
            {
                KorpicaId = korpica.Id,
                Godina = c.Godina,
                Sedmica = c.Sedmica,
                Status = StatusSadrzaja.Nacrt,
            };

            var vrijednost = 0m;
            foreach (var artikal in bazen)
            {
                if (vrijednost >= cilj * 0.95m && sadrzaj.Stavke.Count >= minimum) break;
                if (artikal.Price <= 0) continue;

                var preostalo = cilj - vrijednost;
                if (preostalo <= 0) break;

                var budzet = sadrzaj.Stavke.Count < minimum
                    ? Math.Min(poStavci, preostalo)
                    : preostalo;

                var kolicina = Zaokruzi(budzet / artikal.Price, artikal.Unit);
                if (kolicina > artikal.Stock) kolicina = Zaokruzi(artikal.Stock, artikal.Unit);
                if (kolicina <= 0) continue;

                sadrzaj.Stavke.Add(new StavkaSadrzaja
                {
                    ProductId = artikal.Id,
                    Kolicina = kolicina,
                    JedinicnaCijena = artikal.Price,
                });
                vrijednost += kolicina * artikal.Price;
            }

            db.SedmicniSadrzaji.Add(sadrzaj);

            rezultati.Add(new RezultatGenerisanjaDto
            {
                SadrzajId = sadrzaj.Id,
                KorpicaId = korpica.Id,
                KorpicaNaziv = korpica.Name,
                BrojArtikala = sadrzaj.Stavke.Count,
                Vrijednost = vrijednost,
                CiljGenerisanja = cilj,
                Upozorenje = sadrzaj.Stavke.Count < minimum
                    ? $"U bazenu je premalo artikala: {sadrzaj.Stavke.Count} od {minimum}. Dopuni sadržaj ručno."
                    : null,
            });
        }

        await db.SaveChangesAsync(ct);
        return rezultati;
    }

    /// <summary>
    /// Korak je 0.1 za težinu i zapreminu, 1 za komade. Tip parametra je kvalifikovan jer se
    /// <c>Unit</c> inače sudara sa <c>MediatR.Unit</c> (oba imena su importovana u ovom fajlu).
    /// </summary>
    private static decimal Zaokruzi(decimal kolicina, EPiljara.Domain.Enums.Unit jedinica)
    {
        if (jedinica == EPiljara.Domain.Enums.Unit.Kom) return Math.Floor(kolicina);
        return Math.Floor(kolicina * 10m) / 10m;
    }

    /// <summary>Mjesec u koji pada četvrtak zadate ISO sedmice — četvrtak je uvijek u „pravom" mjesecu.</summary>
    private static int MjesecSedmice(int godina, int sedmica) =>
        ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday).Month;
}

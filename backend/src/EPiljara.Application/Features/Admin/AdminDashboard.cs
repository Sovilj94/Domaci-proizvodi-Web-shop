using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin;

/// <summary>Jedna vrsta i veličina korpice u sedmici; <c>IzPretplata</c> je dio <c>Komada</c>.</summary>
public record KorpicaUSedmiciDto(string Tip, string Velicina, string Naziv, int Komada, int IzPretplata);

/// <summary>
/// Artikal Špajza sabran po sedmici. Špajz su artikli koji se prodaju pojedinačno
/// (<c>Product.USpajzu</c>: med, zimnica, mliječno…); voće i povrće idu samo u korpice.
/// </summary>
public record SpajzUSedmiciDto(string Kategorija, string Artikal, string Jedinica, decimal Kolicina);

/// <summary>Šta je naručeno za sedmicu — bez otkazanih. Novac je na Poslovanju, ne ovdje.</summary>
public record PregledSedmiceDto(
    int Godina, int Sedmica, DateTime Isporuka, DateTime Rok,
    int Narudzbi, int IzPretplata, int Korpica,
    IReadOnlyList<KorpicaUSedmiciDto> Korpice,
    IReadOnlyList<SpajzUSedmiciDto> Spajz);

/// <summary>Početni ekran admina: sedmica koja se pakuje (zadnja zaključena) i otvorena sedmica.</summary>
public record PregledDto(PregledSedmiceDto Pakovanje, PregledSedmiceDto Otvorena);

public record GetPregledQuery(DateTime? Sada = null) : IRequest<PregledDto>;

public class GetPregledHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetPregledQuery, PregledDto>
{
    public async Task<PregledDto> Handle(GetPregledQuery q, CancellationToken ct)
    {
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var pakovanje = SedmicaHelper.ZaNabavku(dan, vrijemeOd, cutoff, q.Sada);
        var otvorena = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff, q.Sada);

        // Sve korpice (i nedostupne, ako su naručene) — mali katalog, čita se jednom.
        var korpice = await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica)
            .Select(p => new { p.Id, p.Name, p.TipKorpice, p.Velicina, p.IsAvailable, p.IsDeleted })
            .ToListAsync(ct);
        var korpicaIds = korpice.Select(k => k.Id).ToHashSet();
        var uSpajzu = await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.Vrsta == VrstaProizvoda.Obican && p.USpajzu)
            .Select(p => new { p.Id, Kategorija = p.Category!.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Kategorija, ct);

        async Task<PregledSedmiceDto> SedmicaAsync((int G, int S) w)
        {
            var isporuka = SedmicaHelper.DatumIsporuke(w.G, w.S, dan, vrijemeOd);
            var stavke = await db.OrderItems
                .Where(i => i.Order!.Godina == w.G && i.Order.Sedmica == w.S
                            && i.Order.Status != OrderStatus.Cancelled)
                .Select(i => new { i.OrderId, i.ProductId, i.ProductName, i.Unit, i.Quantity, Pretplata = i.Order!.PretplataId != null })
                .ToListAsync(ct);
            var narudzbe = await db.Orders
                .Where(o => o.Godina == w.G && o.Sedmica == w.S && o.Status != OrderStatus.Cancelled)
                .Select(o => o.PretplataId != null)
                .ToListAsync(ct);

            var uKorpicama = stavke.Where(s => korpicaIds.Contains(s.ProductId)).ToList();
            var redovi = korpice
                .Where(k => (k.IsAvailable && !k.IsDeleted) || uKorpicama.Any(s => s.ProductId == k.Id))
                .OrderBy(k => (int)(k.TipKorpice ?? TipKorpice.Povrce))
                .ThenBy(k => (int)(k.Velicina ?? VelicinaKorpice.Mala))
                .Select(k =>
                {
                    var moje = uKorpicama.Where(s => s.ProductId == k.Id).ToList();
                    return new KorpicaUSedmiciDto(
                        SedmicaHelper.Slug(k.TipKorpice ?? TipKorpice.Povrce),
                        (k.Velicina ?? VelicinaKorpice.Mala).ToString().ToLowerInvariant(),
                        k.Name,
                        (int)moje.Sum(s => s.Quantity),
                        (int)moje.Where(s => s.Pretplata).Sum(s => s.Quantity));
                })
                .ToList();

            var spajz = stavke.Where(s => uSpajzu.ContainsKey(s.ProductId))
                .GroupBy(s => s.ProductId)
                .Select(g => new SpajzUSedmiciDto(
                    uSpajzu[g.Key], g.First().ProductName, g.First().Unit.ToApi(), g.Sum(s => s.Quantity)))
                .OrderBy(s => s.Kategorija, StringComparer.CurrentCulture)
                .ThenBy(s => s.Artikal, StringComparer.CurrentCulture)
                .ToList();

            return new PregledSedmiceDto(
                w.G, w.S, isporuka, isporuka.AddHours(-cutoff),
                narudzbe.Count, narudzbe.Count(x => x), redovi.Sum(r => r.Komada),
                redovi, spajz);
        }

        return new PregledDto(await SedmicaAsync(pakovanje), await SedmicaAsync(otvorena));
    }
}

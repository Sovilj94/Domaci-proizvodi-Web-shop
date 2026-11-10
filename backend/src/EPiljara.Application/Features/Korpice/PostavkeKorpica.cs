using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Features.Korpice;

/*
 * Postavke korpica (admin → Korpice → Postavke): po vrsti korpice koje kategorije ulaze u nju,
 * cijena i ponuda svake veličine, i za artikle — smije li u korpicu, prioritet i zadate
 * količine. Pravila za sve korpice (koeficijent, marža, broj kultura, zamjene) su u
 * Podesavanja i ekran ih čuva kroz postojeći PUT /admin/podesavanja.
 */

/// <param name="Ambalaza">Cijena kutije (Product.AmbalazaCijena); null = nije unesena.</param>
public record KorpicaPostavkeDto(Guid Id, string Naziv, string Velicina, decimal Cijena, bool UProdaji, string? ImageUrl, decimal? Ambalaza = null);

public record TipPostavkeDto(string Tip, IReadOnlyList<KorpicaPostavkeDto> Korpice, IReadOnlyList<Guid> KategorijeIds);

public record KategorijaPostavkeDto(Guid Id, string Naziv);

public record PostavkeKorpicaDto(IReadOnlyList<TipPostavkeDto> Tipovi, IReadOnlyList<KategorijaPostavkeDto> Kategorije);

public static class PostavkeKorpicaMapa
{
    public static string Tip(TipKorpice t) => t.ToString().ToLowerInvariant();

    public static TipKorpice Tip(string t) =>
        Enum.TryParse<TipKorpice>(t, ignoreCase: true, out var tip)
            ? tip
            : throw new NotFoundException("Nepoznata vrsta korpice.");

    public static async Task<PostavkeKorpicaDto> UcitajAsync(IAppDbContext db, CancellationToken ct)
    {
        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.TipKorpice != null)
            .ToListAsync(ct);
        var veze = await db.KorpicaKategorije.ToListAsync(ct);
        var kategorije = await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new KategorijaPostavkeDto(c.Id, c.Name))
            .ToListAsync(ct);

        return new PostavkeKorpicaDto(
            Enum.GetValues<TipKorpice>().Select(t => new TipPostavkeDto(
                Tip(t),
                korpice.Where(k => k.TipKorpice == t)
                    .OrderBy(k => k.Velicina)
                    .Select(k => new KorpicaPostavkeDto(
                        k.Id, k.Name, (k.Velicina ?? VelicinaKorpice.Mala).ToString().ToLowerInvariant(),
                        k.Price, k.IsAvailable, k.ImageUrl, k.AmbalazaCijena))
                    .ToList(),
                veze.Where(v => v.TipKorpice == t).Select(v => v.CategoryId).ToList())).ToList(),
            kategorije);
    }
}

/* ---------- pregled ---------- */

public record GetPostavkeKorpicaQuery : IRequest<PostavkeKorpicaDto>;

public class GetPostavkeKorpicaHandler(IAppDbContext db) : IRequestHandler<GetPostavkeKorpicaQuery, PostavkeKorpicaDto>
{
    public Task<PostavkeKorpicaDto> Handle(GetPostavkeKorpicaQuery q, CancellationToken ct) =>
        PostavkeKorpicaMapa.UcitajAsync(db, ct);
}

/* ---------- jedna vrsta: kategorije, cijene, ponuda ---------- */

/// <param name="Ambalaza">Cijena kutije: null = ne mijenjaj, 0 ili manje = ukloni.</param>
public record KorpicaUnos(Guid Id, decimal Cijena, bool UProdaji, decimal? Ambalaza = null);

public record SacuvajTipKorpiceCommand(string Tip, IReadOnlyList<Guid> KategorijeIds, IReadOnlyList<KorpicaUnos> Korpice)
    : IRequest<PostavkeKorpicaDto>;

public class SacuvajTipKorpiceValidator : AbstractValidator<SacuvajTipKorpiceCommand>
{
    public SacuvajTipKorpiceValidator()
    {
        RuleForEach(x => x.Korpice).Must(k => k.Cijena > 0).WithMessage("Cijena korpice mora biti veća od 0.");
        RuleFor(x => x.KategorijeIds).Must(l => l.Distinct().Count() == l.Count).WithMessage("Kategorija je navedena dvaput.");
    }
}

public class SacuvajTipKorpiceHandler(IAppDbContext db) : IRequestHandler<SacuvajTipKorpiceCommand, PostavkeKorpicaDto>
{
    public async Task<PostavkeKorpicaDto> Handle(SacuvajTipKorpiceCommand c, CancellationToken ct)
    {
        var tip = PostavkeKorpicaMapa.Tip(c.Tip);

        var postojeceKategorije = await db.Categories.Where(k => c.KategorijeIds.Contains(k.Id)).CountAsync(ct);
        if (postojeceKategorije != c.KategorijeIds.Count)
            throw new NotFoundException("Kategorija nije pronađena.");

        // Veze se zamjenjuju u cjelosti: šta je štiklirano, to ulazi u ovu vrstu korpice.
        var stare = await db.KorpicaKategorije.Where(v => v.TipKorpice == tip).ToListAsync(ct);
        db.KorpicaKategorije.RemoveRange(stare.Where(v => !c.KategorijeIds.Contains(v.CategoryId)));
        foreach (var id in c.KategorijeIds.Where(id => stare.All(v => v.CategoryId != id)))
            db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = tip, CategoryId = id });

        var idjevi = c.Korpice.Select(k => k.Id).ToList();
        var korpice = await db.Products.Where(p => idjevi.Contains(p.Id)).ToListAsync(ct);
        foreach (var unos in c.Korpice)
        {
            var k = korpice.FirstOrDefault(p => p.Id == unos.Id && p.Vrsta == VrstaProizvoda.Korpica && p.TipKorpice == tip)
                ?? throw new NotFoundException("Korpica nije pronađena u ovoj vrsti.");
            k.Price = unos.Cijena;
            k.IsAvailable = unos.UProdaji;
            if (unos.Ambalaza is decimal a) k.AmbalazaCijena = a > 0 ? Math.Round(a, 2) : null;
            k.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return await PostavkeKorpicaMapa.UcitajAsync(db, ct);
    }
}

/* ---------- artikli: u korpici, prioritet, zadate količine ---------- */

/// <param name="CijenaPiljara">Cijena u piljari po jedinici; null = nije unesena (red se šalje cio).</param>
public record ArtikalKorpiceUnos(
    Guid Id, bool MozeUKorpicu, int PrioritetUKorpici,
    decimal? KolicinaMala, decimal? KolicinaSrednja, decimal? KolicinaVelika, decimal? CijenaPiljara = null);

public record SacuvajArtikleKorpicaCommand(IReadOnlyList<ArtikalKorpiceUnos> Artikli) : IRequest<int>;

public class SacuvajArtikleKorpicaValidator : AbstractValidator<SacuvajArtikleKorpicaCommand>
{
    public SacuvajArtikleKorpicaValidator()
    {
        RuleForEach(x => x.Artikli).Must(a => a.PrioritetUKorpici >= 0)
            .WithMessage("Prioritet u korpici ne može biti negativan.");
        RuleForEach(x => x.Artikli).Must(a => new[] { a.KolicinaMala, a.KolicinaSrednja, a.KolicinaVelika }.All(v => v is null or > 0))
            .WithMessage("Količina u korpici mora biti veća od 0 (ili prazna).");
        RuleForEach(x => x.Artikli).Must(a => a.CijenaPiljara is null or (> 0 and <= 10000))
            .WithMessage("Cijena u piljari mora biti veća od 0 (ili prazna).");
    }
}

public class SacuvajArtikleKorpicaHandler(IAppDbContext db) : IRequestHandler<SacuvajArtikleKorpicaCommand, int>
{
    public async Task<int> Handle(SacuvajArtikleKorpicaCommand c, CancellationToken ct)
    {
        var idjevi = c.Artikli.Select(a => a.Id).ToList();
        var artikli = await db.Products.Where(p => idjevi.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        foreach (var a in c.Artikli)
        {
            if (!artikli.TryGetValue(a.Id, out var p) || p.Vrsta != VrstaProizvoda.Obican)
                throw new NotFoundException("Artikal nije pronađen.");
            if (p.Unit == Unit.Kom && new[] { a.KolicinaMala, a.KolicinaSrednja, a.KolicinaVelika }
                    .Any(v => v is not null && v != Math.Floor(v.Value)))
                throw new DomainException($"{p.Name} ide u korpicu u cijelim komadima.", "kolicina_na_komad");

            p.MozeUKorpicu = a.MozeUKorpicu;
            p.PrioritetUKorpici = a.PrioritetUKorpici;
            (p.KolicinaMala, p.KolicinaSrednja, p.KolicinaVelika) = (a.KolicinaMala, a.KolicinaSrednja, a.KolicinaVelika);
            p.CijenaPiljara = a.CijenaPiljara is decimal pc ? Math.Round(pc, 2) : null;
            p.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return c.Artikli.Count;
    }
}

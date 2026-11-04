using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Sezonski kalendar korpica za admin panel: sve kulture iz kategorija jedne vrste korpice, sa
/// mjesecima u kojima idu u korpicu i da li uopšte idu. Isti podaci pune „Šta dobijaš u korpici?"
/// i generator sadržaja, pa izmjena ovdje odmah mijenja i jedno i drugo.
/// </summary>
public record KalendarStavkaDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? IzrezUrl { get; init; }
    public bool UKorpici { get; init; }
    /// <summary>Prazno = cijele godine (pravilo kataloga).</summary>
    public IReadOnlyList<int> Mjeseci { get; init; } = [];
    /// <summary>Mjeseci u kojima je kultura glavna — ide u početni sadržaj korpice prije ostalog.</summary>
    public IReadOnlyList<int> MjeseciGlavni { get; init; } = [];
    /// <summary>Samo kao zamjena koju kupac bira; nikad u početnom sadržaju.</summary>
    public bool Opciono { get; init; }
    public bool Dostupan { get; init; }

    public static KalendarStavkaDto Iz(Domain.Entities.Product p) => new()
    {
        ProductId = p.Id,
        Naziv = p.Name,
        ImageUrl = p.ImageUrl,
        IzrezUrl = p.IzrezUrl,
        UKorpici = p.MozeUKorpicu,
        Mjeseci = p.Mjeseci,
        MjeseciGlavni = p.MjeseciGlavni,
        Opciono = p.Opciono,
        Dostupan = p.IsAvailable,
    };
}

public record GetKalendarKorpicaQuery(string? Tip) : IRequest<IReadOnlyList<KalendarStavkaDto>>;

public class GetKalendarKorpicaHandler(IAppDbContext db)
    : IRequestHandler<GetKalendarKorpicaQuery, IReadOnlyList<KalendarStavkaDto>>
{
    public async Task<IReadOnlyList<KalendarStavkaDto>> Handle(GetKalendarKorpicaQuery q, CancellationToken ct)
    {
        var tip = SedmicaHelper.IzSluga(q.Tip) ?? throw new NotFoundException("Nepoznata vrsta korpice.");
        var kategorije = await db.KorpicaKategorije
            .Where(k => k.TipKorpice == tip)
            .Select(k => k.CategoryId)
            .ToListAsync(ct);

        var proizvodi = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Obican && kategorije.Contains(p.CategoryId))
            .OrderByDescending(p => p.MozeUKorpicu).ThenBy(p => p.Name)
            .ToListAsync(ct);

        return proizvodi.Select(KalendarStavkaDto.Iz).ToList();
    }
}

/// <param name="Mjeseci">1–12; prazno znači cijele godine.</param>
/// <param name="MjeseciGlavni">Podskup sezone u kojem je kultura glavna; null = bez promjene.</param>
/// <param name="Opciono">Samo kao zamjena; null = bez promjene.</param>
public record PostaviKalendarCommand(Guid ProductId, bool UKorpici, IReadOnlyList<int> Mjeseci,
    IReadOnlyList<int>? MjeseciGlavni = null, bool? Opciono = null) : IRequest<KalendarStavkaDto>;

public class PostaviKalendarValidator : AbstractValidator<PostaviKalendarCommand>
{
    public PostaviKalendarValidator()
    {
        RuleFor(x => x.Mjeseci).NotNull()
            .Must(m => m.All(x => x is >= 1 and <= 12)).WithMessage("Mjeseci moraju biti od 1 do 12.")
            .OverridePropertyName("mjeseci");
        RuleFor(x => x.MjeseciGlavni)
            .Must(m => m!.All(x => x is >= 1 and <= 12)).When(x => x.MjeseciGlavni is not null)
            .WithMessage("Mjeseci moraju biti od 1 do 12.")
            .OverridePropertyName("mjeseciGlavni");
    }
}

public class PostaviKalendarHandler(IAppDbContext db) : IRequestHandler<PostaviKalendarCommand, KalendarStavkaDto>
{
    public async Task<KalendarStavkaDto> Handle(PostaviKalendarCommand c, CancellationToken ct)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == c.ProductId && x.Vrsta == VrstaProizvoda.Obican, ct)
            ?? throw new NotFoundException("Proizvod", c.ProductId);

        p.MozeUKorpicu = c.UKorpici;
        p.Mjeseci = c.Mjeseci.Distinct().Order().ToArray();
        if (c.Opciono is bool opciono) p.Opciono = opciono;
        var glavni = c.MjeseciGlavni ?? p.MjeseciGlavni;
        // Glavno samo unutar sezone (prazna sezona = cijele godine), a opciona kultura nema glavnih mjeseci.
        p.MjeseciGlavni = p.Opciono
            ? []
            : glavni.Where(m => p.Mjeseci.Length == 0 || p.Mjeseci.Contains(m)).Distinct().Order().ToArray();
        await db.SaveChangesAsync(ct);

        return KalendarStavkaDto.Iz(p);
    }
}

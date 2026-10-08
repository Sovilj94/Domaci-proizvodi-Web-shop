using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Catalog;

/* ---------- Products ---------- */

public record GetProductsQuery(string? Category, string? Search, bool? Featured)
    : IRequest<IReadOnlyList<ProductDto>>;

public class GetProductsHandler(IAppDbContext db)
    : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<IReadOnlyList<ProductDto>> Handle(GetProductsQuery q, CancellationToken ct)
    {
        // Javni katalog ne prikazuje korpice — one su Product red samo da bi
        // iskoristile postojeću korpu/checkout/isporuku (vidi Product.Vrsta), ali se
        // seeduju sa prvom kategorijom/proizvođačem radi FK-ova, pa bi se ovdje
        // pojavile kao povrće od nasumičnog proizvođača. Korpice imaju svoj katalog
        // (/korpice, KorpiceQueries.cs). Admin i dalje vidi sve — GetAdminProductsHandler
        // je potpuno odvojen handler bez ovog filtera.
        var query = db.Products
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .Where(p => p.Vrsta == VrstaProizvoda.Obican)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Category))
            query = query.Where(p => p.Category!.Slug == q.Category);

        if (q.Featured == true)
            query = query.Where(p => p.IsFeatured);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(s) ||
                p.Description.ToLower().Contains(s) ||
                p.Category!.Name.ToLower().Contains(s));
        }

        var list = await query
            .OrderByDescending(p => p.IsAvailable)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        return list.Select(p => p.ToDto()).ToList();
    }
}

/* ---------- Product by slug ---------- */

public record GetProductBySlugQuery(string Slug) : IRequest<ProductDto>;

public class GetProductBySlugHandler(IAppDbContext db)
    : IRequestHandler<GetProductBySlugQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductBySlugQuery q, CancellationToken ct)
    {
        // Isti razlog kao u GetProductsHandler — korpica po slugu ide kroz /korpice, ne ovdje.
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .FirstOrDefaultAsync(p => p.Slug == q.Slug && p.Vrsta == VrstaProizvoda.Obican, ct)
            ?? throw new NotFoundException("Proizvod nije pronađen.");
        return product.ToDto();
    }
}

/* ---------- Categories ---------- */

public record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;

public class GetCategoriesHandler(IAppDbContext db)
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery q, CancellationToken ct)
    {
        var categories = await db.Categories.OrderBy(c => c.SortOrder).ToListAsync(ct);
        var counts = await db.Products
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        return categories.Select(c => c.ToDto(counts.GetValueOrDefault(c.Id))).ToList();
    }
}

/* ---------- Producers ---------- */

public record GetProducersQuery : IRequest<IReadOnlyList<ProducerDto>>;

public class GetProducersHandler(IAppDbContext db)
    : IRequestHandler<GetProducersQuery, IReadOnlyList<ProducerDto>>
{
    public async Task<IReadOnlyList<ProducerDto>> Handle(GetProducersQuery q, CancellationToken ct)
    {
        var producers = await db.Producers.OrderBy(p => p.Name).ToListAsync(ct);
        return producers.Select(p => p.ToDto()).ToList();
    }
}

public record GetProducerByIdQuery(Guid Id) : IRequest<ProducerDto>;

public class GetProducerByIdHandler(IAppDbContext db)
    : IRequestHandler<GetProducerByIdQuery, ProducerDto>
{
    public async Task<ProducerDto> Handle(GetProducerByIdQuery q, CancellationToken ct)
    {
        var producer = await db.Producers.FirstOrDefaultAsync(p => p.Id == q.Id, ct)
            ?? throw new NotFoundException("Proizvođač nije pronađen.");
        return producer.ToDto();
    }
}

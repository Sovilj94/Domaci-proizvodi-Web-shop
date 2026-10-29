using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>
/// Shared payload for creating and updating products.
///
/// <c>MozeUKorpicu</c> / <c>PrioritetUKorpici</c> / <c>Mjeseci</c>: polja korpice, bez
/// kojih artikal dodat kroz admin ekran nikad nije mogao ući u bazen generatora korpica
/// (vidi <c>GenerisiSedmicniSadrzajHandler</c>) — kolone su postojale na <c>Product</c>-u,
/// ali ništa iz admin API-ja nije pisalo u njih. Opciona su (<c>null</c> dozvoljen) da se
/// ne sruši postojeći klijentski kod koji ih još ne šalje; handler tretira izostale kao
/// "ne mijenjaj" na update-u i kao podrazumijevane vrijednosti na create-u.
/// </summary>
public record ProductInput(
    string Name,
    decimal Price,
    string Unit,
    decimal Stock,
    Guid CategoryId,
    Guid ProducerId,
    string Description,
    string? ImageUrl,
    bool IsAvailable,
    bool? IsFeatured,
    bool? MozeUKorpicu = null,
    int? PrioritetUKorpici = null,
    IReadOnlyList<int>? Mjeseci = null,
    string? Boja = null,
    decimal? NabavnaCijena = null,
    bool? UkloniNabavnuCijenu = null,
    string? IzrezUrl = null);

public class ProductInputValidator : AbstractValidator<ProductInput>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Naziv je obavezan.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Cijena mora biti veća od 0.");
        RuleFor(x => x.Unit).NotEmpty().WithMessage("Jedinica je obavezna.");
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Kategorija je obavezna.");
        RuleFor(x => x.ProducerId).NotEmpty().WithMessage("Proizvođač je obavezan.");
        RuleFor(x => x.PrioritetUKorpici).GreaterThanOrEqualTo(0)
            .When(x => x.PrioritetUKorpici is not null)
            .WithMessage("Prioritet u korpici ne može biti negativan.");
        RuleForEach(x => x.Mjeseci).InclusiveBetween(1, 12)
            .When(x => x.Mjeseci is not null)
            .WithMessage("Mjesec mora biti između 1 i 12.");
        RuleFor(x => x.NabavnaCijena).GreaterThan(0)
            .When(x => x.NabavnaCijena is not null)
            .WithMessage("Nabavna cijena mora biti veća od 0.");
        RuleFor(x => x.Boja).Must(b => BojaProizvoda.JeValidna(BojaUlaz.Normalizuj(b)))
            .WithMessage("Boja mora biti u obliku #RRGGBB.");
    }
}

/// <summary>Prazna boja iz forme znači „bez zadane boje".</summary>
internal static class BojaUlaz
{
    public static string? Normalizuj(string? boja) =>
        string.IsNullOrWhiteSpace(boja) ? null : boja.Trim().ToUpperInvariant();
}

/* ---------- List ---------- */

public record GetAdminProductsQuery : IRequest<IReadOnlyList<AdminProductDto>>;

public class GetAdminProductsHandler(IAppDbContext db)
    : IRequestHandler<GetAdminProductsQuery, IReadOnlyList<AdminProductDto>>
{
    public async Task<IReadOnlyList<AdminProductDto>> Handle(GetAdminProductsQuery q, CancellationToken ct)
    {
        var products = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        return products.Select(p => p.ToAdminDto()).ToList();
    }
}

/* ---------- Create ---------- */

public record CreateProductCommand(ProductInput Input) : IRequest<AdminProductDto>;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator() => RuleFor(x => x.Input).SetValidator(new ProductInputValidator());
}

public class CreateProductHandler(IAppDbContext db) : IRequestHandler<CreateProductCommand, AdminProductDto>
{
    public async Task<AdminProductDto> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == input.CategoryId, ct)
            ?? throw new DomainException("Nepoznata kategorija.", "invalid_reference");
        var producer = await db.Producers.FirstOrDefaultAsync(p => p.Id == input.ProducerId, ct)
            ?? throw new DomainException("Nepoznat proizvođač.", "invalid_reference");

        var product = new Product
        {
            Name = input.Name.Trim(),
            Slug = await UniqueSlugAsync(input.Name, ct),
            Price = input.Price,
            Unit = UnitExtensions.ParseUnit(input.Unit),
            Stock = input.Stock,
            Description = input.Description ?? string.Empty,
            ImageUrl = input.ImageUrl,
            IzrezUrl = input.IzrezUrl,
            IsAvailable = input.IsAvailable,
            IsFeatured = input.IsFeatured ?? false,
            CategoryId = category.Id,
            Category = category,
            ProducerId = producer.Id,
            Producer = producer,
            MozeUKorpicu = input.MozeUKorpicu ?? false,
            PrioritetUKorpici = input.PrioritetUKorpici ?? 0,
            Mjeseci = input.Mjeseci?.ToArray() ?? [],
            Boja = BojaUlaz.Normalizuj(input.Boja),
            NabavnaCijena = input.UkloniNabavnuCijenu == true ? null : input.NabavnaCijena,
        };
        product.ProvjeriInvarijantuKorpice();

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return product.ToAdminDto();
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slug.From(name);
        var slug = baseSlug;
        var n = 2;
        while (await db.Products.AnyAsync(p => p.Slug == slug, ct))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }
}

/* ---------- Update ---------- */

public record UpdateProductCommand(Guid Id, ProductInput Input) : IRequest<AdminProductDto>;

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator() => RuleFor(x => x.Input).SetValidator(new ProductInputValidator());
}

public class UpdateProductHandler(IAppDbContext db) : IRequestHandler<UpdateProductCommand, AdminProductDto>
{
    public async Task<AdminProductDto> Handle(UpdateProductCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Proizvod nije pronađen.");

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == input.CategoryId, ct)
            ?? throw new DomainException("Nepoznata kategorija.", "invalid_reference");
        var producer = await db.Producers.FirstOrDefaultAsync(p => p.Id == input.ProducerId, ct)
            ?? throw new DomainException("Nepoznat proizvođač.", "invalid_reference");

        product.Name = input.Name.Trim();
        product.Price = input.Price;
        product.Unit = UnitExtensions.ParseUnit(input.Unit);
        product.Stock = input.Stock;
        product.Description = input.Description ?? string.Empty;
        product.ImageUrl = input.ImageUrl ?? product.ImageUrl;
        product.IzrezUrl = input.IzrezUrl ?? product.IzrezUrl;
        product.IsAvailable = input.IsAvailable;
        product.IsFeatured = input.IsFeatured ?? product.IsFeatured;
        product.CategoryId = category.Id;
        product.Category = category;
        product.ProducerId = producer.Id;
        product.Producer = producer;
        product.MozeUKorpicu = input.MozeUKorpicu ?? product.MozeUKorpicu;
        product.PrioritetUKorpici = input.PrioritetUKorpici ?? product.PrioritetUKorpici;
        product.Mjeseci = input.Mjeseci?.ToArray() ?? product.Mjeseci;
        // null = ne mijenjaj (stari klijent), prazan string = vrati na boju iz palete.
        if (input.Boja is not null) product.Boja = BojaUlaz.Normalizuj(input.Boja);
        // Isti obrazac: izostala nabavna cijena = ne mijenjaj; brisanje se traži izričito.
        if (input.UkloniNabavnuCijenu == true) product.NabavnaCijena = null;
        else if (input.NabavnaCijena is not null) product.NabavnaCijena = input.NabavnaCijena;
        product.UpdatedAt = DateTime.UtcNow;
        product.ProvjeriInvarijantuKorpice();

        await db.SaveChangesAsync(ct);
        return product.ToAdminDto();
    }
}

/* ---------- Delete (soft) ---------- */

public record DeleteProductCommand(Guid Id) : IRequest<Guid>;

public class DeleteProductHandler(IAppDbContext db) : IRequestHandler<DeleteProductCommand, Guid>
{
    public async Task<Guid> Handle(DeleteProductCommand cmd, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Proizvod nije pronađen.");

        // Soft delete keeps order history intact (see the global query filter).
        product.IsDeleted = true;
        product.IsAvailable = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

/* ---------- Toggle availability ---------- */

public record SetProductAvailabilityCommand(Guid Id, bool IsAvailable) : IRequest<AdminProductDto>;

public class SetProductAvailabilityHandler(IAppDbContext db)
    : IRequestHandler<SetProductAvailabilityCommand, AdminProductDto>
{
    public async Task<AdminProductDto> Handle(SetProductAvailabilityCommand cmd, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Proizvod nije pronađen.");

        product.IsAvailable = cmd.IsAvailable;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return product.ToAdminDto();
    }
}

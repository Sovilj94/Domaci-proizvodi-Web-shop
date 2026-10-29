using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>Shared payload for creating and updating categories.</summary>
public record CategoryInput(string Name, string? ImageUrl, int SortOrder, string? NazivEn = null);

public class CategoryInputValidator : AbstractValidator<CategoryInput>
{
    public CategoryInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Naziv je obavezan.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage("Redoslijed ne može biti negativan.");
    }
}

/* ---------- Create ---------- */

public record CreateCategoryCommand(CategoryInput Input) : IRequest<CategoryDto>;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator() => RuleFor(x => x.Input).SetValidator(new CategoryInputValidator());
}

public class CreateCategoryHandler(IAppDbContext db) : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(CreateCategoryCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        var category = new Category
        {
            Name = input.Name.Trim(),
            Slug = await UniqueSlugAsync(input.Name, ct),
            ImageUrl = input.ImageUrl,
            NazivEn = string.IsNullOrWhiteSpace(input.NazivEn) ? null : input.NazivEn.Trim(),
            SortOrder = input.SortOrder,
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return category.ToDto(0);
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slug.From(name);
        var slug = baseSlug;
        var n = 2;
        while (await db.Categories.AnyAsync(c => c.Slug == slug, ct))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }
}

/* ---------- Update ---------- */

public record UpdateCategoryCommand(Guid Id, CategoryInput Input) : IRequest<CategoryDto>;

public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator() => RuleFor(x => x.Input).SetValidator(new CategoryInputValidator());
}

public class UpdateCategoryHandler(IAppDbContext db) : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(UpdateCategoryCommand cmd, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Kategorija nije pronađena.");

        // Slug stays stable so existing shop links keep working.
        category.Name = cmd.Input.Name.Trim();
        category.ImageUrl = cmd.Input.ImageUrl;
        category.NazivEn = string.IsNullOrWhiteSpace(cmd.Input.NazivEn) ? null : cmd.Input.NazivEn.Trim();
        category.SortOrder = cmd.Input.SortOrder;

        await db.SaveChangesAsync(ct);
        var count = await db.Products.CountAsync(p => p.CategoryId == category.Id, ct);
        return category.ToDto(count);
    }
}

/* ---------- Delete ---------- */

public record DeleteCategoryCommand(Guid Id) : IRequest<Guid>;

public class DeleteCategoryHandler(IAppDbContext db) : IRequestHandler<DeleteCategoryCommand, Guid>
{
    public async Task<Guid> Handle(DeleteCategoryCommand cmd, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Kategorija nije pronađena.");

        // Ignore the soft-delete filter: even a hidden product still holds the FK,
        // so it would block the delete at the database. Guard with a clear 409.
        if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.CategoryId == cmd.Id, ct))
            throw new ConflictException(
                "Kategorija sadrži proizvode i ne može se obrisati.", "category_in_use");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

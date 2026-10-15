using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.PromoKodovi;

public record ProvjeriPromoKodQuery(string Kod, string? Email, IReadOnlyList<OrderItemInput> Items)
    : IRequest<ProvjeraPromoKodaDto>;

public class ProvjeriPromoKodValidator : AbstractValidator<ProvjeriPromoKodQuery>
{
    public ProvjeriPromoKodValidator()
    {
        RuleFor(x => x.Kod).NotEmpty().WithMessage("Unesite promo kod.")
            .MaximumLength(30).WithMessage("Promo kod je predugačak.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Korpa je prazna.");
    }
}

/// <summary>
/// Provjera ne rezerviše kod — samo kaže koliki bi popust bio. Konačna provjera je pri
/// slanju narudžbe, jer se između dva koraka kod može potrošiti ili ugasiti.
/// </summary>
public class ProvjeriPromoKodHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<ProvjeriPromoKodQuery, ProvjeraPromoKodaDto>
{
    public async Task<ProvjeraPromoKodaDto> Handle(ProvjeriPromoKodQuery query, CancellationToken ct)
    {
        // Subtotal iz cijena u bazi, nikad iz klijenta.
        decimal subtotal = 0m;
        foreach (var line in query.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            if (product is null || !product.IsAvailable)
                throw new DomainException($"Proizvod nije dostupan ({line.ProductId}).", "product_unavailable");
            subtotal += product.Price * line.Quantity;
        }
        subtotal = Math.Round(subtotal, 2);

        var kupac = current.IsAuthenticated
            ? new KupacZaPromo(current.Id, current.Email)
            : new KupacZaPromo(null, query.Email);

        var kod = await PromoKodPravila.ProvjeriAsync(db, query.Kod, subtotal, kupac, LokalniDatum.Danas(), ct);
        return new ProvjeraPromoKodaDto(kod.Kod, kod.Opis, PromoKodPravila.Popust(kod, subtotal), subtotal);
    }
}

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

namespace EPiljara.Application.Features.Orders;

public record CustomerInput(string Name, string? Email, string Phone);

public record OrderItemInput(Guid ProductId, decimal Quantity);

public record CreateOrderCommand(
    CustomerInput Customer,
    Guid DeliveryZoneId,
    Guid DeliverySlotId,
    string DeliveryAddress,
    string? Note,
    IReadOnlyList<OrderItemInput> Items,
    double? Latitude = null,
    double? Longitude = null) : IRequest<OrderDto>;

public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.Customer).NotNull().WithMessage("Podaci o kupcu su obavezni.");
        When(x => x.Customer is not null, () =>
        {
            RuleFor(x => x.Customer.Name).NotEmpty().WithMessage("Ime je obavezno.");
            RuleFor(x => x.Customer.Phone).NotEmpty().WithMessage("Telefon je obavezan.");
        });
        RuleFor(x => x.DeliveryZoneId).NotEmpty().WithMessage("Odaberite zonu dostave.");
        RuleFor(x => x.DeliverySlotId).NotEmpty().WithMessage("Odaberite termin dostave.");
        RuleFor(x => x.DeliveryAddress).NotEmpty().WithMessage("Adresa je obavezna.");
        // Sve greške oko tačke idu pod ključ adrese — obje forme prikazuju
        // errors.deliveryAddress ispod polja s mapom, pa poruka pod "latitude"/"longitude"
        // ne bi imala gdje da se pojavi i narudžba bi nečujno pala.
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue)
            .OverridePropertyName("deliveryAddress")
            .WithMessage("Neispravna geografska širina.");
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue)
            .OverridePropertyName("deliveryAddress")
            .WithMessage("Neispravna geografska dužina.");
        // Pola tačke nije tačka — pin bez jedne ose bi se vodio kao "ima lokaciju", a ne bi se
        // mogao iscrtati.
        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .OverridePropertyName("deliveryAddress")
            .WithMessage("Potrebne su i geografska širina i dužina.");
        // Opseg cijelog globusa propušta zamijenjenu širinu/dužinu, pa tačka mora pasti
        // i u okvir Bosne i Hercegovine. Provjerava se tek kad su obje vrijednosti u opsegu,
        // da se poruke ne dupliraju.
        RuleFor(x => x)
            .Must(x => GeoBounds.Contains(x.Latitude!.Value, x.Longitude!.Value))
            .When(x => x.Latitude is >= -90 and <= 90 && x.Longitude is >= -180 and <= 180)
            .OverridePropertyName("deliveryAddress")
            .WithMessage(GeoBounds.OutsideMessage);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Korpa je prazna.");
        RuleForEach(x => x.Items).Must(i => i.Quantity > 0).WithMessage("Neispravna količina.");
    }
}

public class CreateOrderHandler(IAppDbContext db, ICurrentUser current, IEmailService email, IViberService viber)
    : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var zone = await db.DeliveryZones
            .FirstOrDefaultAsync(z => z.Id == cmd.DeliveryZoneId && z.IsActive, ct)
            ?? throw new DomainException("Nepoznata zona dostave.", "invalid_zone");

        var slot = await db.DeliverySlots.FirstOrDefaultAsync(s => s.Id == cmd.DeliverySlotId, ct)
            ?? throw new DomainException("Nepoznat termin dostave.", "invalid_slot");

        var items = new List<OrderItem>();
        foreach (var line in cmd.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            if (product is null || !product.IsAvailable)
                throw new DomainException($"Proizvod nije dostupan ({line.ProductId}).", "product_unavailable");

            items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit,
                Quantity = line.Quantity,
                UnitPrice = product.Price, // price snapshot
                ImageUrl = product.ImageUrl,
            });

            product.Stock = Math.Max(0, product.Stock - line.Quantity);
        }

        var subtotal = Math.Round(items.Sum(i => i.UnitPrice * i.Quantity), 2);
        if (subtotal < zone.MinOrderAmount)
            throw new DomainException(
                $"Minimalna narudžba za zonu {zone.Name} je {zone.MinOrderAmount:0.##} KM.",
                "below_minimum");

        // A registered customer placing their own order gets it linked to their account.
        var customerId = current.IsAuthenticated && current.Role == UserRole.Customer ? current.Id : null;

        var order = new Order
        {
            Code = await GenerateCodeAsync(ct),
            CustomerId = customerId,
            CustomerName = cmd.Customer.Name,
            CustomerPhone = cmd.Customer.Phone,
            CustomerEmail = cmd.Customer.Email,
            Status = OrderStatus.Pending,
            Items = items,
            Subtotal = subtotal,
            DeliveryFee = zone.DeliveryFee,
            TotalAmount = Math.Round(subtotal + zone.DeliveryFee, 2),
            DeliverySlotId = slot.Id,
            DeliverySlot = slot,
            DeliveryZoneId = zone.Id,
            DeliveryZone = zone,
            DeliveryAddress = cmd.DeliveryAddress,
            Note = cmd.Note,
            Latitude = cmd.Latitude,
            Longitude = cmd.Longitude,
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        try
        {
            // Confirm by email when we have one; otherwise fall back to Viber on the phone.
            if (!string.IsNullOrWhiteSpace(order.CustomerEmail))
                await email.SendOrderConfirmationAsync(order, ct);
            else
                await viber.SendOrderConfirmationAsync(order, ct);
        }
        catch
        {
            // Notification failures never block order placement.
        }

        return order.ToDto();
    }

    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        var count = await db.Orders.CountAsync(ct);
        var now = DateTime.UtcNow;
        return $"EP-{now:yyMM}-{count + 41:D4}";
    }
}

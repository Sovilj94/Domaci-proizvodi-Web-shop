using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Orders;

public record CreateGuestOrderRequestCommand(
    CustomerInput Customer,
    Guid DeliveryZoneId,
    Guid DeliverySlotId,
    string DeliveryAddress,
    string? Note,
    IReadOnlyList<OrderItemInput> Items,
    double? Latitude = null,
    double? Longitude = null) : IRequest<GuestOrderRequestDto>;

public record GuestOrderRequestDto(DateTime ExpiresAt);

public class CreateGuestOrderRequestValidator : AbstractValidator<CreateGuestOrderRequestCommand>
{
    public CreateGuestOrderRequestValidator()
    {
        RuleFor(x => x.Customer).NotNull().WithMessage("Podaci o kupcu su obavezni.");
        When(x => x.Customer is not null, () =>
        {
            RuleFor(x => x.Customer.Name).NotEmpty().WithMessage("Ime je obavezno.");
            RuleFor(x => x.Customer.Phone).NotEmpty().WithMessage("Telefon je obavezan.");
            // Gost bez e-maila nema kako potvrditi — zato je ovdje obavezan, a kod
            // prijavljenog kupca nije.
            RuleFor(x => x.Customer.Email).NotEmpty().WithMessage("E-mail je obavezan za naručivanje bez naloga.");
            RuleFor(x => x.Customer.Email).EmailAddress().WithMessage("Neispravna e-mail adresa.")
                .When(x => !string.IsNullOrWhiteSpace(x.Customer.Email));
        });
        RuleFor(x => x.DeliveryZoneId).NotEmpty().WithMessage("Odaberite zonu dostave.");
        RuleFor(x => x.DeliverySlotId).NotEmpty().WithMessage("Odaberite termin dostave.");
        RuleFor(x => x.DeliveryAddress).NotEmpty().WithMessage("Adresa je obavezna.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Korpa je prazna.");
        RuleForEach(x => x.Items).Must(i => i.Quantity > 0).WithMessage("Neispravna količina.");
    }
}

public class CreateGuestOrderRequestHandler(IAppDbContext db, ICurrentUser current, IEmailService email)
    : IRequestHandler<CreateGuestOrderRequestCommand, GuestOrderRequestDto>
{
    public async Task<GuestOrderRequestDto> Handle(CreateGuestOrderRequestCommand cmd, CancellationToken ct)
    {
        // Prijavljeni kupac ima svoj put — POST /orders — gdje se narudžba vezuje za
        // njegov nalog. Zahtjev gosta postoji isključivo za nepoznatog kupca; da ga
        // prijavljeni kupac ovuda propusti, dobio bi narudžbu koju ne vidi pod
        // "Moje narudžbe" (nastaje tek kad neko klikne link iz mejla, bez ICurrentUser konteksta).
        if (current.IsAuthenticated)
            throw new DomainException("Prijavljeni kupac naručuje sa svog naloga.", "already_authenticated");

        var zone = await db.DeliveryZones
            .FirstOrDefaultAsync(z => z.Id == cmd.DeliveryZoneId && z.IsActive, ct)
            ?? throw new DomainException("Nepoznata zona dostave.", "invalid_zone");

        var slot = await db.DeliverySlots.FirstOrDefaultAsync(s => s.Id == cmd.DeliverySlotId, ct)
            ?? throw new DomainException("Nepoznat termin dostave.", "invalid_slot");

        var items = new List<GuestOrderRequestItem>();
        foreach (var line in cmd.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            if (product is null || !product.IsAvailable)
                throw new DomainException($"Proizvod nije dostupan ({line.ProductId}).", "product_unavailable");

            items.Add(new GuestOrderRequestItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit,
                Quantity = line.Quantity,
                UnitPrice = product.Price, // snimljena cijena u trenutku slanja forme
                ImageUrl = product.ImageUrl,
            });

            // Zalihe se namjerno NE diraju ovdje (za razliku od CreateOrderHandler-a).
            // Ovo je nepotvrđen zahtjev koji ističe za GuestOrderRequest.TrajanjeMinuta
            // minuta ako gost ne klikne link iz mejla — da se stock skida ovdje, roba bi
            // se "zaključavala" za narudžbe koje se možda nikad neće desiti. Skidanje
            // zaliha je posao handlera koji potvrđuje zahtjev.
        }

        var subtotal = Math.Round(items.Sum(i => i.UnitPrice * i.Quantity), 2);
        if (subtotal < zone.MinOrderAmount)
            throw new DomainException(
                $"Minimalna narudžba za zonu {zone.Name} je {zone.MinOrderAmount:0.##} KM.",
                "below_minimum");

        var request = new GuestOrderRequest
        {
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(GuestOrderRequest.TrajanjeMinuta),
            CustomerName = cmd.Customer.Name,
            CustomerEmail = cmd.Customer.Email!, // validator ga čini obaveznim za gosta
            CustomerPhone = cmd.Customer.Phone,
            DeliveryZoneId = zone.Id,
            DeliveryZone = zone,
            DeliverySlotId = slot.Id,
            DeliverySlot = slot,
            DeliveryAddress = cmd.DeliveryAddress,
            Note = cmd.Note,
            Latitude = cmd.Latitude,
            Longitude = cmd.Longitude,
            Items = items,
            Subtotal = subtotal,
            DeliveryFee = zone.DeliveryFee,
            TotalAmount = Math.Round(subtotal + zone.DeliveryFee, 2),
        };

        // Upis i slanje mejla dijele jednu transakciju, bez try/catch oko slanja:
        // za razliku od CreateOrderHandler-a (narudžba već postoji, mejl je samo
        // obavještenje koje smije propasti), ovdje JE mejl proizvod cijele operacije —
        // gost bez linka nema kako potvrditi zahtjev, pa red ne smije preživjeti neuspjelo
        // slanje. Izuzetak se propagira neuhvaćen; `await using` iznad ga koristi da
        // automatski vrati (rollback) transakciju kad blok napusti bez CommitAsync-a,
        // isti obrazac kao u AddressCommands.cs za višekoračne upise.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.GuestOrderRequests.Add(request);
        await db.SaveChangesAsync(ct);

        await email.SendGuestOrderRequestAsync(request, ct);

        await tx.CommitAsync(ct);
        return new GuestOrderRequestDto(request.ExpiresAt);
    }
}

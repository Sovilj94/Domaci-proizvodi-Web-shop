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

// Namjerno duplicira pravila za kupca/zonu/termin/adresu/stavke iz CreateOrderValidator
// umjesto da ih izvuče u zajednički CustomerInputValidator pozvan kroz SetValidator:
// SetValidator mijenja oblik generisanih ključeva grešaka, a postojeća forma za
// naručivanje (i tri buduća taska) čitaju err.fieldErrors["customer.name"] doslovno.
// Duplikacija je ovdje jeftinija od refaktora koji bi mogao tiho pokvariti poruke
// validacije na živoj putanji naručivanja.
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
        // Iste geo provjere kao CreateOrderValidator — gost prolazi kroz isti mapski
        // picker, a nepotvrđena tačka danas postaje prava Order.Latitude/Longitude čim
        // sljedeći task potvrdi zahtjev, pa driver aplikacija ne smije dobiti polovičnu
        // ili pogrešnu koordinatu za navigaciju.
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

        db.GuestOrderRequests.Add(request);
        await db.SaveChangesAsync(ct);

        // Za razliku od CreateOrderHandler-a (narudžba već postoji, mejl je samo
        // obavještenje koje smije tiho propasti), ovdje JE mejl proizvod cijele operacije:
        // gost bez linka nema kako potvrditi zahtjev, pa greška ne smije biti progutana —
        // catch ispod ništa ne guta, samo čisti pa prosljeđuje isti izuzetak (throw, ne
        // throw ex, da ostane originalni stack trace).
        //
        // Ranija verzija je upis i slanje držala u jednoj DB transakciji da bi rollback
        // pokrio neuspjeh, ali SendGuestOrderRequestAsync radi pravi SMTP round-trip
        // (bez eksplicitnog timeouta, do ~100s), pa bi to držalo otvorenu transakciju i
        // konekciju iz poola za cijelo to vrijeme — ova ruta je javna i neautentikovana,
        // pa šačica gostiju uz spor mail server realno prazni connection pool. Zato se
        // ovdje upisuje i ODMAH snima (bez otvorene transakcije), a neuspjeh slanja se
        // kompenzuje brisanjem tek upisanog reda umjesto da ga transakcija povuče nazad.
        // Ako i sâmo brisanje padne, red i dalje ističe za
        // GuestOrderRequest.TrajanjeMinuta minuta i uklanja ga pozadinski čistač (kasniji
        // task) — sistem se sam sanira umjesto da gomila smeće.
        try
        {
            await email.SendGuestOrderRequestAsync(request, ct);
        }
        catch
        {
            db.GuestOrderRequests.Remove(request);
            await db.SaveChangesAsync(ct);
            throw;
        }

        return new GuestOrderRequestDto(request.ExpiresAt);
    }
}

public record ConfirmGuestOrderCommand(string Token) : IRequest<OrderDto>;

/// <summary>
/// Klik na link iz mejla. Ovo je jedino mjesto gdje narudžba gosta stvarno nastaje —
/// do sada je postojao samo <see cref="GuestOrderRequest"/>, koji ovim postaje stvaran
/// <see cref="Order"/> i nestaje.
/// </summary>
public class ConfirmGuestOrderHandler(IAppDbContext db, IEmailService email, IViberService viber)
    : IRequestHandler<ConfirmGuestOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(ConfirmGuestOrderCommand cmd, CancellationToken ct)
    {
        var zahtjev = await db.GuestOrderRequests
            .Include(r => r.Items)
            .Include(r => r.DeliveryZone)
            .Include(r => r.DeliverySlot)
            .FirstOrDefaultAsync(r => r.Token == cmd.Token, ct);

        if (zahtjev is null || zahtjev.Istekao(DateTime.UtcNow))
        {
            // Zahtjev koji je istekao ovdje ga i brišemo — čim ga neko zatekne istekloga,
            // nema razloga čekati pozadinskog čistača da to uradi kasnije.
            if (zahtjev is not null)
            {
                db.GuestOrderRequests.Remove(zahtjev);
                await db.SaveChangesAsync(ct);
            }

            // 410 je bio prvobitni plan (vidi dizajn dokument), ali ExceptionHandlingMiddleware
            // mapira samo 400/403/404/409, a frontend grana po `code`, ne po statusu — zato
            // ConflictException (409), čiji je Code i dalje "guest_request_expired".
            throw new ConflictException(
                "Potvrda je istekla ili je narudžba već primljena.", "guest_request_expired");
        }

        var items = new List<OrderItem>();
        foreach (var stavka in zahtjev.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == stavka.ProductId, ct);
            if (product is null || !product.IsAvailable)
                throw new DomainException(
                    $"Proizvod nije dostupan ({stavka.ProductName}).", "product_unavailable");

            items.Add(new OrderItem
            {
                ProductId = stavka.ProductId,
                ProductName = stavka.ProductName,
                Unit = stavka.Unit,
                Quantity = stavka.Quantity,
                UnitPrice = stavka.UnitPrice, // cijena iz zahtjeva — kupac plaća ono što je vidio, ne trenutnu cijenu
                ImageUrl = stavka.ImageUrl,
            });

            // Zalihe se skidaju tek sada — CreateGuestOrderRequestHandler ih namjerno
            // nije dirao dok je zahtjev bio samo nepotvrđena namjera.
            product.Stock = Math.Max(0, product.Stock - stavka.Quantity);
        }

        var order = new Order
        {
            Code = await OrderCode.GenerateAsync(db, ct),
            PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = null, // gost nema nalog
            CustomerName = zahtjev.CustomerName,
            CustomerPhone = zahtjev.CustomerPhone,
            CustomerEmail = zahtjev.CustomerEmail,
            Status = OrderStatus.Pending,
            Items = items,
            // Iznosi dolaze iz zahtjeva, ne preračunati — kupac je odobrio baš ove brojeve
            // kad je poslao formu, to je i smisao snimka.
            Subtotal = zahtjev.Subtotal,
            DeliveryFee = zahtjev.DeliveryFee,
            TotalAmount = zahtjev.TotalAmount,
            DeliverySlotId = zahtjev.DeliverySlotId,
            DeliverySlot = zahtjev.DeliverySlot,
            DeliveryZoneId = zahtjev.DeliveryZoneId,
            DeliveryZone = zahtjev.DeliveryZone,
            DeliveryAddress = zahtjev.DeliveryAddress,
            Note = zahtjev.Note,
            Latitude = zahtjev.Latitude,
            Longitude = zahtjev.Longitude,
        };

        db.Orders.Add(order);
        db.GuestOrderRequests.Remove(zahtjev);
        // Jedan SaveChangesAsync za narudžbu, njene stavke, skidanje zaliha i brisanje
        // zahtjeva — potvrda prođe cijela ili nikako, pa drugi klik na isti link ne može
        // zateći posao napola urađen (ni "narudžba postoji, zahtjev još nije obrisan" niti
        // obrnuto).
        await db.SaveChangesAsync(ct);

        try
        {
            // Suprotno od CreateGuestOrderRequestHandler-a, gdje pad slanja mejla mora da
            // sruši cijelu operaciju (bez linka gost nema kako da potvrdi zahtjev): ovdje
            // narudžba već postoji (upisana je gore) i njen opstanak ne smije zavisiti od
            // toga da li je mejl uspio, isto kao u CreateOrderHandler-u.
            if (!string.IsNullOrWhiteSpace(order.CustomerEmail))
                await email.SendOrderConfirmationAsync(order, ct);
            else
                await viber.SendOrderConfirmationAsync(order, ct);
        }
        catch
        {
            // Notification failures never block a confirmed order.
        }

        return order.ToDto();
    }
}

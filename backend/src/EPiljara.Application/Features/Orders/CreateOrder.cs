using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Application.Features.PromoKodovi;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Orders;

public record CustomerInput(string Name, string? Email, string Phone);

public record OrderItemInput(Guid ProductId, decimal Quantity);

/// <summary>
/// `CustomerId` i `PretplataId` NISU ulazi sa žice. Nijedan kontroler ih ne popunjava —
/// `OrdersController` gradi komandu iz tijela zahtjeva koje ta dva polja nema, jer bi inače
/// bilo ko mogao da naruči u tuđe ime. Postoje zbog jedinog pozivaoca koji nema prijavljenog
/// korisnika: pozadinskog posla pretplata. Narudžba bez vlasnika se ne bi pojavila kupcu pod
/// „Moje narudžbe", a pretplatna narudžba koja ne zna iz koje je pretplate nastala ne bi
/// mogla ni da uveća brojač isporuka ni da se pojavi na ekranu pretplate.
/// </summary>
public record CreateOrderCommand(
    CustomerInput Customer,
    Guid DeliveryZoneId,
    Guid DeliverySlotId,
    string DeliveryAddress,
    string? Note,
    IReadOnlyList<OrderItemInput> Items,
    double? Latitude = null,
    double? Longitude = null,
    Guid? CustomerId = null,
    Guid? PretplataId = null,
    string? PromoKod = null,
    bool IskoristiPoene = false) : IRequest<OrderDto>;

/// <summary>
/// Tijelo <c>POST /orders</c>. Namjerno bez <c>CustomerId</c> i <c>PretplataId</c>: dok je
/// kontroler vezivao <see cref="CreateOrderCommand"/> direktno, prijavljeni kupac je mogao
/// poslati tuđi <c>customerId</c> (narudžba na tuđem nalogu) ili <c>pretplataId</c>
/// (pretplatna cijena bez pretplate).
/// </summary>
public record CreateOrderRequest(
    CustomerInput Customer, Guid DeliveryZoneId, Guid DeliverySlotId, string DeliveryAddress,
    string? Note, IReadOnlyList<OrderItemInput> Items,
    double? Latitude = null, double? Longitude = null, string? PromoKod = null,
    bool IskoristiPoene = false)
{
    public CreateOrderCommand ToCommand() => new(
        Customer, DeliveryZoneId, DeliverySlotId, DeliveryAddress, Note, Items,
        Latitude, Longitude, PromoKod: PromoKod, IskoristiPoene: IskoristiPoene);
}

public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.Customer).NotNull().WithMessage("Podaci o kupcu su obavezni.");
        When(x => x.Customer is not null, () =>
        {
            RuleFor(x => x.Customer.Name).NotEmpty().WithMessage("Ime je obavezno.");
            RuleFor(x => x.Customer.Name).MaximumLength(160).WithMessage("Ime je predugačko.");
            RuleFor(x => x.Customer.Phone).NotEmpty().WithMessage("Telefon je obavezan.");
            RuleFor(x => x.Customer.Phone).MaximumLength(40).WithMessage("Telefon je predugačak.");
            // Email je opcionalan za prijavljenog kupca (Viber je zamjena), pa se dužina
            // provjerava samo kad je unesen — Orders.CustomerEmail je nullable(256).
            RuleFor(x => x.Customer.Email).MaximumLength(256)
                .When(x => !string.IsNullOrWhiteSpace(x.Customer.Email))
                .WithMessage("E-mail je predugačak.");
        });
        RuleFor(x => x.DeliveryZoneId).NotEmpty().WithMessage("Odaberi zonu dostave.");
        RuleFor(x => x.DeliverySlotId).NotEmpty().WithMessage("Odaberi termin dostave.");
        RuleFor(x => x.DeliveryAddress).NotEmpty().WithMessage("Adresa je obavezna.");
        RuleFor(x => x.DeliveryAddress).MaximumLength(400).WithMessage("Adresa je predugačka.");
        // Note je nullable — dužina se provjerava samo kad je unesena.
        RuleFor(x => x.Note).MaximumLength(1000)
            .When(x => x.Note is not null)
            .WithMessage("Napomena je predugačka.");
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
        RuleFor(x => x.PromoKod).MaximumLength(30)
            .When(x => x.PromoKod is not null)
            .WithMessage("Promo kod je predugačak.");
    }
}

public class CreateOrderHandler(
    IAppDbContext db,
    ICurrentUser current,
    IEmailService email,
    IViberService viber,
    IOptions<KorpiceOpcije> korpiceOpcije,
    IOptions<PretplateOpcije> pretplateOpcije,
    IOptions<PoeniOpcije> poeniOpcije)
    : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var zone = await db.DeliveryZones
            .FirstOrDefaultAsync(z => z.Id == cmd.DeliveryZoneId && z.IsActive, ct)
            ?? throw new DomainException("Nepoznata zona dostave.", "invalid_zone");

        var slot = await db.DeliverySlots.FirstOrDefaultAsync(s => s.Id == cmd.DeliverySlotId, ct)
            ?? throw new DomainException("Nepoznat termin dostave.", "invalid_slot");

        // Sedmica isporuke se računa iz ciklusa korpica, ne iz DateTime.Now — vidi
        // SedmicaHelper.SedmicaZaNarudzbu za razlog zašto poslije roka ide naredna sedmica.
        var (danIsporuke, vrijemeIsporuke, cutoffSati) =
            await SedmicaHelper.UcitajRokAsync(db, korpiceOpcije.Value, ct);
        var (godina, sedmica) =
            SedmicaHelper.SedmicaZaNarudzbu(danIsporuke, vrijemeIsporuke, cutoffSati);

        // Pretplatna cijena se računa ovdje, a ne u pozivaocu, iz istog razloga iz kojeg
        // pretplata uopšte ide kroz ovaj handler: zbir, dostava i ukupno se računaju na
        // jednom mjestu. Pravilo („šesta isporuka je prva jeftinija") i dalje stoji samo u
        // PretplatnaCijena — ovdje se poziva, ne ponavlja.
        Pretplata? pretplata = null;
        if (cmd.PretplataId is not null)
            pretplata = await db.Pretplate.FirstOrDefaultAsync(x => x.Id == cmd.PretplataId, ct);

        var items = new List<OrderItem>();
        foreach (var line in cmd.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            if (product is null || !product.IsAvailable)
                throw new DomainException($"Proizvod nije dostupan ({line.ProductId}).", "product_unavailable");

            // Cijena se snima u trenutku narudžbe. Za pretplatnu korpicu to je cijena
            // umanjena za popust koji je kupac dotad ZARADIO — popust visi o broju
            // isporučenih, ne o datumu pretplate, pa ga svaka naredna narudžba računa iznova.
            var jedinicna = pretplata is not null && product.Id == pretplata.ProductId
                ? PretplatnaCijena.Izracunaj(product.Price, pretplata.Plan, pretplateOpcije.Value)
                : product.Price;

            var stavka = new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit,
                Quantity = line.Quantity,
                UnitPrice = jedinicna,
                ImageUrl = product.ImageUrl,
            };

            // Korpica nosi sadržaj sedmice koju narudžba isporučuje. Isti poziv stoji i u
            // ConfirmGuestOrderHandler-u — vidi SnapshotKorpice za razlog zašto je ovo
            // zajednički pomoćnik, a ne kopiran blok.
            await SnapshotKorpice.PricvrstiAsync(db, stavka, product, godina, sedmica, ct);

            items.Add(stavka);

            product.Stock = Math.Max(0, product.Stock - line.Quantity);
        }

        var subtotal = Math.Round(items.Sum(i => i.UnitPrice * i.Quantity), 2);
        if (subtotal < zone.MinOrderAmount)
            throw new DomainException(
                $"Minimalna narudžba za zonu {zone.Name} je {zone.MinOrderAmount:0.##} KM.",
                "below_minimum");

        // Svako ko je prijavljen dobija narudžbu vezanu za svoj nalog — u ovoj prodavnici
        // kupuju i admin i vozači, a narudžba koju vlasnik ne može naći pod "Moje narudžbe"
        // je greška, ne zaštita. Ranije je ovdje stajao uslov `Role == UserRole.Customer`,
        // pa su narudžbe admina i vozača tiho ostajale sa CustomerId = NULL.
        // Gost nema nalog i ostaje nevezan — to je i dalje ispravno.
        // `cmd.CustomerId` ima prednost samo zato što ga postavlja pozivalac koji nema
        // ICurrentUser (posao pretplata). Kontroleri ga ne šalju, pa je za svaki zahtjev
        // sa žice ovo i dalje isključivo prijavljeni korisnik.
        var customerId = cmd.CustomerId ?? (current.IsAuthenticated ? current.Id : null);

        // Kod se provjerava tek kad je subtotal poznat (minimum koda) i poslije minimuma zone (O2).
        // Zalihe su gore već skinute u memoriji, ali ako kod padne izuzetak izlazi prije
        // SaveChangesAsync pa se ništa ne upisuje.
        // B1/B2: kupac (poeni) i kod (limit) se zaključavaju do upisa narudžbe. Transakcija se otvara
        // samo ako je pozivalac (posao pretplata) nije već otvorio.
        await using var tx = ZakljucavanjeNarudzbe.Podrzano(db) && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        await ZakljucavanjeNarudzbe.ZakljucajAsync(db, cmd.IskoristiPoene ? customerId : null, cmd.PromoKod, ct);

        PromoKod? promo = null;
        var popust = 0m;
        if (!string.IsNullOrWhiteSpace(cmd.PromoKod))
        {
            promo = await PromoKodPravila.ProvjeriAsync(db, cmd.PromoKod, subtotal,
                new KupacZaPromo(customerId, cmd.Customer.Email), LokalniDatum.Danas(), ct);
            popust = PromoKodPravila.Popust(promo, subtotal);
        }

        // Poeni poslije promo koda (O5) i samo za prijavljenog kupca — gost nema knjigu poena.
        var iskoristeniPoeni = 0;
        var poeniPopust = 0m;
        // Troši samo nalog kupca: admin koji unosi telefonsku narudžbu pod svojim nalogom
        // (CustomerId je njegov) ne smije potrošiti svoje poene.
        if (cmd.IskoristiPoene && customerId is { } kupacId
            && await db.Users.AnyAsync(u => u.Id == kupacId && u.Role == UserRole.Customer, ct))
        {
            var stanje = await PoeniPravila.StanjeAsync(db, kupacId, ct);
            (iskoristeniPoeni, poeniPopust) = PoeniPravila.ZaTrosenje(stanje, subtotal - popust, poeniOpcije.Value);
        }

        // Posao pretplata nema zahtjev (njegov ICurrentUser je uvijek latinica), pa pismo
        // pretplatne narudžbe dolazi sa naloga vlasnika.
        var pismo = cmd.PretplataId is not null && customerId is { } vlasnikId
            ? await db.Users.Where(u => u.Id == vlasnikId).Select(u => u.Pismo).FirstOrDefaultAsync(ct)
            : current.Pismo;

        var order = new Order
        {
            Code = await OrderCode.GenerateAsync(db, ct),
            PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = customerId,
            CustomerName = cmd.Customer.Name,
            CustomerPhone = cmd.Customer.Phone,
            CustomerEmail = cmd.Customer.Email,
            Status = OrderStatus.Pending,
            Items = items,
            Subtotal = subtotal,
            DeliveryFee = zone.DeliveryFee,
            PromoKodId = promo?.Id,
            PromoKod = promo?.Kod,
            Popust = popust,
            IskoristeniPoeni = iskoristeniPoeni,
            PoeniPopust = poeniPopust,
            TotalAmount = Math.Round(subtotal - popust - poeniPopust + zone.DeliveryFee, 2),
            DeliverySlotId = slot.Id,
            DeliverySlot = slot,
            DeliveryZoneId = zone.Id,
            DeliveryZone = zone,
            DeliveryAddress = cmd.DeliveryAddress,
            Note = cmd.Note,
            Godina = godina,
            Sedmica = sedmica,
            Latitude = cmd.Latitude,
            Longitude = cmd.Longitude,
            PretplataId = cmd.PretplataId,
            Pismo = pismo,
        };

        db.Orders.Add(order);
        // Stavka ide u isti upis kao narudžba: poeni se ne smiju potrošiti bez narudžbe ni obrnuto.
        if (iskoristeniPoeni > 0)
            db.PoeniStavke.Add(new PoeniStavka
            {
                UserId = customerId!.Value, Iznos = -iskoristeniPoeni,
                Vrsta = VrstaPoena.Iskoristeno, OrderId = order.Id,
            });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);

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
}

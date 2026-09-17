using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Account;

/// <summary>
/// Zajednička forma za komande koje nose podatke jedne adrese — omogućava da
/// <see cref="AddressRules"/> piše pravila jednom, sa pravim pristupom svojstvu
/// (x.FullAddress), a ne preko Func-a. Vidi napomenu na <see cref="AddressRules"/>.
/// </summary>
public interface IAddressFields
{
    string FullAddress { get; }
    double? Latitude { get; }
    double? Longitude { get; }
}

public record CreateAddressCommand(
    string? Label,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    Guid? DeliveryZoneId) : IRequest<AddressDto>, IAddressFields;

public record UpdateAddressCommand(
    Guid Id,
    string? Label,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    Guid? DeliveryZoneId) : IRequest<AddressDto>, IAddressFields;

public record DeleteAddressCommand(Guid Id) : IRequest<Unit>;

public record SetDefaultAddressCommand(Guid Id) : IRequest<AddressDto>;

/// <summary>
/// Pravila za tačku su ista kao u CreateOrderValidator i to je namjerno: adresa
/// odavde putuje u narudžbu, pa bi blaža provjera ovdje samo pomjerila grešku na
/// kasniji trenutak, kad je kupac već u checkoutu.
///
/// Sve poruke o tački idu pod ključ `fullAddress` jer forma ima jedno polje sa
/// mapom — poruka pod "latitude" ne bi imala gdje da se prikaže.
///
/// Napomena o realizaciji: prvobitna zamisao (RuleFor(x => accessor(x)) sa Func
/// akcesorima) je odbačena — FluentValidation izvodi ime svojstva iz member
/// izraza (x.Svojstvo), a poziv Func-a to nije, pa bi ime ispalo prazno/pogrešno
/// i poruke bi promašile ključ. Umjesto toga, obje komande implementiraju
/// IAddressFields, pa RuleFor(x => x.FullAddress) itd. ostaju pravi pristupi
/// svojstvu (preko interfejsa) i FluentValidation ih ispravno imenuje.
/// </summary>
public static class AddressRules
{
    public static void Apply<T>(AbstractValidator<T> v) where T : IAddressFields
    {
        v.RuleFor(x => x.FullAddress).NotEmpty().WithMessage("Adresa je obavezna.");
        v.RuleFor(x => x.FullAddress).MaximumLength(400).WithMessage("Adresa je predugačka.");

        v.RuleFor(x => x.Latitude).InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue)
            .OverridePropertyName("fullAddress")
            .WithMessage("Neispravna geografska širina.");
        v.RuleFor(x => x.Longitude).InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue)
            .OverridePropertyName("fullAddress")
            .WithMessage("Neispravna geografska dužina.");

        // Pola tačke nije tačka — pin bez jedne ose bi se vodio kao "ima lokaciju".
        v.RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .OverridePropertyName("fullAddress")
            .WithMessage("Potrebne su i geografska širina i dužina.");

        // Opseg cijelog globusa propušta zamijenjenu širinu/dužinu, pa tačka mora
        // pasti i u okvir Bosne i Hercegovine. Provjerava se tek kad su obje
        // vrijednosti u opsegu, da se poruke ne dupliraju.
        v.RuleFor(x => x)
            .Must(x => GeoBounds.Contains(x.Latitude!.Value, x.Longitude!.Value))
            .When(x => x.Latitude is >= -90 and <= 90 && x.Longitude is >= -180 and <= 180)
            .OverridePropertyName("fullAddress")
            .WithMessage(GeoBounds.OutsideMessage);
    }
}

public class CreateAddressValidator : AbstractValidator<CreateAddressCommand>
{
    public CreateAddressValidator() => AddressRules.Apply(this);
}

public class UpdateAddressValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressValidator() => AddressRules.Apply(this);
}

public class CreateAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<CreateAddressCommand, AddressDto>
{
    /// <summary>Granica postoji da spisak i tabela ne rastu bez kraja.</summary>
    public const int MaxAdresa = 10;

    public async Task<AddressDto> Handle(CreateAddressCommand cmd, CancellationToken ct)
    {
        // Broji se u handleru, ne u validatoru: validator ne vidi bazu.
        var moje = await db.Addresses.Where(a => a.UserId == current.Id!.Value).ToListAsync(ct);
        if (moje.Count >= MaxAdresa)
            throw new DomainException(
                $"Možete sačuvati najviše {MaxAdresa} adresa.", "address_limit");

        // Isti razlog kao u CreateOrderHandler: FK je Restrict, pa bi proizvoljan GUID
        // bez ove provjere pao na SaveChanges kao DbUpdateException -> 500, umjesto
        // tačnog 400. Neaktivna zona ostaje dozvoljena — DeliveryZoneActive u DTO-u
        // postoji baš zato što klijent smije sačuvati adresu sa ugašenom zonom.
        if (cmd.DeliveryZoneId is not null &&
            !await db.DeliveryZones.AnyAsync(z => z.Id == cmd.DeliveryZoneId.Value, ct))
            throw new DomainException("Nepoznata zona dostave.", "invalid_zone");

        var address = new Address
        {
            UserId = current.Id!.Value,
            Label = string.IsNullOrWhiteSpace(cmd.Label) ? null : cmd.Label.Trim(),
            FullAddress = cmd.FullAddress.Trim(),
            Latitude = cmd.Latitude,
            Longitude = cmd.Longitude,
            DeliveryZoneId = cmd.DeliveryZoneId,
            // Prva adresa je glavna sama od sebe — inače bi kupac imao adresar
            // bez glavne adrese i checkout bi se ponašao kao za gosta.
            IsDefault = moje.Count == 0,
        };

        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);

        await db.Addresses.Entry(address).Reference(a => a.DeliveryZone).LoadAsync(ct);
        return address.ToDto();
    }
}

public class UpdateAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(UpdateAddressCommand cmd, CancellationToken ct)
    {
        var address = await db.Addresses
            .Include(a => a.DeliveryZone)
            .FirstOrDefaultAsync(a => a.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Adresa nije pronađena.");

        // Vlasništvo se provjerava poslije dohvata, a ne filterom u upitu, da razlika
        // između "ne postoji" i "nije tvoja" ostane namjerna odluka.
        if (address.UserId != current.Id)
            throw new ForbiddenException("Ova adresa nije vaša.");

        // Isti razlog kao u CreateAddressHandler — vidi komentar tamo.
        if (cmd.DeliveryZoneId is not null &&
            !await db.DeliveryZones.AnyAsync(z => z.Id == cmd.DeliveryZoneId.Value, ct))
            throw new DomainException("Nepoznata zona dostave.", "invalid_zone");

        address.Label = string.IsNullOrWhiteSpace(cmd.Label) ? null : cmd.Label.Trim();
        address.FullAddress = cmd.FullAddress.Trim();
        address.Latitude = cmd.Latitude;
        address.Longitude = cmd.Longitude;
        address.DeliveryZoneId = cmd.DeliveryZoneId;

        await db.SaveChangesAsync(ct);
        // Include gore je učitao staru zonu (ili null); FK iznad se promijenio, pa EF-ova
        // relaciona fixup logika resetuje IsLoaded na navigaciji — LoadAsync ovdje stvarno
        // ide u bazu po NOVU zonu, ne vraća staru iz keša. Provjereno testom i curl-om
        // (task-3-report.md, I1): DeliveryZoneName u odgovoru prati DeliveryZoneId.
        await db.Addresses.Entry(address).Reference(a => a.DeliveryZone).LoadAsync(ct);
        return address.ToDto();
    }
}

public class DeleteAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<DeleteAddressCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAddressCommand cmd, CancellationToken ct)
    {
        // Sve korisnikove adrese u JEDNOM upitu, izmjena u memoriji, jedan SaveChanges.
        // Isti razlog kao kod admin dispečovanja: entitet označen za brisanje je u bazi
        // sve do SaveChanges, pa bi ponovni upit vratio red koji upravo brišemo.
        var moje = await db.Addresses.Where(a => a.UserId == current.Id!.Value).ToListAsync(ct);

        var address = moje.FirstOrDefault(a => a.Id == cmd.Id);
        if (address is null)
        {
            // Postoji li uopšte, ili je tuđa? Razlika je vidljiva korisniku.
            var postoji = await db.Addresses.AnyAsync(a => a.Id == cmd.Id, ct);
            if (postoji) throw new ForbiddenException("Ova adresa nije vaša.");
            throw new NotFoundException("Adresa nije pronađena.");
        }

        db.Addresses.Remove(address);

        // Ako je obrisana bila glavna, oznaka ide na najskorije dodanu preostalu.
        if (address.IsDefault)
        {
            var sljedeca = moje.Where(a => a.Id != address.Id)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();
            if (sljedeca is not null) sljedeca.IsDefault = true;
        }

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class SetDefaultAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<SetDefaultAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(SetDefaultAddressCommand cmd, CancellationToken ct)
    {
        var moje = await db.Addresses
            .Include(a => a.DeliveryZone)
            .Where(a => a.UserId == current.Id!.Value)
            .ToListAsync(ct);

        var address = moje.FirstOrDefault(a => a.Id == cmd.Id);
        if (address is null)
        {
            var postoji = await db.Addresses.AnyAsync(a => a.Id == cmd.Id, ct);
            if (postoji) throw new ForbiddenException("Ova adresa nije vaša.");
            throw new NotFoundException("Adresa nije pronađena.");
        }

        // Gašenje stare i paljenje nove u istom SaveChanges — nikad dva reda sa IsDefault.
        foreach (var a in moje) a.IsDefault = a.Id == address.Id;

        await db.SaveChangesAsync(ct);
        return address.ToDto();
    }
}

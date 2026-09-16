using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>
/// Jedno mjesto koje čita magacin iz baze, da se poruka o nedostajućem redu ne
/// prepisuje po pozivaocima. Red upisuje migracija, pa ga nema samo ako ga je
/// neko ručno obrisao — a tada je tiho računanje rute iz tačke 0°N 0°E gore od
/// pada, jer rezultat izgleda uvjerljivo.
/// </summary>
public static class MagacinCitac
{
    public const string NemaRedaPoruka =
        "Magacin nije podešen — tabela \"Magacin\" je prazna. Red upisuje migracija "
        + "MagacinUBazi; primijeni migracije ili vrati red ručno.";

    public static async Task<Magacin> UcitajAsync(IAppDbContext db, CancellationToken ct) =>
        await db.Magacin.FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException(NemaRedaPoruka);
}

/* ---------- Read ---------- */

public record GetWarehouseQuery : IRequest<WarehouseDto>;

public class GetWarehouseHandler(IAppDbContext db) : IRequestHandler<GetWarehouseQuery, WarehouseDto>
{
    public async Task<WarehouseDto> Handle(GetWarehouseQuery q, CancellationToken ct) =>
        (await MagacinCitac.UcitajAsync(db, ct)).ToDto();
}

/* ---------- Update ---------- */

public record UpdateWarehouseCommand(string Name, double Latitude, double Longitude)
    : IRequest<WarehouseDto>;

/// <summary>
/// Ista granica koju provjerava i start aplikacije, samo ovdje vraća 400 sa ključevima
/// polja. Bez toga bi admin kroz panel mogao upisati tačku koju start odbija, i prvi
/// sljedeći restart ne bi prošao — greška bi se pojavila daleko od mjesta gdje je napravljena.
/// </summary>
public class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Naziv je obavezan.");
        RuleFor(x => x.Latitude)
            .InclusiveBetween(GeoBounds.MinLatitude, GeoBounds.MaxLatitude)
            .WithMessage(GeoBounds.OutsideMessage);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(GeoBounds.MinLongitude, GeoBounds.MaxLongitude)
            .WithMessage(GeoBounds.OutsideMessage);
    }
}

public class UpdateWarehouseHandler(IAppDbContext db) : IRequestHandler<UpdateWarehouseCommand, WarehouseDto>
{
    public async Task<WarehouseDto> Handle(UpdateWarehouseCommand cmd, CancellationToken ct)
    {
        var magacin = await MagacinCitac.UcitajAsync(db, ct);

        magacin.Naziv = cmd.Name.Trim();
        magacin.Latitude = cmd.Latitude;
        magacin.Longitude = cmd.Longitude;

        await db.SaveChangesAsync(ct);
        return magacin.ToDto();
    }
}

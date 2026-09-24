using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>Shared payload for creating and updating drivers.</summary>
public record DriverInput(
    string Name,
    string Phone,
    string? Vozilo,
    string? Registracija,
    bool Aktivan);

public class DriverInputValidator : AbstractValidator<DriverInput>
{
    public DriverInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Ime je obavezno.");
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Telefon je obavezan.");
    }
}

/* ---------- Read ---------- */

public record GetDriversQuery : IRequest<IReadOnlyList<DriverDto>>;

public class GetDriversHandler(IAppDbContext db) : IRequestHandler<GetDriversQuery, IReadOnlyList<DriverDto>>
{
    public async Task<IReadOnlyList<DriverDto>> Handle(GetDriversQuery q, CancellationToken ct)
    {
        var drivers = await db.Drivers.OrderBy(d => d.Name).ToListAsync(ct);
        return drivers.Select(d => d.ToDto()).ToList();
    }
}

/* ---------- Create ---------- */

public record CreateDriverCommand(DriverInput Input) : IRequest<DriverDto>;

public class CreateDriverValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverValidator() => RuleFor(x => x.Input).SetValidator(new DriverInputValidator());
}

public class CreateDriverHandler(IAppDbContext db) : IRequestHandler<CreateDriverCommand, DriverDto>
{
    public async Task<DriverDto> Handle(CreateDriverCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        // UserId ostaje null: vozač postoji za dispečerski ekran i rute, a nalog za
        // mobilnu aplikaciju mu se dodjeljuje odvojeno.
        // Puno ime tipa: u ovom namespace-u `Driver` inače pogađa sestrinski
        // namespace EPiljara.Application.Features.Driver, ne entitet.
        var driver = new EPiljara.Domain.Entities.Driver
        {
            Name = input.Name.Trim(),
            Phone = input.Phone.Trim(),
            Vozilo = Ocisti(input.Vozilo),
            Registracija = Ocisti(input.Registracija),
            Aktivan = input.Aktivan,
        };

        db.Drivers.Add(driver);
        await db.SaveChangesAsync(ct);
        return driver.ToDto();
    }

    internal static string? Ocisti(string? vrijednost) =>
        string.IsNullOrWhiteSpace(vrijednost) ? null : vrijednost.Trim();
}

/* ---------- Update ---------- */

public record UpdateDriverCommand(Guid Id, DriverInput Input) : IRequest<DriverDto>;

public class UpdateDriverValidator : AbstractValidator<UpdateDriverCommand>
{
    public UpdateDriverValidator() => RuleFor(x => x.Input).SetValidator(new DriverInputValidator());
}

public class UpdateDriverHandler(IAppDbContext db) : IRequestHandler<UpdateDriverCommand, DriverDto>
{
    public async Task<DriverDto> Handle(UpdateDriverCommand cmd, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Vozač nije pronađen.");

        var input = cmd.Input;
        driver.Name = input.Name.Trim();
        driver.Phone = input.Phone.Trim();
        driver.Vozilo = CreateDriverHandler.Ocisti(input.Vozilo);
        driver.Registracija = CreateDriverHandler.Ocisti(input.Registracija);
        driver.Aktivan = input.Aktivan;

        await db.SaveChangesAsync(ct);
        return driver.ToDto();
    }
}

/* ---------- Delete ---------- */

public record DeleteDriverCommand(Guid Id) : IRequest<Guid>;

public class DeleteDriverHandler(IAppDbContext db) : IRequestHandler<DeleteDriverCommand, Guid>
{
    public async Task<Guid> Handle(DeleteDriverCommand cmd, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Vozač nije pronađen.");

        // Rute nose istoriju dana; brisanje vozača bi je pojelo. Zato se vozač koji je
        // ikad vozio penzioniše sa Aktivan=false, a ne briše.
        if (await db.DeliveryRoutes.AnyAsync(r => r.DriverId == cmd.Id, ct))
            throw new ConflictException(
                "Vozač ima rute i ne može se obrisati — postavi ga na neaktivan.", "driver_in_use");

        db.Drivers.Remove(driver);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>Shared payload for creating and updating producers.</summary>
public record ProducerInput(
    string Name,
    string Village,
    string? Description,
    string? PhotoUrl,
    double? Latitude,
    double? Longitude,
    string? Phone,
    // Podaci za otkupni list — svi opcioni.
    string? NosilacIme = null,
    string? Bpg = null,
    string? Jmbg = null,
    string? Adresa = null,
    string? ZiroRacun = null,
    string? Banka = null);

public class ProducerInputValidator : AbstractValidator<ProducerInput>
{
    public ProducerInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Naziv je obavezan.");
        RuleFor(x => x.Village).NotEmpty().WithMessage("Selo je obavezno.");
        RuleFor(x => x.Jmbg).Matches(@"^\d{13}$").WithMessage("JMBG ima tačno 13 cifara.")
            .When(x => !string.IsNullOrWhiteSpace(x.Jmbg));
        RuleFor(x => x.Bpg).Matches(@"^\d{1,20}$").WithMessage("Broj gazdinstva (BPG) sadrži samo cifre, najviše 20.")
            .When(x => !string.IsNullOrWhiteSpace(x.Bpg));
        RuleFor(x => x.NosilacIme).MaximumLength(200);
        RuleFor(x => x.Adresa).MaximumLength(200);
        RuleFor(x => x.ZiroRacun).MaximumLength(200);
        RuleFor(x => x.Banka).MaximumLength(200);
    }
}

/// <summary>Podaci za otkupni list: prazan unos se čuva kao null („nije uneseno").</summary>
internal static class PodaciOtkupa
{
    public static void Primijeni(Producer p, ProducerInput input)
    {
        p.NosilacIme = Ocisti(input.NosilacIme);
        p.Bpg = Ocisti(input.Bpg);
        p.Jmbg = Ocisti(input.Jmbg);
        p.Adresa = Ocisti(input.Adresa);
        p.ZiroRacun = Ocisti(input.ZiroRacun);
        p.Banka = Ocisti(input.Banka);
    }

    private static string? Ocisti(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/* ---------- Create ---------- */

public record CreateProducerCommand(ProducerInput Input) : IRequest<AdminProducerDto>;

public class CreateProducerValidator : AbstractValidator<CreateProducerCommand>
{
    public CreateProducerValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input).SetValidator(new ProducerInputValidator());
    }
}

public class CreateProducerHandler(IAppDbContext db) : IRequestHandler<CreateProducerCommand, AdminProducerDto>
{
    public async Task<AdminProducerDto> Handle(CreateProducerCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        var producer = new Producer
        {
            Name = input.Name.Trim(),
            Village = input.Village.Trim(),
            Description = input.Description ?? string.Empty,
            PhotoUrl = input.PhotoUrl,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            Phone = input.Phone,
        };
        PodaciOtkupa.Primijeni(producer, input);

        db.Producers.Add(producer);
        await db.SaveChangesAsync(ct);
        return producer.ToAdminDto();
    }
}

/* ---------- Update ---------- */

public record UpdateProducerCommand(Guid Id, ProducerInput Input) : IRequest<AdminProducerDto>;

public class UpdateProducerValidator : AbstractValidator<UpdateProducerCommand>
{
    public UpdateProducerValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input).SetValidator(new ProducerInputValidator());
    }
}

public class UpdateProducerHandler(IAppDbContext db) : IRequestHandler<UpdateProducerCommand, AdminProducerDto>
{
    public async Task<AdminProducerDto> Handle(UpdateProducerCommand cmd, CancellationToken ct)
    {
        var producer = await db.Producers.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Proizvođač nije pronađen.");

        var input = cmd.Input;
        producer.Name = input.Name.Trim();
        producer.Village = input.Village.Trim();
        producer.Description = input.Description ?? string.Empty;
        producer.PhotoUrl = input.PhotoUrl;
        producer.Latitude = input.Latitude;
        producer.Longitude = input.Longitude;
        producer.Phone = input.Phone;
        PodaciOtkupa.Primijeni(producer, input);

        await db.SaveChangesAsync(ct);
        return producer.ToAdminDto();
    }
}

/* ---------- Delete ---------- */

public record DeleteProducerCommand(Guid Id) : IRequest<Guid>;

public class DeleteProducerHandler(IAppDbContext db) : IRequestHandler<DeleteProducerCommand, Guid>
{
    public async Task<Guid> Handle(DeleteProducerCommand cmd, CancellationToken ct)
    {
        var producer = await db.Producers.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Proizvođač nije pronađen.");

        // Even soft-deleted products still hold the FK, so guard with a clear 409.
        // Isto važi za analize i otkupne listove: dokument ne smije ostati bez prodavca.
        if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.ProducerId == cmd.Id, ct)
            || await db.Analize.AnyAsync(a => a.ProducerId == cmd.Id, ct)
            || await db.OtkupniListovi.AnyAsync(l => l.ProducerId == cmd.Id, ct))
            throw new ConflictException(
                "Proizvođač ima proizvode, analize ili otkupne listove i ne može se obrisati.", "producer_in_use");

        db.Producers.Remove(producer);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

/* ---------- List (admin) ---------- */

/// <summary>Sva gazdinstva sa podacima za otkupni list — samo za admina.</summary>
public record GetAdminProducersQuery : IRequest<IReadOnlyList<AdminProducerDto>>;

public class GetAdminProducersHandler(IAppDbContext db)
    : IRequestHandler<GetAdminProducersQuery, IReadOnlyList<AdminProducerDto>>
{
    public async Task<IReadOnlyList<AdminProducerDto>> Handle(GetAdminProducersQuery q, CancellationToken ct) =>
        (await db.Producers.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct))
            .Select(p => p.ToAdminDto())
            .ToList();
}

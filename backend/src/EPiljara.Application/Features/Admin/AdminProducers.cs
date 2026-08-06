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
    string? Phone);

public class ProducerInputValidator : AbstractValidator<ProducerInput>
{
    public ProducerInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Naziv je obavezan.");
        RuleFor(x => x.Village).NotEmpty().WithMessage("Selo je obavezno.");
    }
}

/* ---------- Create ---------- */

public record CreateProducerCommand(ProducerInput Input) : IRequest<ProducerDto>;

public class CreateProducerValidator : AbstractValidator<CreateProducerCommand>
{
    public CreateProducerValidator() => RuleFor(x => x.Input).SetValidator(new ProducerInputValidator());
}

public class CreateProducerHandler(IAppDbContext db) : IRequestHandler<CreateProducerCommand, ProducerDto>
{
    public async Task<ProducerDto> Handle(CreateProducerCommand cmd, CancellationToken ct)
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

        db.Producers.Add(producer);
        await db.SaveChangesAsync(ct);
        return producer.ToDto();
    }
}

/* ---------- Update ---------- */

public record UpdateProducerCommand(Guid Id, ProducerInput Input) : IRequest<ProducerDto>;

public class UpdateProducerValidator : AbstractValidator<UpdateProducerCommand>
{
    public UpdateProducerValidator() => RuleFor(x => x.Input).SetValidator(new ProducerInputValidator());
}

public class UpdateProducerHandler(IAppDbContext db) : IRequestHandler<UpdateProducerCommand, ProducerDto>
{
    public async Task<ProducerDto> Handle(UpdateProducerCommand cmd, CancellationToken ct)
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

        await db.SaveChangesAsync(ct);
        return producer.ToDto();
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
        if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.ProducerId == cmd.Id, ct))
            throw new ConflictException(
                "Proizvođač ima proizvode i ne može se obrisati.", "producer_in_use");

        db.Producers.Remove(producer);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

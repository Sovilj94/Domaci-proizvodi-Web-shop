using System.Text.RegularExpressions;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/*
 * Zone i termini dostave iz admin panela. Do sada su postojali samo u seedu — cijena dostave,
 * minimum, dani i rok su se mijenjali u bazi. Zona se ne briše (narudžbe i pretplate je pamte),
 * nego gasi (`IsActive`). Termin se briše samo dok ga nijedna narudžba ni pretplata ne koristi.
 *
 * Pažnja: termin sa najnižim danom određuje rok cijele sedmice (SedmicaHelper.UcitajRokAsync),
 * pa promjena njegovog dana, sata ili `CutoffHours` pomjera rok svima.
 */

public record ZonaInput(string Name, decimal MinOrderAmount, decimal DeliveryFee, bool IsActive);

public class ZonaInputValidator : AbstractValidator<ZonaInput>
{
    public ZonaInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Upiši naziv zone.")
            .MaximumLength(80).WithMessage("Naziv može imati najviše 80 znakova.");
        RuleFor(x => x.MinOrderAmount).InclusiveBetween(0, 10_000).WithMessage("Minimum mora biti između 0 i 10 000 KM.");
        RuleFor(x => x.DeliveryFee).InclusiveBetween(0, 1_000).WithMessage("Cijena dostave mora biti između 0 i 1 000 KM.");
    }
}

public record GetAdminZoneQuery : IRequest<IReadOnlyList<DeliveryZoneDto>>;

public class GetAdminZoneHandler(IAppDbContext db) : IRequestHandler<GetAdminZoneQuery, IReadOnlyList<DeliveryZoneDto>>
{
    public async Task<IReadOnlyList<DeliveryZoneDto>> Handle(GetAdminZoneQuery q, CancellationToken ct) =>
        (await db.DeliveryZones.OrderBy(z => z.Name).ToListAsync(ct)).Select(z => z.ToDto()).ToList();
}

public record CreateZonaCommand(ZonaInput Input) : IRequest<DeliveryZoneDto>;

public class CreateZonaValidator : AbstractValidator<CreateZonaCommand>
{
    public CreateZonaValidator() =>
        RuleFor(x => x.Input).NotNull().WithMessage("Podaci o zoni su obavezni.").SetValidator(new ZonaInputValidator());
}

public class CreateZonaHandler(IAppDbContext db) : IRequestHandler<CreateZonaCommand, DeliveryZoneDto>
{
    public async Task<DeliveryZoneDto> Handle(CreateZonaCommand cmd, CancellationToken ct)
    {
        var naziv = cmd.Input.Name.Trim();
        if (await db.DeliveryZones.AnyAsync(z => z.Name.ToLower() == naziv.ToLower(), ct))
            throw new ConflictException("Zona sa ovim nazivom već postoji.", "zona_postoji");

        var z = new DeliveryZone();
        Upisi(z, cmd.Input);
        db.DeliveryZones.Add(z);
        await db.SaveChangesAsync(ct);
        return z.ToDto();
    }

    internal static void Upisi(DeliveryZone z, ZonaInput i)
    {
        z.Name = i.Name.Trim();
        z.MinOrderAmount = i.MinOrderAmount;
        z.DeliveryFee = i.DeliveryFee;
        z.IsActive = i.IsActive;
    }
}

public record UpdateZonaCommand(Guid Id, ZonaInput Input) : IRequest<DeliveryZoneDto>;

public class UpdateZonaValidator : AbstractValidator<UpdateZonaCommand>
{
    public UpdateZonaValidator() =>
        RuleFor(x => x.Input).NotNull().WithMessage("Podaci o zoni su obavezni.").SetValidator(new ZonaInputValidator());
}

public class UpdateZonaHandler(IAppDbContext db) : IRequestHandler<UpdateZonaCommand, DeliveryZoneDto>
{
    public async Task<DeliveryZoneDto> Handle(UpdateZonaCommand cmd, CancellationToken ct)
    {
        var z = await db.DeliveryZones.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Zona nije pronađena.");
        var naziv = cmd.Input.Name.Trim();
        if (await db.DeliveryZones.AnyAsync(x => x.Id != cmd.Id && x.Name.ToLower() == naziv.ToLower(), ct))
            throw new ConflictException("Zona sa ovim nazivom već postoji.", "zona_postoji");

        CreateZonaHandler.Upisi(z, cmd.Input);
        await db.SaveChangesAsync(ct);
        return z.ToDto();
    }
}

/* ---------- Termini ---------- */

public record TerminInput(int DayOfWeek, string TimeFrom, string TimeTo, int CutoffHours, string? Label);

public partial class TerminInputValidator : AbstractValidator<TerminInput>
{
    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")]
    private static partial Regex Vrijeme();

    public TerminInputValidator()
    {
        RuleFor(x => x.DayOfWeek).InclusiveBetween(0, 6).WithMessage("Izaberi dan.");
        RuleFor(x => x.TimeFrom).Must(v => v is not null && Vrijeme().IsMatch(v)).WithMessage("Vrijeme upiši kao SS:MM, npr. 16:00.");
        RuleFor(x => x.TimeTo).Must(v => v is not null && Vrijeme().IsMatch(v)).WithMessage("Vrijeme upiši kao SS:MM, npr. 20:00.")
            .Must((x, kraj) => string.CompareOrdinal(kraj, x.TimeFrom) > 0)
            .When(x => x.TimeFrom is not null && Vrijeme().IsMatch(x.TimeFrom) && x.TimeTo is not null && Vrijeme().IsMatch(x.TimeTo))
            .WithMessage("Kraj termina mora biti poslije početka.");
        RuleFor(x => x.CutoffHours).InclusiveBetween(0, 168).WithMessage("Rok mora biti između 0 i 168 sati prije isporuke.");
        RuleFor(x => x.Label).MaximumLength(60).WithMessage("Oznaka može imati najviše 60 znakova.");
    }
}

public record GetAdminTerminiQuery : IRequest<IReadOnlyList<DeliverySlotDto>>;

public class GetAdminTerminiHandler(IAppDbContext db) : IRequestHandler<GetAdminTerminiQuery, IReadOnlyList<DeliverySlotDto>>
{
    public async Task<IReadOnlyList<DeliverySlotDto>> Handle(GetAdminTerminiQuery q, CancellationToken ct) =>
        (await db.DeliverySlots.OrderBy(s => s.DayOfWeek).ThenBy(s => s.TimeFrom).ToListAsync(ct))
            .Select(s => s.ToDto()).ToList();
}

public record CreateTerminCommand(TerminInput Input) : IRequest<DeliverySlotDto>;

public class CreateTerminValidator : AbstractValidator<CreateTerminCommand>
{
    public CreateTerminValidator() =>
        RuleFor(x => x.Input).NotNull().WithMessage("Podaci o terminu su obavezni.").SetValidator(new TerminInputValidator());
}

public class CreateTerminHandler(IAppDbContext db) : IRequestHandler<CreateTerminCommand, DeliverySlotDto>
{
    public async Task<DeliverySlotDto> Handle(CreateTerminCommand cmd, CancellationToken ct)
    {
        var s = new DeliverySlot();
        Upisi(s, cmd.Input);
        db.DeliverySlots.Add(s);
        await db.SaveChangesAsync(ct);
        return s.ToDto();
    }

    internal static void Upisi(DeliverySlot s, TerminInput i)
    {
        s.DayOfWeek = i.DayOfWeek;
        s.TimeFrom = i.TimeFrom;
        s.TimeTo = i.TimeTo;
        s.CutoffHours = i.CutoffHours;
        s.Label = string.IsNullOrWhiteSpace(i.Label) ? null : i.Label.Trim();
    }
}

public record UpdateTerminCommand(Guid Id, TerminInput Input) : IRequest<DeliverySlotDto>;

public class UpdateTerminValidator : AbstractValidator<UpdateTerminCommand>
{
    public UpdateTerminValidator() =>
        RuleFor(x => x.Input).NotNull().WithMessage("Podaci o terminu su obavezni.").SetValidator(new TerminInputValidator());
}

public class UpdateTerminHandler(IAppDbContext db) : IRequestHandler<UpdateTerminCommand, DeliverySlotDto>
{
    public async Task<DeliverySlotDto> Handle(UpdateTerminCommand cmd, CancellationToken ct)
    {
        var s = await db.DeliverySlots.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Termin nije pronađen.");
        CreateTerminHandler.Upisi(s, cmd.Input);
        await db.SaveChangesAsync(ct);
        return s.ToDto();
    }
}

public record DeleteTerminCommand(Guid Id) : IRequest<Guid>;

public class DeleteTerminHandler(IAppDbContext db) : IRequestHandler<DeleteTerminCommand, Guid>
{
    public async Task<Guid> Handle(DeleteTerminCommand cmd, CancellationToken ct)
    {
        var s = await db.DeliverySlots.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Termin nije pronađen.");
        if (await db.Orders.AnyAsync(o => o.DeliverySlotId == s.Id, ct)
            || await db.Pretplate.AnyAsync(p => p.DeliverySlotId == s.Id, ct))
            throw new ConflictException(
                "Termin koriste narudžbe ili pretplate, pa se ne može obrisati. Promijeni mu vrijeme umjesto brisanja.",
                "termin_u_upotrebi");
        if (await db.DeliverySlots.CountAsync(ct) == 1)
            throw new ConflictException("Ovo je jedini termin — bez njega se ne može naručiti.", "jedini_termin");

        db.DeliverySlots.Remove(s);
        await db.SaveChangesAsync(ct);
        return s.Id;
    }
}

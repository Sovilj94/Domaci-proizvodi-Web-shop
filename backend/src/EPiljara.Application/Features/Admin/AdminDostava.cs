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
 * Termini dostave iz admin panela (zone su postale naselja, vidi AdminNaselja). Do sada su postojali samo u seedu — cijena dostave,
 * minimum, dani i rok su se mijenjali u bazi. Zona se ne briše (narudžbe i pretplate je pamte),
 * nego gasi (`IsActive`). Termin se briše samo dok ga nijedna narudžba ni pretplata ne koristi.
 *
 * Pažnja: termin sa najnižim danom određuje rok cijele sedmice (SedmicaHelper.UcitajRokAsync),
 * pa promjena njegovog dana, sata ili `CutoffHours` pomjera rok svima.
 */

// Zone su postale naselja — vidi Features/Naselja/AdminNaselja.cs.

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

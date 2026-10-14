using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Ocjene;

internal static class OcjenaPravila
{
    /// <summary>Zajedničko za vlasnika i gosta: provjera statusa, jedna ocjena, upis.</summary>
    public static async Task<OcjenaDto> OcijeniAsync(
        IAppDbContext db, Order order, OcjenaInput input, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Delivered)
            throw new DomainException("Ocijeniti se može samo isporučena narudžba.", "narudzba_nije_isporucena");
        if (await db.OcjeneIsporuke.AnyAsync(o => o.OrderId == order.Id, ct))
            throw new ConflictException("Ova narudžba je već ocijenjena.", "vec_ocijenjeno");

        var ocjena = new OcjenaIsporuke
        {
            OrderId = order.Id,
            Ocjena = input.Ocjena,
            Komentar = (input.Komentar ?? "").Trim(),
            DozvolaZaObjavu = input.DozvolaZaObjavu,
        };
        db.OcjeneIsporuke.Add(ocjena);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Dva istovremena zahtjeva prođu AnyAsync; jedinstven indeks propusti samo prvi.
            throw new ConflictException("Ova narudžba je već ocijenjena.", "vec_ocijenjeno");
        }
        return ocjena.ToDto();
    }
}

public record OcjenaInput(int Ocjena, string? Komentar, bool DozvolaZaObjavu);

public class OcjenaInputValidator : AbstractValidator<OcjenaInput>
{
    public OcjenaInputValidator()
    {
        RuleFor(x => x.Ocjena).InclusiveBetween(1, 5)
            .WithName("Ocjena").OverridePropertyName("ocjena")
            .WithMessage("Ocjena mora biti od 1 do 5.");
        // Provjera ide nad trimovanom vrijednošću — handler upisuje trimovano.
        RuleFor(x => (x.Komentar ?? "").Trim()).MaximumLength(1000)
            .WithName("Komentar").OverridePropertyName("komentar")
            .WithMessage("Komentar može imati najviše 1000 znakova.");
    }
}

/* ---------- Vlasnik ---------- */

public record OcijeniNarudzbuCommand(Guid OrderId, OcjenaInput Input) : IRequest<OcjenaDto>;

public class OcijeniNarudzbuValidator : AbstractValidator<OcijeniNarudzbuCommand>
{
    public OcijeniNarudzbuValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new OcjenaInputValidator()).When(x => x.Input is not null);
    }
}

public class OcijeniNarudzbuHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<OcijeniNarudzbuCommand, OcjenaDto>
{
    public async Task<OcjenaDto> Handle(OcijeniNarudzbuCommand cmd, CancellationToken ct)
    {
        // Tuđa narudžba se ne razlikuje od nepostojeće — ne otkriva se da postoji.
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId && o.CustomerId == current.Id, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        return await OcjenaPravila.OcijeniAsync(db, order, cmd.Input, ct);
    }
}

/* ---------- Gost preko tokena ---------- */

public record OcijeniPrekoTokenaCommand(string Token, OcjenaInput Input) : IRequest<OcjenaDto>;

public class OcijeniPrekoTokenaValidator : AbstractValidator<OcijeniPrekoTokenaCommand>
{
    public OcijeniPrekoTokenaValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new OcjenaInputValidator()).When(x => x.Input is not null);
    }
}

public class OcijeniPrekoTokenaHandler(IAppDbContext db)
    : IRequestHandler<OcijeniPrekoTokenaCommand, OcjenaDto>
{
    public async Task<OcjenaDto> Handle(OcijeniPrekoTokenaCommand cmd, CancellationToken ct)
    {
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.PublicToken == cmd.Token, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        return await OcjenaPravila.OcijeniAsync(db, order, cmd.Input, ct);
    }
}

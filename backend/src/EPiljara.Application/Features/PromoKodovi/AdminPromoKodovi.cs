using System.Text.RegularExpressions;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.PromoKodovi;

internal static class PromoKodMapiranje
{
    /// <summary>Jedina definicija „računa se u limit": narudžbe sa kodom koje nisu otkazane.</summary>
    public static IQueryable<Order> NeotkazaneSaKodom(this IQueryable<Order> q) =>
        q.Where(o => o.PromoKodId != null && o.Status != OrderStatus.Cancelled);

    public static PromoKodDto ToDto(this PromoKod k, int brojKoristenja, bool mozeSeObrisati) => new()
    {
        Id = k.Id,
        Kod = k.Kod,
        Opis = k.Opis,
        Tip = k.Tip.ToString(),
        Vrijednost = k.Vrijednost,
        MinIznos = k.MinIznos,
        VaziOd = k.VaziOd,
        VaziDo = k.VaziDo,
        MaxKoristenja = k.MaxKoristenja,
        JednomPoKupcu = k.JednomPoKupcu,
        SamoNoviKupci = k.SamoNoviKupci,
        Aktivan = k.Aktivan,
        BrojKoristenja = brojKoristenja,
        MozeSeObrisati = mozeSeObrisati,
        Kreiran = k.Kreiran,
    };
}

public record GetAdminPromoKodoviQuery : IRequest<IReadOnlyList<PromoKodDto>>;

public class GetAdminPromoKodoviHandler(IAppDbContext db)
    : IRequestHandler<GetAdminPromoKodoviQuery, IReadOnlyList<PromoKodDto>>
{
    public async Task<IReadOnlyList<PromoKodDto>> Handle(GetAdminPromoKodoviQuery q, CancellationToken ct)
    {
        var kodovi = await db.PromoKodovi.OrderByDescending(k => k.Kreiran).ToListAsync(ct);
        // Jedan grupisani upit umjesto brojanja po kodu; otkazane se ne broje jer ne troše limit.
        var brojevi = await db.Orders
            .NeotkazaneSaKodom()
            .GroupBy(o => o.PromoKodId!.Value)
            .Select(g => new { Id = g.Key, Broj = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Broj, ct);
        // Za brisanje se računaju i otkazane (FK je Restrict) — drugi grupisani upit, ne N+1.
        var saNarudzbama = (await db.Orders
            .Where(o => o.PromoKodId != null)
            .Select(o => o.PromoKodId!.Value)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
        return kodovi.Select(k => k.ToDto(
            brojevi.GetValueOrDefault(k.Id), !saNarudzbama.Contains(k.Id))).ToList();
    }
}

public record PromoKodInput(
    string Kod, string? Opis, string Tip, decimal Vrijednost, decimal? MinIznos,
    DateOnly? VaziOd, DateOnly? VaziDo, int? MaxKoristenja,
    bool JednomPoKupcu, bool SamoNoviKupci, bool Aktivan);

public class PromoKodInputValidator : AbstractValidator<PromoKodInput>
{
    private static readonly Regex KodOblik = new("^[A-Z0-9-]{3,30}$", RegexOptions.Compiled);

    internal static bool TipJeValidan(string? tip, out TipPopusta rez) =>
        Enum.TryParse(tip, out rez) && Enum.IsDefined(rez);

    public PromoKodInputValidator()
    {
        // Provjera ide nad normalizovanim kodom — handler upisuje normalizovan, pa bi
        // „ jesen5 " inače bilo odbijeno iako se upisuje kao ispravno „JESEN5".
        RuleFor(x => PromoKodPravila.Normalizuj(x.Kod)).Must(k => KodOblik.IsMatch(k))
            .WithName("Kod").OverridePropertyName("kod")
            .WithMessage("Kod mora imati 3 do 30 znakova: slova A–Z, cifre i crtica.");
        RuleFor(x => (x.Opis ?? "").Trim()).MaximumLength(200)
            .WithName("Opis").OverridePropertyName("opis")
            .WithMessage("Opis može imati najviše 200 znakova.");
        RuleFor(x => x.Tip).Must(t => TipJeValidan(t, out _))
            .WithMessage("Tip popusta mora biti Procenat ili Iznos.");

        // Granice zavise od tipa; kad je tip nevažeći, javlja se samo greška tipa.
        When(x => TipJeValidan(x.Tip, out var t) && t == TipPopusta.Procenat, () =>
            RuleFor(x => x.Vrijednost).InclusiveBetween(1m, 90m)
                .WithMessage("Procenat mora biti između 1 i 90."));
        When(x => TipJeValidan(x.Tip, out var t) && t == TipPopusta.Iznos, () =>
            RuleFor(x => x.Vrijednost).InclusiveBetween(0.01m, 1000m)
                .WithMessage("Iznos popusta mora biti između 0,01 i 1000 KM."));

        RuleFor(x => x.MinIznos!.Value).GreaterThanOrEqualTo(0m)
            .When(x => x.MinIznos is not null)
            .WithName("MinIznos").OverridePropertyName("minIznos")
            .WithMessage("Minimalni iznos ne može biti negativan.");
        RuleFor(x => x.VaziDo).Must((x, kraj) => kraj >= x.VaziOd)
            .When(x => x.VaziOd is not null && x.VaziDo is not null)
            .WithMessage("Kraj važenja ne može biti prije početka.");
        RuleFor(x => x.MaxKoristenja!.Value).GreaterThanOrEqualTo(1)
            .When(x => x.MaxKoristenja is not null)
            .WithName("MaxKoristenja").OverridePropertyName("maxKoristenja")
            .WithMessage("Limit korišćenja mora biti bar 1.");
    }
}

public record CreatePromoKodCommand(PromoKodInput Input) : IRequest<PromoKodDto>;

public class CreatePromoKodValidator : AbstractValidator<CreatePromoKodCommand>
{
    public CreatePromoKodValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Podaci o kodu su obavezni.")
            .SetValidator(new PromoKodInputValidator());
    }
}

public class CreatePromoKodHandler(IAppDbContext db) : IRequestHandler<CreatePromoKodCommand, PromoKodDto>
{
    public async Task<PromoKodDto> Handle(CreatePromoKodCommand cmd, CancellationToken ct)
    {
        var kodTekst = PromoKodPravila.Normalizuj(cmd.Input.Kod);
        if (await db.PromoKodovi.AnyAsync(k => k.Kod == kodTekst, ct))
            throw Postoji();

        var kod = new PromoKod { Kod = kodTekst };
        Upisi(kod, cmd.Input);
        db.PromoKodovi.Add(kod);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Dva istovremena zahtjeva prođu AnyAsync; jedinstven indeks propusti samo prvi.
            // Konflikt prijavljujemo samo ako red zaista postoji; ostale greške propuštamo dalje.
            db.PromoKodovi.Entry(kod).State = EntityState.Detached;
            if (await db.PromoKodovi.AnyAsync(k => k.Kod == kodTekst, ct))
                throw Postoji();
            throw;
        }
        return kod.ToDto(0, true);
    }

    private static ConflictException Postoji() =>
        new("Promo kod sa ovim nazivom već postoji.", "promo_kod_postoji");

    /// <summary>Kod se ovdje ne upisuje: poslije kreiranja je nepromjenjiv (narudžbe ga pamte).</summary>
    internal static void Upisi(PromoKod k, PromoKodInput input)
    {
        k.Opis = (input.Opis ?? "").Trim();
        k.Tip = Enum.Parse<TipPopusta>(input.Tip);
        k.Vrijednost = input.Vrijednost;
        k.MinIznos = input.MinIznos;
        k.VaziOd = input.VaziOd;
        k.VaziDo = input.VaziDo;
        k.MaxKoristenja = input.MaxKoristenja;
        k.JednomPoKupcu = input.JednomPoKupcu;
        k.SamoNoviKupci = input.SamoNoviKupci;
        k.Aktivan = input.Aktivan;
    }
}

public record UpdatePromoKodCommand(Guid Id, PromoKodInput Input) : IRequest<PromoKodDto>;

public class UpdatePromoKodValidator : AbstractValidator<UpdatePromoKodCommand>
{
    public UpdatePromoKodValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Podaci o kodu su obavezni.")
            .SetValidator(new PromoKodInputValidator());
    }
}

public class UpdatePromoKodHandler(IAppDbContext db) : IRequestHandler<UpdatePromoKodCommand, PromoKodDto>
{
    public async Task<PromoKodDto> Handle(UpdatePromoKodCommand cmd, CancellationToken ct)
    {
        var kod = await db.PromoKodovi.FirstOrDefaultAsync(k => k.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Promo kod nije pronađen.");
        // Kod iz ulaza se ignoriše (nepromjenjiv); admin forma ga ionako vraća nepromijenjenog.
        CreatePromoKodHandler.Upisi(kod, cmd.Input);
        await db.SaveChangesAsync(ct);
        var broj = await db.Orders.NeotkazaneSaKodom().CountAsync(
            o => o.PromoKodId == kod.Id, ct);
        var saNarudzbom = await db.Orders.AnyAsync(o => o.PromoKodId == kod.Id, ct);
        return kod.ToDto(broj, !saNarudzbom);
    }
}

public record DeletePromoKodCommand(Guid Id) : IRequest<Guid>;

public class DeletePromoKodHandler(IAppDbContext db) : IRequestHandler<DeletePromoKodCommand, Guid>
{
    public async Task<Guid> Handle(DeletePromoKodCommand cmd, CancellationToken ct)
    {
        var kod = await db.PromoKodovi.FirstOrDefaultAsync(k => k.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Promo kod nije pronađen.");
        // Brojimo i otkazane narudžbe: FK je Restrict, pa bi brisanje palo na bazi.
        if (await db.Orders.AnyAsync(o => o.PromoKodId == cmd.Id, ct))
            throw new ConflictException(
                "Kod je korišćen na narudžbama — isključi ga umjesto brisanja.", "promo_kod_koristen");
        db.PromoKodovi.Remove(kod);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

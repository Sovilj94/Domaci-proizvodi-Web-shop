using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Poeni;

/* ---------- Lista kupaca sa stanjem (B4) ---------- */

public record GetAdminPoeniKupciQuery(string? Pretraga) : IRequest<IReadOnlyList<AdminPoeniKupacDto>>;

public class GetAdminPoeniKupciHandler(IAppDbContext db)
    : IRequestHandler<GetAdminPoeniKupciQuery, IReadOnlyList<AdminPoeniKupacDto>>
{
    private const int Najvise = 100;

    public async Task<IReadOnlyList<AdminPoeniKupacDto>> Handle(GetAdminPoeniKupciQuery q, CancellationToken ct)
    {
        var upit = db.Users.Where(u => u.Role == UserRole.Customer);

        var pretraga = q.Pretraga?.Trim().ToLower();
        if (!string.IsNullOrEmpty(pretraga))
            upit = upit.Where(u => u.Name.ToLower().Contains(pretraga) || u.Email.ToLower().Contains(pretraga));

        // Stanje je zbir knjige (PoeniStavka) — vidi tamo zašto kolone sa stanjem nema.
        return await upit
            .Select(u => new AdminPoeniKupacDto
            {
                UserId = u.Id,
                Ime = u.Name,
                Email = u.Email,
                Stanje = db.PoeniStavke.Where(s => s.UserId == u.Id).Sum(s => (int?)s.Iznos) ?? 0,
            })
            .OrderByDescending(k => k.Stanje)
            .ThenBy(k => k.Ime)
            .Take(Najvise)
            .ToListAsync(ct);
    }
}

/* ---------- Stanje i istorija jednog kupca ---------- */

public record GetAdminPoeniKupcaQuery(Guid UserId) : IRequest<AdminPoeniKupcaDto>;

public class GetAdminPoeniKupcaHandler(IAppDbContext db) : IRequestHandler<GetAdminPoeniKupcaQuery, AdminPoeniKupcaDto>
{
    public Task<AdminPoeniKupcaDto> Handle(GetAdminPoeniKupcaQuery q, CancellationToken ct) =>
        AdminPoeniKupca.UcitajAsync(db, q.UserId, ct);
}

internal static class AdminPoeniKupca
{
    public static async Task<AdminPoeniKupcaDto> UcitajAsync(IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var kupac = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Customer, ct)
            ?? throw new NotFoundException("Kupac nije pronađen.");

        // Cijela istorija, ne zadnjih 50 kao kupcu: admin po njoj traži grešku koju ispravlja.
        var stavke = await db.PoeniStavke.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Kreirana)
            .ToListAsync(ct);

        var orderIds = stavke.Where(s => s.OrderId != null).Select(s => s.OrderId!.Value).Distinct().ToList();
        var kodovi = await db.Orders.Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Code }).ToDictionaryAsync(o => o.Id, o => o.Code, ct);

        var osobe = stavke.SelectMany(s => new[] { s.PreporuceniId, s.UnioId })
            .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        var imena = await db.Users.Where(u => osobe.Contains(u.Id))
            .Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return new AdminPoeniKupcaDto
        {
            UserId = kupac.Id,
            Ime = kupac.Name,
            Email = kupac.Email,
            Stanje = stavke.Sum(s => s.Iznos),
            Istorija = stavke.Select(s =>
            {
                var code = s.OrderId is { } id && kodovi.TryGetValue(id, out var c) ? c : null;
                // Admin vidi puno ime preporučenog; kupcu se prikazuje skraćeno (ImeSkraceno).
                var ime = s.PreporuceniId is { } pid && imena.TryGetValue(pid, out var n) ? n : "Kupac";
                return new AdminPoeniStavkaDto
                {
                    Iznos = s.Iznos,
                    Vrsta = s.Vrsta.ToString(),
                    Opis = PoeniOpis.Za(s.Vrsta, code, ime, s.Razlog),
                    OrderCode = code,
                    Unio = s.UnioId is { } uid && imena.TryGetValue(uid, out var a) ? a : null,
                    Kreirana = s.Kreirana,
                };
            }).ToList(),
        };
    }
}

/* ---------- Ručna korekcija ---------- */

public record KorigujPoeneCommand(Guid UserId, int Iznos, string Razlog) : IRequest<AdminPoeniKupcaDto>;

public class KorigujPoeneValidator : AbstractValidator<KorigujPoeneCommand>
{
    public const int NajvecaKorekcija = 100_000;

    public KorigujPoeneValidator()
    {
        RuleFor(x => x.Iznos).NotEqual(0).WithMessage("Iznos korekcije ne može biti nula.")
            .InclusiveBetween(-NajvecaKorekcija, NajvecaKorekcija)
            .WithMessage($"Korekcija može biti najviše {NajvecaKorekcija} poena odjednom.");
        RuleFor(x => x.Razlog).NotEmpty().WithMessage("Upiši razlog korekcije.")
            .MaximumLength(200).WithMessage("Razlog može imati najviše 200 znakova.");
    }
}

/// <summary>
/// Ručna ispravka stanja (B4). Upisuje se kao nova stavka <see cref="VrstaPoena.Korekcija"/> —
/// knjiga se nikad ne mijenja unazad. Stanje ne smije pasti ispod nule; red kupca se zaključava
/// kao pri narudžbi (B1), da istovremena narudžba koja troši poene ne pročita staro stanje.
/// </summary>
public class KorigujPoeneHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<KorigujPoeneCommand, AdminPoeniKupcaDto>
{
    public async Task<AdminPoeniKupcaDto> Handle(KorigujPoeneCommand cmd, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == cmd.UserId && u.Role == UserRole.Customer, ct))
            throw new NotFoundException("Kupac nije pronađen.");

        await using (var tx = ZakljucavanjeNarudzbe.Podrzano(db) ? await db.Database.BeginTransactionAsync(ct) : null)
        {
            await ZakljucavanjeNarudzbe.ZakljucajAsync(db, cmd.UserId, null, ct);

            var stanje = await PoeniPravila.StanjeAsync(db, cmd.UserId, ct);
            if (stanje + cmd.Iznos < 0)
                throw new DomainException(
                    $"Kupac ima {stanje} poena — korekcija od {cmd.Iznos} bi spustila stanje ispod nule.",
                    "poeni_ispod_nule");

            db.PoeniStavke.Add(new PoeniStavka
            {
                UserId = cmd.UserId,
                Iznos = cmd.Iznos,
                Vrsta = VrstaPoena.Korekcija,
                Razlog = cmd.Razlog.Trim(),
                UnioId = current.Id,
            });
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }

        return await AdminPoeniKupca.UcitajAsync(db, cmd.UserId, ct);
    }
}

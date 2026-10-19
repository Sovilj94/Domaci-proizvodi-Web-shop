using System.Security.Cryptography;
using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Poeni;

public record GetMojiPoeniQuery : IRequest<MojiPoeniDto>;

public class GetMojiPoeniHandler(IAppDbContext db, ICurrentUser current, IOptions<PoeniOpcije> opcije)
    : IRequestHandler<GetMojiPoeniQuery, MojiPoeniDto>
{
    // Bez 0/O i 1/I/L da se kod bez greške prepiše s papira ili poruke.
    private const string Abeceda = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int DuzinaKoda = 8;
    private const int Pokusaja = 5;
    private const int MaxIstorija = 50;

    public async Task<MojiPoeniDto> Handle(GetMojiPoeniQuery q, CancellationToken ct)
    {
        if (!current.IsAuthenticated) throw new UnauthorizedException("Niste prijavljeni.");
        var o = opcije.Value;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct)
            ?? throw new UnauthorizedException("Niste prijavljeni.");

        // Upis je uslovan (samo dok je kod još prazan): dva istovremena prvog poziva ne smiju
        // jedan drugom prepisati kod koji je prvi već podijelio. Zato se na kraju vraća ono što
        // je STVARNO upisano, a ne ono što je ovaj zahtjev izvukao.
        var kodKorisnika = user.PreporukaKod;
        if (string.IsNullOrEmpty(kodKorisnika))
        {
            for (var i = 0; i < Pokusaja && string.IsNullOrEmpty(kodKorisnika); i++)
            {
                var kod = string.Create(DuzinaKoda, 0, (span, _) =>
                {
                    for (var k = 0; k < span.Length; k++)
                        span[k] = Abeceda[RandomNumberGenerator.GetInt32(Abeceda.Length)];
                });
                if (await db.Users.AnyAsync(u => u.PreporukaKod == kod, ct)) continue;
                var id = user.Id;
                try
                {
                    await db.Users.Where(u => u.Id == id && u.PreporukaKod == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.PreporukaKod, kod), ct);
                }
                catch (DbUpdateException)
                {
                    continue; // sudar sa tuđim kodom u istom trenutku — novi pokušaj
                }
                kodKorisnika = await db.Users.AsNoTracking().Where(u => u.Id == id)
                    .Select(u => u.PreporukaKod).FirstOrDefaultAsync(ct);
            }
            if (string.IsNullOrEmpty(kodKorisnika))
                throw new DomainException("Kod preporuke trenutno nije moguće napraviti. Pokušaj ponovo.", "kod_preporuke");
        }

        var stanje = await PoeniPravila.StanjeAsync(db, user.Id, ct);
        var brojPreporuka = await db.Users.CountAsync(u => u.PreporucioId == user.Id, ct);

        var stavke = await db.PoeniStavke
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.Kreirana)
            .Take(MaxIstorija)
            .AsNoTracking()
            .ToListAsync(ct);

        var orderIds = stavke.Where(s => s.OrderId != null).Select(s => s.OrderId!.Value).Distinct().ToList();
        var kodovi = await db.Orders.Where(x => orderIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Code }).ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        var preporuceniIds = stavke
            .Where(s => s.Vrsta == VrstaPoena.PreporukaPreporucio && s.PreporuceniId != null)
            .Select(s => s.PreporuceniId!.Value).Distinct().ToList();
        var imena = await db.Users.Where(x => preporuceniIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return new MojiPoeniDto
        {
            Stanje = stanje,
            VrijednostPoenaKM = o.VrijednostPoenaKM,
            VrijednostKM = Math.Max(0, stanje) * o.VrijednostPoenaKM,
            NagradaPreporuka = o.NagradaPreporuka,
            PreporukaKod = kodKorisnika,
            BrojPreporuka = brojPreporuka,
            Istorija = stavke.Select(s =>
            {
                var code = s.OrderId is { } id && kodovi.TryGetValue(id, out var c) ? c : null;
                var ime = s.PreporuceniId is { } pid && imena.TryGetValue(pid, out var n) ? ImeSkraceno.Za(n) : "Kupac";
                return new PoeniStavkaDto
                {
                    Iznos = s.Iznos,
                    Vrsta = s.Vrsta.ToString(),
                    Opis = Opis(s.Vrsta, code, ime),
                    OrderCode = code,
                    Kreirana = s.Kreirana,
                };
            }).ToList(),
        };
    }

    private static string Opis(VrstaPoena vrsta, string? code, string ime) => vrsta switch
    {
        VrstaPoena.Isporuka => $"Isporuka {code}".TrimEnd(),
        VrstaPoena.Iskoristeno => $"Iskorišteno na {code}".TrimEnd(),
        VrstaPoena.Povrat => $"Vraćeno — otkazana {code}".TrimEnd(),
        VrstaPoena.PreporukaPreporucio => $"Preporuka: {ime} je primio prvu isporuku",
        VrstaPoena.PreporukaNovi => "Dobrodošlica — preporuka",
        _ => "Korekcija",
    };
}

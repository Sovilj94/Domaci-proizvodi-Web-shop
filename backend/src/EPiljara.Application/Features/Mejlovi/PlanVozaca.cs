using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Voznje;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Mejlovi;

/// <summary>
/// Od podešenog sata (lokalno) svaki vozač koji sutra ima stanice dobija plan: koliko stanica, koje zone,
/// procjena dnevne ture i goriva. Jednom po vozaču i danu (ključ u redu mejlova); izmjena rute poslije slanja
/// ne šalje novi plan — vozač vidi tačno stanje u aplikaciji.
/// </summary>
public record PosaljiPlanoveVozacaCommand(DateTime? Sada = null) : IRequest<int>;

public class PosaljiPlanoveVozacaHandler(IAppDbContext db, IRouteOptimizer optimizer, IEmailService email)
    : IRequestHandler<PosaljiPlanoveVozacaCommand, int>
{
    public async Task<int> Handle(PosaljiPlanoveVozacaCommand c, CancellationToken ct)
    {
        var p = await ObavjestenjaPravila.PodesavanjaAsync(db, ct);
        if (!(p?.ObavVozacPlan ?? true)) return 0;
        var lokalno = ObavjestenjaPravila.Lokalno(c.Sada ?? DateTime.UtcNow);
        if (lokalno.Hour < (p?.ObavVozacPlanSat ?? 17)) return 0;

        var sutra = DateOnly.FromDateTime(lokalno).AddDays(1);
        var sufiks = $":{sutra:yyyyMMdd}";
        var poslati = (await db.EmailPoruke.AsNoTracking()
                .Where(m => m.Kljuc != null && m.Kljuc.StartsWith("plan:") && m.Kljuc.EndsWith(sufiks))
                .Select(m => m.Kljuc!).ToListAsync(ct)).ToHashSet();

        var rute = await DnevnaVoznjaProcjena.RuteAsync(db, sutra, sutra, null, ct);
        var vozaciIds = rute.Select(r => r.DriverId).Distinct()
            .Where(id => !poslati.Contains($"plan:{id:N}{sufiks}")).ToList();
        if (vozaciIds.Count == 0) return 0;

        var vozaci = await (from d in db.Drivers.AsNoTracking()
                            join u in db.Users.AsNoTracking() on d.UserId equals u.Id
                            where vozaciIds.Contains(d.Id) && u.Email != ""
                            select new { d.Id, d.Name, u.Email }).ToListAsync(ct);
        if (vozaci.Count == 0) return 0;

        var voznje = await VoznjeMapa.UDtoAsync(db,
            await VoznjeMapa.OsvjeziOpsegAsync(db, optimizer, sutra, sutra, null, ct), ct);
        var poslato = 0;
        foreach (var v in vozaci)
        {
            var stanice = DnevnaVoznjaProcjena.NarudzbeDana(rute.Where(r => r.DriverId == v.Id));
            if (stanice.Count == 0) continue;
            var zone = stanice.GroupBy(o => o.DeliveryZone?.Name ?? "Bez zone")
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => $"{g.Key} ({g.Count()})");
            var redovi = new List<string>
            {
                $"{stanice.Count} {Stanica(stanice.Count)}: {string.Join(", ", zone)}.",
            };
            var dto = voznje.FirstOrDefault(x => x.DriverId == v.Id);
            if (dto?.Km is double km)
                redovi.Add($"Dnevna tura ≈ {km.ToString("0.#", CultureInfo.GetCultureInfo("sr-Latn"))} km sa povratkom u magacin"
                           + (dto.ProcjenaGoriva is decimal g ? $", gorivo ≈ {ObavjestenjaPravila.Novac(g)}." : "."));
            var bezLokacije = stanice.Count(o => o.Latitude is null);
            if (bezLokacije > 0) redovi.Add($"{bezLokacije} {Stanica(bezLokacije)} bez lokacije — provjeri adresu prije polaska.");
            redovi.Add("Redoslijed i navigacija su u aplikaciji E piljara Vozač.");

            await email.SendObavjestenjeAsync([v.Email], "plan_vozaca",
                $"Plan za sutra — {ObavjestenjaPravila.Dan((int)sutra.DayOfWeek)} {sutra.Day}.{sutra.Month}.",
                redovi, null, null, $"plan:{v.Id:N}{sufiks}", ct);
            poslato++;
        }
        return poslato;
    }

    private static string Stanica(int n) => n % 10 == 1 && n % 100 != 11 ? "stanica" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "stanice" : "stanica";
}

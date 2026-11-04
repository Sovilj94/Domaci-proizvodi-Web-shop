using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Mejlovi;

public static class EmailPravila
{
    /// <summary>Poslije ovoliko neuspjelih pokušaja mejl prelazi u „Greška" i čeka admina.</summary>
    public const int MaksPokusaja = 10;

    /// <summary>Razmak do sljedećeg pokušaja: 1, 2, 5, 10, 30, pa 60 minuta.</summary>
    public static TimeSpan Razmak(int pokusaja) => TimeSpan.FromMinutes(pokusaja switch
    {
        <= 1 => 1,
        2 => 2,
        3 => 5,
        4 => 10,
        5 => 30,
        _ => 60,
    });
}

/// <summary>
/// Šalje mejlove iz reda kojima je došao red: na čekanju, bez zakazanog ili sa prošlim sljedećim pokušajem.
/// Snima poslije svakog mejla — pad usred serije ne smije poslati već poslato ponovo. Vraća broj poslatih.
/// </summary>
public record PosaljiEmailoveCommand(DateTime? Sada = null, int Serija = 20) : IRequest<int>;

public class PosaljiEmailoveHandler(IAppDbContext db, IEmailTransport transport) : IRequestHandler<PosaljiEmailoveCommand, int>
{
    public async Task<int> Handle(PosaljiEmailoveCommand c, CancellationToken ct)
    {
        var sada = c.Sada ?? DateTime.UtcNow;
        var red = await db.EmailPoruke
            .Where(m => m.Status == StatusEmaila.NaCekanju && (m.SljedeciPokusaj == null || m.SljedeciPokusaj <= sada))
            .OrderBy(m => m.Kreirana)
            .Take(c.Serija)
            .ToListAsync(ct);

        var poslato = 0;
        foreach (var m in red)
        {
            EmailRezultat r;
            try
            {
                r = await transport.PosaljiAsync(new EmailZaSlanje(m.Id, m.Primalac, m.PrimalacIme, m.Naslov, m.Html), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                r = new EmailRezultat(false, Greska: ex.Message);
            }

            m.Pokusaja++;
            m.Transport = transport.Naziv;
            if (r.Uspjeh)
            {
                m.Status = StatusEmaila.Poslato;
                m.Poslata = DateTime.UtcNow;
                m.DobavljacId = r.DobavljacId;
                m.ZadnjaGreska = null;
                m.SljedeciPokusaj = null;
                poslato++;
            }
            else
            {
                m.ZadnjaGreska = Skrati(r.Greska ?? "Nepoznata greška dobavljača.");
                if (r.Trajna || m.Pokusaja >= EmailPravila.MaksPokusaja)
                {
                    m.Status = StatusEmaila.Greska;
                    m.SljedeciPokusaj = null;
                }
                else m.SljedeciPokusaj = sada + EmailPravila.Razmak(m.Pokusaja);
            }
            await db.SaveChangesAsync(ct);
        }
        return poslato;
    }

    private static string Skrati(string s) => s.Length <= 1000 ? s : s[..1000];
}

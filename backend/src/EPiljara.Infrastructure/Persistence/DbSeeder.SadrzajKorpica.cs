using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using Microsoft.Extensions.Options;

namespace EPiljara.Infrastructure.Persistence;

public static partial class DbSeeder
{
    /// <summary>
    /// Demo: objavljen sadržaj korpica za tekuću i narednu sedmicu, da stranica korpice na test
    /// deployu ne stoji na „sadržaj još nije objavljen" dok admin ne klikne Generiši/Objavi.
    /// Ide kroz iste handlere kao admin (generator pa objava), pa važe ista pravila o vrijednosti,
    /// marži i sezoni. Korpice koje za tu sedmicu već imaju sadržaj (i nacrte koje je admin
    /// ostavio) ne dira. Poziva se na svakom startu sa SEED_DATA, pa na Render free servisu koji
    /// spava novu sedmicu popuni prvo buđenje.
    ///
    /// Namjerno nije dio <see cref="SeedAsync"/>: testovi nad seedovanom bazom računaju na
    /// korpice bez sadržaja (npr. admin zadatak „Generiši").
    /// </summary>
    /// <returns>Broj objavljenih sadržaja.</returns>
    public static async Task<int> SeedSadrzajKorpicaAsync(
        AppDbContext db, KorpiceOpcije opcije, DateTime? sada = null, CancellationToken ct = default)
    {
        var o = Options.Create(opcije);
        var tekuca = SedmicaHelper.Tekuca(sada);
        var naredna = SedmicaHelper.Pomjeri(tekuca.Godina, tekuca.Sedmica, 1);

        var generator = new GenerisiSedmicniSadrzajHandler(db, o);
        var objava = new ObjaviSadrzajHandler(db, o);
        var objavljeno = 0;

        foreach (var (godina, sedmica) in new[] { tekuca, naredna })
        {
            var rezultati = await generator.Handle(new GenerisiSedmicniSadrzajCommand(godina, sedmica, null), ct);
            foreach (var r in rezultati)
            {
                try
                {
                    await objava.Handle(new ObjaviSadrzajCommand(r.SadrzajId), ct);
                    objavljeno++;
                }
                catch (DomainException)
                {
                    // Prazan nacrt (bazen nije imao ništa u sezoni) ostaje adminu.
                }
            }
        }
        return objavljeno;
    }
}

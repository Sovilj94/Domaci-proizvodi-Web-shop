using EPiljara.Application.Features.Catalog;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Enums;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Poslovna pravila za testove. U aplikaciji dolaze iz reda Podesavanja u bazi; ovdje su
/// eksplicitne vrijednosti iz kojih testovi računaju svoja očekivanja.
/// </summary>
internal static class TestOpcije
{
    public static KorpiceOpcije Korpice() => new()
    {
        KoeficijentVrijednosti = 1.15m,
        MinimalnaMarza = 0.25m,
        BrojZamjena = 4,
        RokSati = 48,
        DanIsporukeRezerva = DayOfWeek.Friday,
        VrijemeIsporukeRezerva = "16:00",
        MinimalanBrojArtikala = new()
        {
            [VelicinaKorpice.Mala] = 8,
            [VelicinaKorpice.Srednja] = 8,
            [VelicinaKorpice.Velika] = 8,
        },
    };

    public static PretplateOpcije Pretplate(Action<PretplateOpcije>? izmjena = null)
    {
        var o = new PretplateOpcije
        {
            PopustNedjeljna = 0.10m,
            PopustMjesecna = 0.15m,
            PopustTriMjeseca = 0.20m,
            ObavezaMjesecna = 4,
            ObavezaTriMjeseca = 12,
            SatiUnaprijed = 48,
            AlarmSatiPrijeRoka = 24,
            MaksPauzaSedmica = 8,
        };
        izmjena?.Invoke(o);
        return o;
    }

    public static PoeniOpcije Poeni() => new() { PoenaPoKM = 1, VrijednostPoenaKM = 0.05m, NagradaPreporuka = 100 };

    public static ProdavnicaOpcije Prodavnica() => new() { PragPriKraju = 10m };

    public static IOptions<KorpiceOpcije> KorpiceO() => Options.Create(Korpice());
    public static IOptions<ProdavnicaOpcije> ProdavnicaO() => Options.Create(Prodavnica());
}

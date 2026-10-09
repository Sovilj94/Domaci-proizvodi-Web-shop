using System.Globalization;
using EPiljara.Application.Features.Korpice;

namespace EPiljara.Application.Tests;

/// <summary>
/// Datumi su birani i provjereni ručno: 2026-01-01 je četvrtak, pa ISO 2026 ima 53 sedmice.
/// 2026-10-05 je ponedjeljak, 2026-12-31 je četvrtak.
/// </summary>
public class SedmicaZaNarudzbuTests
{
    [Fact]
    public void Prije_roka_vraca_tekucu_sedmicu()
    {
        // Isporuka četvrtkom u 16:00, rok 48h → rok je utorak u 16:00.
        // Ponedjeljak u 10:00 je prije njega.
        var sada = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(ISOWeek.GetYear(sada), g);
        Assert.Equal(ISOWeek.GetWeekOfYear(sada), s);
    }

    [Fact]
    public void Tacno_na_roku_jos_uvijek_vraca_tekucu_sedmicu()
    {
        // Granica je uključiva: ko stigne u sekundi roka, naručuje za ovu sedmicu.
        var sada = new DateTime(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(ISOWeek.GetWeekOfYear(sada), s);
        Assert.Equal(ISOWeek.GetYear(sada), g);
    }

    [Fact]
    public void Poslije_roka_vraca_sljedecu_sedmicu()
    {
        // Srijeda u 10:00 — rok (utorak 16:00) je prošao.
        var sada = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(ISOWeek.GetYear(sada), g);
        Assert.Equal(ISOWeek.GetWeekOfYear(sada) + 1, s);
    }

    [Fact]
    public void Prelaz_godine_daje_prvu_sedmicu_naredne_ISO_godine()
    {
        // 2026-12-31 je četvrtak u ISO sedmici 53; rok je prošao 29.12. u 16:00.
        var sada = new DateTime(2026, 12, 31, 20, 0, 0, DateTimeKind.Utc);
        Assert.Equal(53, ISOWeek.GetWeekOfYear(sada));   // pretpostavka testa, ne tvrdnja o kodu

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(2027, g);
        Assert.Equal(1, s);
    }

    [Fact]
    public void Neparsabilno_vrijeme_ne_rusi_racun()
    {
        // ParsirajVrijeme pada na ponoć; funkcija i dalje mora vratiti validnu sedmicu.
        var sada = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "ne-vrijeme", 48, sada);

        Assert.InRange(s, 1, 53);
        Assert.Equal(2026, g);
    }
}

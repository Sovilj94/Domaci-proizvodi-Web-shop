using EPiljara.Application.Features.Admin.Poslovanje;

namespace EPiljara.Application.Tests;

public class PoslovanjeKalendarTests
{
    [Fact]
    public void Sedmica_na_prelazu_ide_u_mjesec_isporuke()
    {
        // Sedmica 40/2026 počinje u ponedjeljak 28.9, a isporuka je u četvrtak 1.10.
        Assert.Equal((2026, 10), PoslovanjeKalendar.MjesecIsporuke(2026, 40, DayOfWeek.Thursday));
        Assert.Equal((2026, 9), PoslovanjeKalendar.MjesecIsporuke(2026, 40, DayOfWeek.Monday));
    }

    [Fact]
    public void Oktobar_2026_ima_pet_cetvrtaka()
    {
        Assert.Equal(
            [(2026, 40), (2026, 41), (2026, 42), (2026, 43), (2026, 44)],
            PoslovanjeKalendar.SedmiceMjeseca(2026, 10, DayOfWeek.Thursday));
    }

    [Fact]
    public void Januar_uzima_sedmicu_prethodne_iso_godine_i_prethodni_je_decembar()
    {
        // 1.1.2027 je petak i pripada ISO sedmici 53/2026.
        Assert.Equal((2026, 53), PoslovanjeKalendar.SedmiceMjeseca(2027, 1, DayOfWeek.Friday)[0]);
        Assert.Equal((2026, 12), PoslovanjeKalendar.Prethodni(2027, 1));
        Assert.Equal((2026, 10), PoslovanjeKalendar.Prethodni(2026, 11));
    }

    [Fact]
    public void Pocetak_mjeseca_je_lokalna_ponoc()
    {
        // 1.10.2026 00:00 u Sarajevu (ljetno vrijeme, UTC+2) = 30.9 22:00 UTC.
        Assert.Equal(new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc), PoslovanjeKalendar.PocetakUtc(2026, 10));
    }
}

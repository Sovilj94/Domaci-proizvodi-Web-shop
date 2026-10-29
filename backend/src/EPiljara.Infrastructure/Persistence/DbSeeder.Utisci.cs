using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

public static partial class DbSeeder
{
    /// <summary>
    /// Demo: objavljeni utisci, da sekcija „Rekli su o E piljari" (početna i stranica korpica)
    /// nije prazna na test deployu. Upisuje se samo kad u bazi nema nijednog utiska, pa ne dira
    /// ono što je admin unio ili obrisao. Kao i sadržaj korpica, nije dio <see cref="SeedAsync"/>
    /// jer testovi računaju na bazu bez utisaka.
    /// </summary>
    /// <returns>Broj upisanih utisaka.</returns>
    public static async Task<int> SeedUtisciAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Utisci.AnyAsync(ct)) return 0;

        (string Ime, string Mjesto, string Tekst)[] utisci =
        [
            ("Jelena Kovačević", "Borik",
                "Konačno paradajz koji miriše na paradajz. Djeca sad sama traže salatu, a to je kod nas pravo čudo."),
            ("Dragan Petrović", "Starčevica",
                "Subotom čekam korpicu kao nekad pijacu sa ocem. Krompir sa Manjače je nešto sasvim drugo."),
            ("Milica Đurić", "Lauš",
                "Sviđa mi se što uz svaku namirnicu piše od kog domaćina je. Znam šta jedem i kome dajem novac."),
            ("Stefan Jovanović", "Centar",
                "Uzeli smo srednju korpicu za četvoro i taman je za sedmicu. Kad nešto ne volimo, zamijenim u nalogu."),
            ("Tijana Savić", "Obilićevo",
                "Kajmak i suho meso iz Špajza dođu u istoj dostavi. Gosti su pitali odakle nam, preporučila sam vas svima."),
        ];

        var sada = DateTime.UtcNow;
        db.Utisci.AddRange(utisci.Select((u, i) => new Utisak
        {
            Ime = u.Ime,
            Mjesto = u.Mjesto,
            Tekst = u.Tekst,
            Objavljen = true,
            Redoslijed = i + 1,
            Kreiran = sada,
        }));
        await db.SaveChangesAsync(ct);
        return utisci.Length;
    }
}

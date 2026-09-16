using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Polazište i krajnja tačka svake ture. Jedna piljara, jedan magacin, pa je ovo
/// tabela sa jednim redom: <see cref="JediniId"/> je jedini dopušten Id, i to drži
/// CHECK u bazi, a ne samo dogovor.
///
/// Stoji u bazi, a ne u konfiguraciji, da se lokacija mijenja bez rebuild-a i
/// deploya. Red upisuje migracija, ne demo seeder — seeder u produkciji ne radi,
/// pa bi magacin tamo ostao bez vrijednosti.
/// </summary>
public class Magacin : Entity
{
    /// <summary>Id jedinog reda. Isti je u svakoj bazi, pa upit ne mora da traži „prvi".</summary>
    public static readonly Guid JediniId = new("a0000001-0000-4000-8000-000000000001");

    /// <summary>Naziv za ljude, npr. „Magacin Stričići". Ne ulazi u račun rute.</summary>
    public string Naziv { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

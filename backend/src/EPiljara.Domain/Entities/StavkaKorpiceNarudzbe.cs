using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Jedan artikal u korpici JEDNE narudžbe.
///
/// NE miješati sa <see cref="StavkaSadrzaja"/>: ta pripada sedmičnom šablonu i dijele je svi
/// kupci, a ova pripada jednom kupcu i on je smije mijenjati do roka.
///
/// Veza ide na <see cref="OrderItem"/>, ne na Order: jedna narudžba može nositi dvije
/// različite korpice i svaka traži svoj sadržaj.
///
/// Naziv, jedinica i cijena su snapshot, iz istog razloga kao na OrderItem-u — kasnija izmjena
/// artikla ne smije pomjeriti ono što je kupac kupio.
/// </summary>
public class StavkaKorpiceNarudzbe : Entity
{
    /// <summary>OrderItem čiji Product ima Vrsta == Korpica.</summary>
    public Guid OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Unit Unit { get; set; }
    public decimal Kolicina { get; set; }
    public decimal JedinicnaCijena { get; set; }

    /// <summary>
    /// True kad je kupac sam dodao artikal, false kad je stigao iz šablona. Pakerima je to
    /// razlika između „ovo ide u svaku kutiju" i „ovo je nečiji izbor".
    /// </summary>
    public bool DodaoKupac { get; set; }
}

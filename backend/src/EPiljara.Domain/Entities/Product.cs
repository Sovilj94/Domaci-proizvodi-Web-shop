using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

public class Product : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>
    /// Nabavna cijena po jedinici — koliko artikal košta kod domaćina. Null = nije unesena;
    /// generator korpica tada za taj artikal ne može provjeriti maržu i to javlja. Nikad ne
    /// izlazi na javni API (samo admin DTO-i).
    /// </summary>
    public decimal? NabavnaCijena { get; set; }
    public Unit Unit { get; set; } = Unit.Kom;
    public decimal Stock { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }

    /// <summary>Obična roba ili korpica. Korpica ne smije ući u sadržaj druge korpice.</summary>
    public VrstaProizvoda Vrsta { get; set; } = VrstaProizvoda.Obican;

    /// <summary>Postavljeno samo kad je <see cref="Vrsta"/> == Korpica.</summary>
    public TipKorpice? TipKorpice { get; set; }

    /// <summary>Postavljeno samo kad je <see cref="Vrsta"/> == Korpica.</summary>
    public VelicinaKorpice? Velicina { get; set; }

    /// <summary>Smije li obična roba ući u sadržaj korpice.</summary>
    public bool MozeUKorpicu { get; set; }

    /// <summary>0 = osnovni artikal (ide skoro uvijek), veći broj = dopuna.</summary>
    public int PrioritetUKorpici { get; set; }

    /// <summary>
    /// Mjeseci (1–12) u kojima je artikal dostupan. <b>Prazan niz znači cijele godine</b> —
    /// mliječno i suhomesnato nemaju sezonu. U bazi stoji kao CSV string, jer SQLite
    /// (testovi) nema tip niza; vidi AppDbContext.
    /// </summary>
    public int[] Mjeseci { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    /// <summary>Slika bez pozadine (PNG) za obojene pločice korpica; bez nje pločica koristi ImageUrl.</summary>
    public string? IzrezUrl { get; set; }
    /// <summary>Naziv na engleskom (jezik sajta English); prazno = prikazuje se srpski naziv.</summary>
    public string? NazivEn { get; set; }
    /// <summary>Opis na engleskom; prazno = prikazuje se srpski opis.</summary>
    public string? OpisEn { get; set; }
    /// <summary>Boja pločice namirnice ("#RRGGBB"); null = boja iz palete (BojaProizvoda).</summary>
    public string? Boja { get; set; }

    /// <summary>
    /// Sedmica za koju je admin upozoren da sadržaj ove korpice nije objavljen (A2b) — da alarm
    /// ode jednom, iako posao pretplata prolazi svakih 15 minuta.
    /// </summary>
    public int? AlarmSadrzajaGodina { get; set; }
    public int? AlarmSadrzajaSedmica { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid ProducerId { get; set; }
    public Producer? Producer { get; set; }

    /// <summary>Soft delete — removed products stay in the DB for order history integrity.</summary>
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Provjerava invarijantu korpice: kad je <see cref="Vrsta"/> == Korpica, mora imati
    /// <see cref="TipKorpice"/> i <see cref="Velicina"/>, i ne smije sama ući u sadržaj
    /// druge korpice (<see cref="MozeUKorpicu"/> == false). Običnu robu (Vrsta == Obican)
    /// ne dotiče, bez obzira na vrijednosti ostalih polja. Nije ožičeno u SaveChanges ni u
    /// handlere — poziva ga seeder i kasniji validatori.
    /// </summary>
    public void ProvjeriInvarijantuKorpice()
    {
        if (Vrsta != VrstaProizvoda.Korpica) return;

        if (TipKorpice is null)
            throw new DomainException("Korpica mora imati tip korpice.", "korpica_bez_tipa");
        if (Velicina is null)
            throw new DomainException("Korpica mora imati veličinu.", "korpica_bez_velicine");
        if (MozeUKorpicu)
            throw new DomainException(
                "Korpica ne može ući u sadržaj druge korpice.", "korpica_u_korpici");
    }
}

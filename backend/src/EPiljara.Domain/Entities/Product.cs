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
    /// <summary>Trošak ambalaže po jedinici prodaje (kesa, tegla, flaša; za korpicu kutija). Null: nema ili nije unesen.</summary>
    public decimal? AmbalazaCijena { get; set; }
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
    /// Koliko ovog artikla ide u malu / srednju / veliku korpicu (u jedinici artikla). Admin ih
    /// zadaje; generator, admin editor i zamjena ih koriste umjesto računa iz vrijednosti.
    /// Null = nije zadato, količina se računa iz vrijednosti kao ranije.
    /// </summary>
    public decimal? KolicinaMala { get; set; }
    public decimal? KolicinaSrednja { get; set; }
    public decimal? KolicinaVelika { get; set; }

    /// <summary>
    /// Koliko kilograma teži jedan komad (bundeva, karfiol, veza peršuna). Samo za artikle na komad:
    /// bez nje generator ne zna koliko komad doprinosi težini korpice. Null = nije unesena.
    /// </summary>
    public decimal? TezinaKomada { get; set; }

    /// <summary>
    /// Raspon težine sadržaja u kg — samo za korpice. Generator ne pušta sadržaj preko gornje
    /// granice i dopunjava ga do donje. Null = bez te granice (mliječnoj korpici težina ne znači ništa).
    /// </summary>
    public decimal? TezinaOd { get; set; }
    public decimal? TezinaDo { get; set; }

    /// <summary>
    /// Kilograma po jedinici artikla: kg i litar = 1, komad = <see cref="TezinaKomada"/>.
    /// Null kad je artikal na komad, a težina komada nije unesena.
    /// </summary>
    public decimal? KgPoJedinici => Unit == Unit.Kom
        ? (TezinaKomada is > 0 ? TezinaKomada : null)
        : 1m;

    /// <summary>Zadata količina za veličinu korpice, ili null.</summary>
    public decimal? KolicinaUKorpici(VelicinaKorpice? velicina) => velicina switch
    {
        VelicinaKorpice.Mala => KolicinaMala,
        VelicinaKorpice.Srednja => KolicinaSrednja,
        VelicinaKorpice.Velika => KolicinaVelika,
        _ => null,
    } is > 0 and var k ? k : null;

    /// <summary>
    /// Najmanja smislena količina u korpici (u jedinici artikla): ispod nje se namirnica ne stavlja
    /// nigdje — ni generator, ni kupčevo dodavanje/smanjivanje, ni „popuni umjesto mene". Ako ne staje,
    /// izostavlja se. Null = <see cref="NajmanjaKolicinaUKorpici"/> pada na zadatu za malu korpicu.
    /// </summary>
    public decimal? NajmanjeUKorpici { get; set; }

    /// <summary>Korak zaokruživanja količine: 1 za komad, 0,1 za kg i litar.</summary>
    public static decimal KorakZaokruzivanja(Unit jedinica) => jedinica == Unit.Kom ? 1m : 0.1m;

    /// <summary>Podrazumijevana najmanja količina na kg/l kad admin nije unio svoju: pola kile.</summary>
    public const decimal PodrazumijevanoNajmanjeKg = 0.5m;

    /// <summary>
    /// `NajmanjeUKorpici` kad je unesen; inače komad 1, a kg/l pola kile — ili zadata za malu korpicu
    /// kad je manja (bijeli luk 0,3 kg). Ranije je podrazumijevana bila cijela zadata, pa kupac
    /// nije mogao smanjiti ništa (cvekla 1 kg, a najmanje 2 kg). Nikad ispod koraka zaokruživanja.
    /// </summary>
    public decimal NajmanjaKolicinaUKorpici()
    {
        var korak = KorakZaokruzivanja(Unit);
        if (NajmanjeUKorpici is > 0) return Math.Max(korak, NajmanjeUKorpici.Value);
        if (Unit == Unit.Kom) return korak;
        return Math.Max(korak, Math.Min(PodrazumijevanoNajmanjeKg, KolicinaMala is > 0 ? KolicinaMala.Value : PodrazumijevanoNajmanjeKg));
    }

    /// <summary>
    /// Za koliko kupčevo „+"/„−" mijenja količinu: komad 1; kg i litar 0,5 — ali ne više od najmanje
    /// količine (bijeli luk 0,3 kg ide po 0,3).
    /// </summary>
    public decimal KorakUKorpici() =>
        Unit == Unit.Kom ? 1m : Math.Max(0.1m, Math.Min(0.5m, NajmanjaKolicinaUKorpici()));

    /// <summary>
    /// Mjeseci (1–12) u kojima je artikal dostupan. <b>Prazan niz znači cijele godine</b> —
    /// mliječno i suhomesnato nemaju sezonu. U bazi stoji kao CSV string, jer SQLite
    /// (testovi) nema tip niza; vidi AppDbContext.
    /// </summary>
    public int[] Mjeseci { get; set; } = [];

    /// <summary>
    /// Mjeseci u kojima je kultura GLAVNA u sezoni (npr. bundeva u oktobru): generator je stavlja u
    /// početni sadržaj korpice prije svega ostalog. Kupac je i dalje može zamijeniti do roka.
    /// </summary>
    public int[] MjeseciGlavni { get; set; } = [];

    /// <summary>
    /// Opciona kultura (npr. kopriva): nikad ne ulazi u početni sadržaj korpice, nego se nudi kao
    /// zamjena koju kupac sam izabere do roka.
    /// </summary>
    public bool Opciono { get; set; }

    public bool GlavnoU(int mjesec) => !Opciono && MjeseciGlavni.Contains(mjesec);

    /// <summary>
    /// Cijena istog artikla u piljari / na pijaci (po jedinici artikla), koju admin povremeno provjeri.
    /// Služi samo obračunu cijene korpice — da korpica bude povoljnija od kupovine istog u piljari. Nije javna.
    /// </summary>
    public decimal? CijenaPiljara { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    /// <summary>Slika bez pozadine (PNG) za obojene pločice korpica; bez nje pločica koristi ImageUrl.</summary>
    public string? IzrezUrl { get; set; }
    /// <summary>Naziv na engleskom (jezik sajta English); prazno = prikazuje se srpski naziv.</summary>
    public string? NazivEn { get; set; }
    /// <summary>Opis na engleskom; prazno = prikazuje se srpski opis.</summary>
    public string? OpisEn { get; set; }

    /// <summary>
    /// Prodaje se pojedinačno u Špajzu (po uzoru na farmit). Sezonsko povrće i voće se ne kupuje
    /// na kilogram — ide samo u korpice, pa ovdje stoji false; jaja, ajvar, džem, med i mliječno true.
    /// </summary>
    public bool USpajzu { get; set; } = true;
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

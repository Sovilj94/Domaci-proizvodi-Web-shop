using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Predložena zamjena u sedmičnom sadržaju korpice: artikal koji NIJE u kutiji, ali ga kupac
/// može uzeti umjesto artikla koji neće. Drži se u odvojenoj tabeli, ne kao zastavica na
/// <see cref="StavkaSadrzaja"/>, jer sve što sabira sadržaj (cilj budžeta, snimak u narudžbu,
/// pakerska lista) mora zamjene preskočiti — odvojena tabela to čini nemogućim zaboraviti.
///
/// Količina se ne čuva: računa se pri zamjeni, iz vrijednosti artikla koji se izbacuje.
/// </summary>
public class ZamjenaSadrzaja : Entity
{
    public Guid SedmicniSadrzajId { get; set; }
    public SedmicniSadrzaj? SedmicniSadrzaj { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Snimak Product.Price, ponovo upisan pri objavi — isti razlog kao na StavkaSadrzaja.</summary>
    public decimal JedinicnaCijena { get; set; }
}

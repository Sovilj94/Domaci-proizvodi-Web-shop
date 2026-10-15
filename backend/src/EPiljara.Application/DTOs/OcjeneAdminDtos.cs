namespace EPiljara.Application.DTOs;

/// <summary>Admin pregled ocjena isporuke: sažetak nad svim ocjenama + (filtrirane) stavke.</summary>
public record OcjeneAdminDto
{
    public OcjeneSazetakDto Sazetak { get; init; } = new();
    public IReadOnlyList<OcjenaAdminDto> Stavke { get; init; } = [];
}

public record OcjeneSazetakDto
{
    /// <summary>Prosjek na jednu decimalu; 0 kad nema ocjena.</summary>
    public decimal Prosjek { get; init; }
    public int Broj { get; init; }
    /// <summary>Pet brojeva, indeks 0 = jedna zvjezdica.</summary>
    public int[] PoBroju { get; init; } = new int[5];
}

public record OcjenaAdminDto
{
    public Guid Id { get; init; }
    public int Ocjena { get; init; }
    public string Komentar { get; init; } = string.Empty;
    public bool DozvolaZaObjavu { get; init; }
    public DateTime Kreirana { get; init; }
    public Guid OrderId { get; init; }
    public string KodNarudzbe { get; init; } = string.Empty;
    public string Kupac { get; init; } = string.Empty;
    public string Zona { get; init; } = string.Empty;
    /// <summary>Utisak napravljen iz ove ocjene; izostavljen dok ga nema.</summary>
    public Guid? UtisakId { get; init; }
}

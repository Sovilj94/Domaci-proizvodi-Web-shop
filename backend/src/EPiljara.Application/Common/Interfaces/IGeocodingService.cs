namespace EPiljara.Application.Common.Interfaces;

/// <summary>
/// Jedan pogodak geokodiranja: tačka, čitljiv opis adrese, i imena naselja/kvartova
/// koja se koriste za pogađanje dostavne zone.
/// </summary>
public record GeocodeHit(
    double Lat,
    double Lon,
    string DisplayName,
    IReadOnlyList<string> Localities)
{
    /// <summary>Ulica bez kućnog broja, kad je servis zna — za kratak prikaz i polje „Ulica".</summary>
    public string? Ulica { get; init; }

    /// <summary>Kućni broj, kad pogodak pada na zgradu a ne samo na ulicu.</summary>
    public string? Broj { get; init; }

    /// <summary>Grad ili naselje, za drugi red prijedloga.</summary>
    public string? Grad { get; init; }

    /// <summary>
    /// Opština / grad kao teritorija („Grad Banja Luka") — selo ima svoj <see cref="Grad"/>, a
    /// pripada opštini grada dostave. Služi samo provjeri područja dostave.
    /// </summary>
    public string? Opstina { get; init; }
}

/// <summary>
/// Prijedlozi adresa dok kupac kuca (autocomplete). Odvojeno od <see cref="IGeocodingService"/>
/// jer javni Nominatim pravilima korišćenja zabranjuje pretragu „dok se kuca" — prijedloge
/// daje servis koji je za to napravljen (Photon), a geokodiranje ostaje gdje je bilo.
/// </summary>
public interface IAddressSuggester
{
    /// <summary>Do nekoliko prijedloga, najbliži području dostave prvi; prazno kad nema ništa.</summary>
    Task<IReadOnlyList<GeocodeHit>> SuggestAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pretvara adresu u koordinate i obrnuto. Implementacija mora poštovati ograničenja
/// servisa koji koristi (javni Nominatim: jedan zahtjev u sekundi).
///
/// Cijelo geokodiranje ide kroz ovaj interfejs, i samo kroz njega — i pozadinski
/// poslovi i zahtjevi iz pregledača (preko <c>/geocode</c>). To je jedina granica na
/// kojoj adrese kupaca izlaze iz sistema, pa je i jedino mjesto koje treba zamijeniti
/// da prestanu izlaziti.
/// </summary>
public interface IGeocodingService
{
    /// <summary>Vraća pogodak ili null kad adresa nije prepoznata.</summary>
    Task<GeocodeHit?> GeocodeAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>Vraća adresu za datu tačku ili null kad je nema.</summary>
    Task<GeocodeHit?> ReverseAsync(double lat, double lon, CancellationToken cancellationToken = default);
}

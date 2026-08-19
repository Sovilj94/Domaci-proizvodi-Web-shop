namespace EPiljara.Application.Common;

/// <summary>
/// Grubi pravougaonik oko Bosne i Hercegovine. Koordinate stižu od klijenta (web korpa,
/// admin ispravka), pa se provjerava da tačka uopšte pada u zemlju dostave — zamijenjena
/// širina i dužina za Banju Luku inače prođu provjeru opsega i pin završi u Somaliji.
/// Jedino mjesto gdje ove granice žive.
/// </summary>
public static class GeoBounds
{
    public const double MinLatitude = 42.5;
    public const double MaxLatitude = 45.4;
    public const double MinLongitude = 15.7;
    public const double MaxLongitude = 19.7;

    /// <summary>Poruka kad tačka ispadne iz okvira — kaže šta nije u redu, ne ponavlja opseg.</summary>
    public const string OutsideMessage =
        "Odabrana tačka je izvan Bosne i Hercegovine — provjerite lokaciju na mapi.";

    /// <summary>True kad tačka pada unutar okvira Bosne i Hercegovine.</summary>
    public static bool Contains(double latitude, double longitude) =>
        latitude is >= MinLatitude and <= MaxLatitude
        && longitude is >= MinLongitude and <= MaxLongitude;
}

namespace EPiljara.Application.Tests;

/// <summary>
/// Naselje za testove narudžbi: kvadrat oko Banje Luke, i tačka u njemu. Od uvođenja naselja server
/// narudžbu prima samo sa tačkom koja pada u aktivno naselje.
/// </summary>
internal static class TestNaselje
{
    public const double Lat = 44.77;
    public const double Lon = 17.19;

    /// <summary>Tačka u BiH (prolazi GeoBounds), ali van <see cref="Granica"/>.</summary>
    public const double VanLat = 44.0;
    public const double VanLon = 18.5;

    public static string Granica => GranicaNaseljaTests.Kvadrat(17.0, 44.6, 17.4, 44.9);

    /// <summary>Granica daleko od <see cref="Lat"/>/<see cref="Lon"/> (Bijeljina).</summary>
    public static string GranicaDaleko => GranicaNaseljaTests.Kvadrat(19.1, 44.7, 19.3, 44.8);
}

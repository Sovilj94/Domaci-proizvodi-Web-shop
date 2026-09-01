namespace EPiljara.API.Controllers;

/// <summary>Small request bodies for endpoints where part of the command comes from the route.</summary>
public record UpdateStatusRequest(string Status);

public record SetAvailabilityRequest(bool IsAvailable);

/// <summary>
/// Nullable namjerno: <c>SuppressModelStateInvalidFilter</c> je uključen, pa nepotpuno tijelo
/// (<c>{}</c>) ne bi bilo odbijeno nego bi se vezalo na 0,0 — stvarnu tačku u Atlantiku.
/// Handler odbija null.
/// </summary>
public record SetCoordinatesRequest(double? Latitude, double? Longitude);

/// <summary>
/// Ne-nullable, za razliku od <see cref="SetCoordinatesRequest"/>: ovdje je to bezbjedno jer
/// nepotpuno tijelo (<c>{}</c>) veže na 0,0, a <c>GeoBounds</c> tu tačku odbija kao izvan BiH —
/// nema tihog upisa pogrešne pozicije, samo 400.
/// </summary>
public record ReoptimizeRequest(double Latitude, double Longitude);

/// <summary>
/// Tijelo za dodjelu vozača narudžbi. <c>Date</c> je ISO "yyyy-MM-dd" — dan rute, ne dan
/// kreiranja narudžbe. Prazan ili neispravan datum handler odbija sa <c>invalid_date</c>;
/// prazan <c>DriverId</c> (Guid.Empty pri nepotpunom tijelu) padne na „Vozač nije pronađen".
/// </summary>
public record AssignDriverRequest(Guid DriverId, string Date);

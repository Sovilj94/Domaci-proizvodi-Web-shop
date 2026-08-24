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

public record ReoptimizeRequest(double Latitude, double Longitude);

using EPiljara.Application.Features.Geocoding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

/// <summary>
/// Jedini put kojim geokodiranje izlazi iz aplikacije. Picker adrese u pregledaču zove
/// ovo, a ne Nominatim direktno — inače bi adresa svakog kupca išla trećoj strani sa
/// IP-a tog kupca, i to na svako kucanje.
///
/// Bez prijave, jer checkout rade i gosti; zaštita je particija „geocode".
/// </summary>
[EnableRateLimiting("geocode")]
public class GeocodingController : ApiControllerBase
{
    /// <summary>Adresa → tačka. <c>data: null</c> kad nema pogotka.</summary>
    [HttpGet("geocode")]
    public async Task<IActionResult> Search([FromQuery] string q) =>
        Envelope(await Mediator.Send(new GeocodeQuery(q ?? string.Empty)));

    /// <summary>
    /// Prijedlozi adresa dok kupac kuca (do pet). Ima svoju, per-IP particiju: zove se
    /// mnogo češće od pretrage, pa bi zajednički prozor od 30/min gostima međusobno gasio polje.
    /// </summary>
    [HttpGet("geocode/suggest")]
    [EnableRateLimiting("geocode-suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string q) =>
        Envelope(await Mediator.Send(new SuggestAddressQuery(q ?? string.Empty)));

    /// <summary>Tačka → adresa. <c>data: null</c> kad nema pogotka.</summary>
    [HttpGet("geocode/reverse")]
    public async Task<IActionResult> Reverse([FromQuery] double lat, [FromQuery] double lon) =>
        Envelope(await Mediator.Send(new ReverseGeocodeQuery(lat, lon)));
}

using EPiljara.Application.Features.Driver;
using EPiljara.Application.Features.Voznje;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

/// <summary>Endpoints consumed by the React Native driver mobile app (Phase 2).</summary>
[Authorize(Roles = "Driver")]
public class DriverController : ApiControllerBase
{
    [HttpGet("driver/route")]
    public async Task<IActionResult> MyRoute([FromQuery] string? date) =>
        Envelope(await Mediator.Send(new GetDriverRouteQuery(date)));

    [HttpGet("driver/routes")]
    public async Task<IActionResult> MyRoutes([FromQuery] string? date) =>
        EnvelopeList(await Mediator.Send(new GetDriverRoutesQuery(date)));

    [HttpPatch("driver/orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateStop(Guid id, [FromBody] UpdateStatusRequest body) =>
        Envelope(await Mediator.Send(new UpdateStopStatusCommand(id, body.Status)));

    [HttpPost("driver/route/reoptimize")]
    public async Task<IActionResult> Reoptimize([FromBody] ReoptimizeRequest body) =>
        Envelope(await Mediator.Send(new ReoptimizeRouteCommand(body.Latitude, body.Longitude)));

    /// <summary>Sljedeća ruta od pozicije vozača; data = null kad ruta više nema.</summary>
    [HttpPost("driver/route/sljedeca")]
    public async Task<IActionResult> SljedecaRuta([FromBody] ReoptimizeRequest body) =>
        Envelope(await Mediator.Send(new SljedecaRutaCommand(body.Latitude, body.Longitude)));

    [HttpGet("driver/routes/history")]
    public async Task<IActionResult> RouteHistory() =>
        Envelope(await Mediator.Send(new GetDriverRouteHistoryQuery()));

    /// <summary>Dnevna tura (km sa povratkom u magacin), procjena goriva i moj unos sa pumpe.</summary>
    [HttpGet("driver/voznja")]
    public async Task<IActionResult> MojaVoznja([FromQuery] DateOnly? datum) =>
        Envelope(await Mediator.Send(new GetMojaVoznjaQuery(datum)));

    /// <summary>Unos ili ispravka plaćenog goriva za dan; iznos null briše unos.</summary>
    [HttpPut("driver/voznja/gorivo")]
    public async Task<IActionResult> MojeGorivo([FromBody] SacuvajMojeGorivoCommand body) =>
        Envelope(await Mediator.Send(body));

    /// <summary>Stanje sata na polasku i povratku u magacin; null briše.</summary>
    [HttpPut("driver/voznja/kilometraza")]
    public async Task<IActionResult> MojaKilometraza([FromBody] SacuvajMojuKilometrazuCommand body) =>
        Envelope(await Mediator.Send(body));

    /// <summary>Statistika za profil: dostave i km ovog mjeseca, gotovina danas i nepredata.</summary>
    [HttpGet("driver/statistika")]
    public async Task<IActionResult> MojaStatistika() =>
        Envelope(await Mediator.Send(new GetMojaStatistikaQuery()));

    [HttpGet("driver/profile")]
    public async Task<IActionResult> Profile() =>
        Envelope(await Mediator.Send(new GetDriverProfileQuery()));
}

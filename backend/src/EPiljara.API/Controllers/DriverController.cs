using EPiljara.Application.Features.Driver;
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

    [HttpGet("driver/routes/history")]
    public async Task<IActionResult> RouteHistory() =>
        Envelope(await Mediator.Send(new GetDriverRouteHistoryQuery()));

    [HttpGet("driver/profile")]
    public async Task<IActionResult> Profile() =>
        Envelope(await Mediator.Send(new GetDriverProfileQuery()));
}

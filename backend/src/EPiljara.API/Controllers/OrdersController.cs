using EPiljara.Application.Features.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

public class OrdersController : ApiControllerBase
{
    /// <summary>Guest checkout is allowed; a logged-in customer's order is linked to their account.</summary>
    [HttpPost("orders")]
    public async Task<IActionResult> Create([FromBody] CreateOrderCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [Authorize]
    [HttpGet("orders/mine")]
    public async Task<IActionResult> Mine() =>
        EnvelopeList(await Mediator.Send(new GetMyOrdersQuery()));

    [Authorize]
    [HttpGet("orders/addresses")]
    public async Task<IActionResult> Addresses() =>
        EnvelopeList(await Mediator.Send(new GetMyAddressesQuery()));

    [Authorize]
    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> ById(Guid id) =>
        Envelope(await Mediator.Send(new GetOrderByIdQuery(id)));

    /// <summary>Praćenje narudžbe bez naloga. Token je jedina zaštita, pa ide pod stroži limit.</summary>
    [HttpGet("orders/track/{token}")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Track(string token) =>
        Envelope(await Mediator.Send(new TrackOrderQuery(token)));
}

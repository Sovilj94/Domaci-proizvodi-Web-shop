using EPiljara.Application.Features.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

public class OrdersController : ApiControllerBase
{
    /// <summary>
    /// A logged-in customer's order, linked to their account. Guests never reach this action —
    /// they go through <see cref="GuestRequest"/>/<see cref="ConfirmGuest"/> so an order only
    /// exists once the emailed link is clicked. Without this attribute the whole confirmation
    /// mechanism was bypassable with one anonymous POST here.
    /// </summary>
    [Authorize]
    [HttpPost("orders")]
    public async Task<IActionResult> Create([FromBody] CreateOrderCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    /// <summary>Gost šalje narudžbu — narudžba ne nastaje dok ne klikne link iz mejla.</summary>
    [HttpPost("orders/guest-request")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> GuestRequest([FromBody] CreateGuestOrderRequestCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    /// <summary>Gost potvrđuje narudžbu klikom iz mejla. Tek ovdje narudžba nastaje.</summary>
    [HttpPost("orders/confirm")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmGuest([FromBody] ConfirmGuestOrderCommand command) =>
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

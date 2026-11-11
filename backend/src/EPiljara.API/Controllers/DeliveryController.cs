using EPiljara.Application.Features.Delivery;
using EPiljara.Application.Features.Naselja;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class DeliveryController : ApiControllerBase
{
    [HttpGet("delivery/zones")]
    public async Task<IActionResult> GetZones() =>
        EnvelopeList(await Mediator.Send(new GetDeliveryZonesQuery()));

    /// <summary>Naselje u koje pada tačka adrese; van naselja 400 sa kodom van_podrucja_dostave.</summary>
    [HttpGet("delivery/naselje")]
    public async Task<IActionResult> NaseljeZaTacku([FromQuery] double lat, [FromQuery] double lon) =>
        Envelope(await Mediator.Send(new NaseljeZaTackuQuery(lat, lon)));

    /// <summary>Cijena dostave (uz korpicu / bez nje) i minimum narudžbe.</summary>
    [HttpGet("delivery/pravila")]
    public async Task<IActionResult> Pravila() => Envelope(await Mediator.Send(new GetDostavaPravilaQuery()));

    [HttpGet("delivery/slots")]
    public async Task<IActionResult> GetSlots() =>
        EnvelopeList(await Mediator.Send(new GetDeliverySlotsQuery()));
}

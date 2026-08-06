using EPiljara.Application.Features.Delivery;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class DeliveryController : ApiControllerBase
{
    [HttpGet("delivery/zones")]
    public async Task<IActionResult> GetZones() =>
        EnvelopeList(await Mediator.Send(new GetDeliveryZonesQuery()));

    [HttpGet("delivery/slots")]
    public async Task<IActionResult> GetSlots() =>
        EnvelopeList(await Mediator.Send(new GetDeliverySlotsQuery()));
}

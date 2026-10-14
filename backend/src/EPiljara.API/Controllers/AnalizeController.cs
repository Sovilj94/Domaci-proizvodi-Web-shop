using EPiljara.Application.Features.Analize;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class AnalizeController : ApiControllerBase
{
    /// <summary>Objavljene analize, najnovije uzorkovanje prvo, bez prijave; opciono samo jednog proizvođača.</summary>
    [HttpGet("analize")]
    public async Task<IActionResult> Analize([FromQuery] Guid? producer) =>
        EnvelopeList(await Mediator.Send(new GetAnalizeQuery(producer)));
}

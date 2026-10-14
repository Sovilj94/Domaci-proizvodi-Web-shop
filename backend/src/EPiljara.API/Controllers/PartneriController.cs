using EPiljara.Application.Features.Partneri;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

public class PartneriController : ApiControllerBase
{
    /// <summary>Javna prijava partnera, bez prijave. Ograničena politikom "auth"; honeypot <c>web</c> je u handleru.</summary>
    [HttpPost("partneri/prijave")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Prijava([FromBody] PrijavaInput input) =>
        EnvelopeCreated(await Mediator.Send(new PosaljiPrijavuCommand(input)));
}

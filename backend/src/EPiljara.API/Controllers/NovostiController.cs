using EPiljara.Application.Features.Mejlovi;
using EPiljara.Application.Features.Novosti;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

public class NovostiController : ApiControllerBase
{
    /// <summary>Prijava na novosti iz footera, bez naloga. Politika "auth"; honeypot <c>web</c> je u handleru.</summary>
    [HttpPost("novosti/prijava")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Prijava([FromBody] PrijavaNovostiInput input) =>
        EnvelopeCreated(await Mediator.Send(new PrijaviNaNovostiCommand(input)));

    /// <summary>Odjava sa ručnih obavještenja i novosti — link iz mejla (adresa + HMAC potpis), bez prijave.</summary>
    [HttpPost("email/odjava")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Odjava([FromBody] OdjavaSaObavjestenjaCommand command)
    {
        await Mediator.Send(command);
        return Envelope(new { message = "Odjavljeni ste sa obavještenja." });
    }
}

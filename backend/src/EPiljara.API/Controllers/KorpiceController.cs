using EPiljara.Application.Features.Korpice;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

public class KorpiceController : ApiControllerBase
{
    [HttpGet("korpice")]
    public async Task<IActionResult> Korpice([FromQuery] string? tip) =>
        EnvelopeList(await Mediator.Send(new GetKorpiceQuery(tip)));

    [HttpGet("korpice/sedmica")]
    public async Task<IActionResult> Sedmica() =>
        Envelope(await Mediator.Send(new GetTekucaSedmicaQuery()));

    [HttpGet("korpice/sezona")]
    public async Task<IActionResult> Sezona([FromQuery] string? tip) =>
        Envelope(await Mediator.Send(new GetSezonaKorpiceQuery(tip)));

    [HttpGet("korpice/{slug}")]
    public async Task<IActionResult> Korpica(string slug) =>
        Envelope(await Mediator.Send(new GetKorpicaBySlugQuery(slug)));

    [HttpGet("korpice/{slug}/sadrzaj")]
    public async Task<IActionResult> Sadrzaj(string slug) =>
        Envelope(await Mediator.Send(new GetSadrzajKorpiceQuery(slug)));

    /// <summary>
    /// „Javi mi kad stigne voće" — javni upis bez prijave, ograničen politikom "auth"; honeypot
    /// <c>web</c> je u handleru. Uvijek 204, da odgovor ne otkriva da li je adresa već prijavljena.
    /// </summary>
    [HttpPost("korpice/{tip}/javi-mi")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> JaviMi(string tip, [FromBody] PrijavaSezoneInput input)
    {
        await Mediator.Send(new PrijaviNaSezonuCommand(tip, input));
        return NoContent();
    }
}

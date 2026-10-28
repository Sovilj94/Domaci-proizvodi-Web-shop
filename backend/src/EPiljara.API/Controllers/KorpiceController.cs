using EPiljara.Application.Features.Korpice;
using Microsoft.AspNetCore.Mvc;

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
}

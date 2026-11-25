using EPiljara.Application.Features.Nabavka;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

/// <summary>Otkupni listovi po gazdinstvu — dokumenti koje gazdinstvo potpisuje pri predaji robe.</summary>
[Authorize(Roles = "Admin")]
public class NabavkaController : ApiControllerBase
{
    [HttpGet("admin/otkupni-listovi")]
    public async Task<IActionResult> OtkupniListovi([FromQuery] int godina, [FromQuery] int sedmica) =>
        EnvelopeList(await Mediator.Send(new GetOtkupniListoviQuery(godina, sedmica)));

    /// <summary>Pravi otkupne listove koji nedostaju za zaključanu sedmicu (posao to radi sam).</summary>
    [HttpPost("admin/otkupni-listovi")]
    public async Task<IActionResult> NapraviOtkupneListove([FromBody] NapraviOtkupneListoveRequest body) =>
        Envelope(new { napravljeno = await Mediator.Send(new NapraviOtkupneListoveCommand(body.Godina, body.Sedmica)) });

    [HttpPost("admin/otkupni-listovi/{id:guid}/osvjezi")]
    public async Task<IActionResult> Osvjezi(Guid id) =>
        Envelope(await Mediator.Send(new OsvjeziOtkupniListCommand(id)));

    [HttpPut("admin/otkupni-listovi/{id:guid}/primljeno")]
    public async Task<IActionResult> UnesiPrimljeno(Guid id, [FromBody] UnesiPrimljenoInput input) =>
        Envelope(await Mediator.Send(new UnesiPrimljenoCommand(id, input)));
}

public record NapraviOtkupneListoveRequest(int Godina, int Sedmica);

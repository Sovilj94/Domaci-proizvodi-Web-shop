using EPiljara.Application.Features.Recepti;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class ReceptiController : ApiControllerBase
{
    /// <summary>Objavljeni recepti, najnoviji prvi, bez prijave.</summary>
    [HttpGet("recepti")]
    public async Task<IActionResult> Recepti() =>
        EnvelopeList(await Mediator.Send(new GetReceptiQuery()));

    /// <summary>Jedan objavljen recept po slugu; neobjavljen se ponaša kao nepostojeći.</summary>
    [HttpGet("recepti/{slug}")]
    public async Task<IActionResult> Recept(string slug) =>
        Envelope(await Mediator.Send(new GetReceptBySlugQuery(slug)));
}

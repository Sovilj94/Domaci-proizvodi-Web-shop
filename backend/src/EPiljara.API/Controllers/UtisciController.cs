using EPiljara.Application.Features.Utisci;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class UtisciController : ApiControllerBase
{
    /// <summary>Objavljeni utisci za početnu, bez prijave.</summary>
    [HttpGet("utisci")]
    public async Task<IActionResult> Utisci() =>
        EnvelopeList(await Mediator.Send(new GetUtisciQuery()));
}

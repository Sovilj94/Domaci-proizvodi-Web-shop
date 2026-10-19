using EPiljara.Application.Features.Admin;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class SadrzajSajtaController : ApiControllerBase
{
    /// <summary>Kontakt i česta pitanja za footer, /o-nama i početnu. Javno.</summary>
    [HttpGet("sadrzaj-sajta")]
    public async Task<IActionResult> Sadrzaj() => Envelope(await Mediator.Send(new GetSadrzajSajtaQuery()));
}

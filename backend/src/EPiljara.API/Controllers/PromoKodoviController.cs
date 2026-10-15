using EPiljara.Application.Features.PromoKodovi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

public class PromoKodoviController : ApiControllerBase
{
    /// <summary>Checkout pita koliki je popust prije slanja. Javno; limit protiv pogađanja kodova.</summary>
    [HttpPost("promo-kodovi/provjera")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Provjeri([FromBody] ProvjeriPromoKodQuery query) =>
        Envelope(await Mediator.Send(query));
}

using EPiljara.Application.Features.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

/// <summary>Nalog prijavljenog kupca: njegovi podaci i njegov adresar.</summary>
[Authorize]
public class AccountController : ApiControllerBase
{
    [HttpGet("account/addresses")]
    public async Task<IActionResult> Addresses() =>
        EnvelopeList(await Mediator.Send(new GetAccountAddressesQuery()));
}

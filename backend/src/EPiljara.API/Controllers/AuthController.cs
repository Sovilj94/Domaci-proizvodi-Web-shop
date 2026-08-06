using EPiljara.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EPiljara.API.Controllers;

[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command) =>
        Envelope(await Mediator.Send(command));

    [HttpPost("auth/google")]
    public async Task<IActionResult> Google([FromBody] GoogleLoginCommand command) =>
        Envelope(await Mediator.Send(command));

    [HttpPost("auth/facebook")]
    public async Task<IActionResult> Facebook([FromBody] FacebookLoginCommand command) =>
        Envelope(await Mediator.Send(command));

    [HttpPost("auth/confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailCommand command) =>
        Envelope(await Mediator.Send(command));

    [HttpPost("auth/resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationCommand command)
    {
        await Mediator.Send(command);
        return Envelope(new { message = "Ako nalog postoji i nije potvrđen, poslali smo novi link." });
    }

    [Authorize]
    [HttpGet("auth/me")]
    public async Task<IActionResult> Me() =>
        Envelope(await Mediator.Send(new GetMeQuery()));
}

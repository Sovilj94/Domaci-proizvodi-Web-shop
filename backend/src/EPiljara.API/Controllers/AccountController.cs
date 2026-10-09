using EPiljara.Application.Features.Account;
using EPiljara.Application.Features.Korpice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

/// <summary>Nalog prijavljenog kupca: njegovi podaci i njegov adresar.</summary>
[Authorize]
public class AccountController : ApiControllerBase
{
    [HttpPut("account/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] AzurirajProfilCommand command) =>
        Envelope(await Mediator.Send(command));

    [HttpGet("account/korpice")]
    public async Task<IActionResult> MojeKorpice() =>
        EnvelopeList(await Mediator.Send(new GetMojeKorpiceQuery()));

    [HttpGet("account/korpice/{orderItemId:guid}")]
    public async Task<IActionResult> MojaKorpica(Guid orderItemId) =>
        Envelope(await Mediator.Send(new GetMojaKorpicaQuery(orderItemId)));

    [HttpPut("account/korpice/{orderItemId:guid}")]
    public async Task<IActionResult> IzmijeniKorpicu(
        Guid orderItemId, [FromBody] IzmijeniKorpicuBody body) =>
        Envelope(await Mediator.Send(
            new IzmijeniMojuKorpicuCommand(orderItemId, body.ProductIds)));

    [HttpGet("account/addresses")]
    public async Task<IActionResult> Addresses() =>
        EnvelopeList(await Mediator.Send(new GetAccountAddressesQuery()));

    [HttpPost("account/addresses")]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [HttpPut("account/addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressBody body) =>
        Envelope(await Mediator.Send(new UpdateAddressCommand(
            id, body.Label, body.FullAddress, body.Latitude, body.Longitude, body.DeliveryZoneId)));

    [HttpDelete("account/addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        await Mediator.Send(new DeleteAddressCommand(id));
        return NoContent();
    }

    [HttpPost("account/addresses/{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id) =>
        Envelope(await Mediator.Send(new SetDefaultAddressCommand(id)));
}

/// <summary>Tijelo PUT-a bez Id-a — Id dolazi iz putanje, da se ne mogu razići.</summary>
public record UpdateAddressBody(
    string? Label,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    Guid? DeliveryZoneId);

/// <summary>Tijelo bez Id-a — Id dolazi iz putanje. Količine i cijene određuje server.</summary>
public record IzmijeniKorpicuBody(IReadOnlyList<Guid> ProductIds);

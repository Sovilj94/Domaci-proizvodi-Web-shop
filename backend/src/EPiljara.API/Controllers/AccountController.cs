using EPiljara.Application.Features.Account;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Pretplate;
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

    [HttpPut("account/pismo")]
    public async Task<IActionResult> PostaviPismo([FromBody] PostaviPismoCommand command) =>
        Envelope(await Mediator.Send(command));

    [HttpGet("account/poeni")]
    public async Task<IActionResult> MojiPoeni() =>
        Envelope(await Mediator.Send(new GetMojiPoeniQuery()));

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

    /// <summary>Jedan artikal korpice za drugi; količinu novog određuje server.</summary>
    [HttpPost("account/korpice/{orderItemId:guid}/zamijeni")]
    public async Task<IActionResult> ZamijeniUKorpici(
        Guid orderItemId, [FromBody] ZamijeniUKorpiciBody body) =>
        Envelope(await Mediator.Send(
            new ZamijeniArtikalUKorpiciCommand(orderItemId, body.Izbaci, body.Ubaci)));

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

    [HttpGet("account/pretplate")]
    public async Task<IActionResult> Pretplate() =>
        EnvelopeList(await Mediator.Send(new GetMojePretplateQuery()));

    [HttpPost("account/pretplate")]
    public async Task<IActionResult> NapraviPretplatu([FromBody] NapraviPretplatuCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [HttpPut("account/pretplate/{id:guid}")]
    public async Task<IActionResult> IzmijeniPretplatu(
        Guid id, [FromBody] IzmijeniPretplatuBody body) =>
        Envelope(await Mediator.Send(new IzmijeniPretplatuCommand(
            id, body.ProductId, body.DeliveryZoneId, body.DeliverySlotId,
            body.DeliveryAddress, body.Latitude, body.Longitude, body.Napomena)));

    [HttpPost("account/pretplate/{id:guid}/pauza")]
    public async Task<IActionResult> PauzirajPretplatu(Guid id, [FromBody] PauzaBody body) =>
        Envelope(await Mediator.Send(new PauzirajPretplatuCommand(id, body.Sedmica)));

    [HttpPost("account/pretplate/{id:guid}/nastavi")]
    public async Task<IActionResult> NastaviPretplatu(Guid id) =>
        Envelope(await Mediator.Send(new NastaviPretplatuCommand(id)));

    [HttpDelete("account/pretplate/{id:guid}")]
    public async Task<IActionResult> OtkaziPretplatu(Guid id)
    {
        await Mediator.Send(new OtkaziPretplatuCommand(id));
        return NoContent();
    }
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
public record ZamijeniUKorpiciBody(Guid Izbaci, Guid Ubaci);

/// <summary>Tijelo PUT-a bez Id-a — Id dolazi iz putanje, da se ne mogu razići.</summary>
public record IzmijeniPretplatuBody(
    Guid ProductId,
    Guid DeliveryZoneId,
    Guid DeliverySlotId,
    string DeliveryAddress,
    double? Latitude,
    double? Longitude,
    string? Napomena);

/// <summary>Koliko SEDMICA (isporuka) se preskače, ne datum — vidi odluku O4 u specu.</summary>
public record PauzaBody(int Sedmica);

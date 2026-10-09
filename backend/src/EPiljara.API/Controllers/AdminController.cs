using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Korpice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : ApiControllerBase
{
    [HttpGet("admin/dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? date) =>
        Envelope(await Mediator.Send(new GetDashboardQuery(date)));

    /// <param name="unassigned">Kad je true, <paramref name="date"/> je dan DOSTAVE i vraćaju
    /// se samo narudžbe koje tog dana nisu ni na čijoj ruti.</param>
    [HttpGet("admin/orders")]
    public async Task<IActionResult> Orders(
        [FromQuery] string? status, [FromQuery] DateTime? date, [FromQuery] bool unassigned = false) =>
        EnvelopeList(await Mediator.Send(new GetAdminOrdersQuery(status, date, unassigned)));

    [HttpPatch("admin/orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest body) =>
        Envelope(await Mediator.Send(new UpdateOrderStatusCommand(id, body.Status)));

    [HttpGet("admin/products")]
    public async Task<IActionResult> Products() =>
        EnvelopeList(await Mediator.Send(new GetAdminProductsQuery()));

    [HttpPost("admin/products")]
    public async Task<IActionResult> CreateProduct([FromBody] ProductInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateProductCommand(input)));

    [HttpPut("admin/products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductInput input) =>
        Envelope(await Mediator.Send(new UpdateProductCommand(id, input)));

    [HttpPatch("admin/products/{id:guid}/availability")]
    public async Task<IActionResult> ToggleProduct(Guid id, [FromBody] SetAvailabilityRequest body) =>
        Envelope(await Mediator.Send(new SetProductAvailabilityCommand(id, body.IsAvailable)));

    [HttpDelete("admin/products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id) =>
        Envelope(await Mediator.Send(new DeleteProductCommand(id)));

    [HttpPost("admin/categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateCategoryCommand(input)));

    [HttpPut("admin/categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryInput input) =>
        Envelope(await Mediator.Send(new UpdateCategoryCommand(id, input)));

    [HttpDelete("admin/categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id) =>
        Envelope(await Mediator.Send(new DeleteCategoryCommand(id)));

    [HttpPost("admin/producers")]
    public async Task<IActionResult> CreateProducer([FromBody] ProducerInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateProducerCommand(input)));

    [HttpPut("admin/producers/{id:guid}")]
    public async Task<IActionResult> UpdateProducer(Guid id, [FromBody] ProducerInput input) =>
        Envelope(await Mediator.Send(new UpdateProducerCommand(id, input)));

    [HttpDelete("admin/producers/{id:guid}")]
    public async Task<IActionResult> DeleteProducer(Guid id) =>
        Envelope(await Mediator.Send(new DeleteProducerCommand(id)));

    [HttpGet("admin/drivers")]
    public async Task<IActionResult> Drivers() =>
        EnvelopeList(await Mediator.Send(new GetDriversQuery()));

    [HttpPost("admin/drivers")]
    public async Task<IActionResult> CreateDriver([FromBody] DriverInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateDriverCommand(input)));

    [HttpPut("admin/drivers/{id:guid}")]
    public async Task<IActionResult> UpdateDriver(Guid id, [FromBody] DriverInput input) =>
        Envelope(await Mediator.Send(new UpdateDriverCommand(id, input)));

    [HttpDelete("admin/drivers/{id:guid}")]
    public async Task<IActionResult> DeleteDriver(Guid id) =>
        Envelope(await Mediator.Send(new DeleteDriverCommand(id)));

    [HttpGet("admin/routes")]
    public async Task<IActionResult> Routes([FromQuery] string date) =>
        EnvelopeList(await Mediator.Send(new GetRoutesQuery(date)));

    [HttpPost("admin/routes")]
    public async Task<IActionResult> AssignRoute([FromBody] AssignRouteCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [HttpPost("admin/routes/{id:guid}/optimize")]
    public async Task<IActionResult> OptimizeRoute(Guid id) =>
        Envelope(await Mediator.Send(new OptimizeRouteCommand(id)));

    /* ---------- Magacin: polazište i krajnja tačka svake ture ---------- */

    [HttpGet("admin/warehouse")]
    public async Task<IActionResult> Warehouse() =>
        Envelope(await Mediator.Send(new GetWarehouseQuery()));

    [HttpPut("admin/warehouse")]
    public async Task<IActionResult> UpdateWarehouse([FromBody] UpdateWarehouseCommand command) =>
        Envelope(await Mediator.Send(command));

    /// <summary>Dodjeljuje ili prebacuje narudžbu vozaču za dati dan (dispečerski ekran).</summary>
    [HttpPost("admin/orders/{id:guid}/driver")]
    public async Task<IActionResult> AssignOrderDriver(Guid id, [FromBody] AssignDriverRequest body) =>
        EnvelopeList(await Mediator.Send(new AssignOrderDriverCommand(id, body.DriverId, body.Date)));

    /// <summary>Dodjeljuje više narudžbi (cijelo naselje) jednom vozaču — sve ili ništa.</summary>
    [HttpPost("admin/orders/assign-bulk")]
    public async Task<IActionResult> AssignOrdersBulk([FromBody] AssignOrdersBulkRequest body) =>
        EnvelopeList(await Mediator.Send(
            new AssignOrdersBulkCommand(body.OrderIds, body.DriverId, body.Date)));

    /// <summary>Skida narudžbu sa rute; vraća je u „Nedodijeljeno".</summary>
    [HttpDelete("admin/orders/{id:guid}/driver")]
    public async Task<IActionResult> RemoveOrderDriver(Guid id, [FromQuery] string date)
    {
        await Mediator.Send(new RemoveOrderDriverCommand(id, date));
        return Envelope(new { message = "Narudžba je skinuta sa rute." });
    }

    [HttpPost("admin/orders/geocode-missing")]
    public async Task<IActionResult> GeocodeMissing() =>
        Envelope(await Mediator.Send(new GeocodeMissingCommand()));

    [HttpPut("admin/orders/{id:guid}/coordinates")]
    public async Task<IActionResult> SetCoordinates(Guid id, [FromBody] SetCoordinatesRequest body)
    {
        await Mediator.Send(new SetOrderCoordinatesCommand(id, body.Latitude, body.Longitude));
        return Envelope(new { message = "Koordinate su sačuvane." });
    }

    /* ---------- Korpice: pregled sedmice, izmjena nacrta, pakerska lista ---------- */

    [HttpGet("admin/korpice/sadrzaj")]
    public async Task<IActionResult> SadrzajPregled([FromQuery] int godina, [FromQuery] int sedmica) =>
        EnvelopeList(await Mediator.Send(new GetSadrzajPregledQuery(godina, sedmica)));

    [HttpPost("admin/korpice/sadrzaj/generisi")]
    public async Task<IActionResult> GenerisiSadrzaj([FromBody] GenerisiSadrzajRequest body) =>
        EnvelopeList(await Mediator.Send(
            new GenerisiSedmicniSadrzajCommand(body.Godina, body.Sedmica, body.KorpicaId)));

    [HttpGet("admin/korpice/sadrzaj/{id:guid}")]
    public async Task<IActionResult> SadrzajZaIzmjenu(Guid id) =>
        Envelope(await Mediator.Send(new GetSadrzajZaIzmjenuQuery(id)));

    [HttpPut("admin/korpice/sadrzaj/{id:guid}")]
    public async Task<IActionResult> IzmijeniNacrt(Guid id, [FromBody] IzmijeniNacrtRequest body) =>
        Envelope(await Mediator.Send(new IzmijeniNacrtCommand(id, body.Stavke)));

    [HttpPost("admin/korpice/sadrzaj/{id:guid}/objavi")]
    public async Task<IActionResult> ObjaviSadrzaj(Guid id) =>
        Envelope(await Mediator.Send(new ObjaviSadrzajCommand(id)));

    [HttpGet("admin/korpice/pakerska-lista")]
    public async Task<IActionResult> PakerskaLista([FromQuery] int godina, [FromQuery] int sedmica) =>
        Envelope(await Mediator.Send(new GetPakerskaListaQuery(godina, sedmica)));
}

using EPiljara.Application.Features.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : ApiControllerBase
{
    [HttpGet("admin/dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? date) =>
        Envelope(await Mediator.Send(new GetDashboardQuery(date)));

    [HttpGet("admin/orders")]
    public async Task<IActionResult> Orders([FromQuery] string? status, [FromQuery] DateTime? date) =>
        EnvelopeList(await Mediator.Send(new GetAdminOrdersQuery(status, date)));

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

    [HttpGet("admin/routes")]
    public async Task<IActionResult> Routes([FromQuery] string date) =>
        EnvelopeList(await Mediator.Send(new GetRoutesQuery(date)));

    [HttpPost("admin/routes")]
    public async Task<IActionResult> AssignRoute([FromBody] AssignRouteCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [HttpPost("admin/routes/{id:guid}/optimize")]
    public async Task<IActionResult> OptimizeRoute(Guid id) =>
        Envelope(await Mediator.Send(new OptimizeRouteCommand(id)));
}

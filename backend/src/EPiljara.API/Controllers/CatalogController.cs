using EPiljara.Application.Features.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class CatalogController : ApiControllerBase
{
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] string? category, [FromQuery] string? search, [FromQuery] bool? featured) =>
        EnvelopeList(await Mediator.Send(new GetProductsQuery(category, search, featured)));

    [HttpGet("products/{slug}")]
    public async Task<IActionResult> GetProduct(string slug) =>
        Envelope(await Mediator.Send(new GetProductBySlugQuery(slug)));

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() =>
        EnvelopeList(await Mediator.Send(new GetCategoriesQuery()));

    [HttpGet("producers")]
    public async Task<IActionResult> GetProducers() =>
        EnvelopeList(await Mediator.Send(new GetProducersQuery()));

    [HttpGet("producers/{id:guid}")]
    public async Task<IActionResult> GetProducer(Guid id) =>
        Envelope(await Mediator.Send(new GetProducerByIdQuery(id)));
}

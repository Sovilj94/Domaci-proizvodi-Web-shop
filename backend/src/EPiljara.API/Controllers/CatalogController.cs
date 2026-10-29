using EPiljara.Application.Features.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

public class CatalogController : ApiControllerBase
{
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? category, [FromQuery] string? search, [FromQuery] bool? featured,
        [FromQuery] Guid? producer) =>
        EnvelopeList(await Mediator.Send(new GetProductsQuery(category, search, featured, producer)));

    [HttpGet("products/{slug}")]
    public async Task<IActionResult> GetProduct(string slug) =>
        Envelope(await Mediator.Send(new GetProductBySlugQuery(slug)));

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() =>
        EnvelopeList(await Mediator.Send(new GetCategoriesQuery()));

    /// <summary>Prevodi kataloga (srpski → engleski) za jezik sajta English; keš 5 minuta.</summary>
    [HttpGet("prevodi")]
    public async Task<IActionResult> GetPrevodi()
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Envelope(await Mediator.Send(new GetPrevodiQuery()));
    }

    [HttpGet("producers")]
    public async Task<IActionResult> GetProducers() =>
        EnvelopeList(await Mediator.Send(new GetProducersQuery()));

    [HttpGet("producers/{id:guid}")]
    public async Task<IActionResult> GetProducer(Guid id) =>
        Envelope(await Mediator.Send(new GetProducerByIdQuery(id)));
}

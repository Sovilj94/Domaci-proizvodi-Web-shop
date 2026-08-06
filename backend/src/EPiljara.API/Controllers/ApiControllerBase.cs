using EPiljara.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>200 with a single item wrapped in the envelope.</summary>
    protected IActionResult Envelope<T>(T data, object? meta = null) =>
        Ok(ApiResponse<T>.Ok(data, meta));

    /// <summary>200 with a list plus meta.total.</summary>
    protected IActionResult EnvelopeList<T>(IReadOnlyList<T> data) =>
        Ok(ApiResponse<IReadOnlyList<T>>.Ok(data, new { total = data.Count }));

    /// <summary>201 with the created item wrapped in the envelope.</summary>
    protected IActionResult EnvelopeCreated<T>(T data) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Ok(data));
}

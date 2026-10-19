using EPiljara.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EPiljara.API.Middleware;

/// <summary>
/// Tijelo zahtjeva koje nedostaje ili nije ispravan JSON. Ugrađeni 400 je isključen
/// (`SuppressModelStateInvalidFilter`, da validaciju i poruke vodi FluentValidation), pa je akcija
/// do sada dobijala `null` i pucala sa 500 čim pročita prvo polje. Ovdje se to hvata prije akcije
/// i vraća 400 u našem omotaču. Validacija sadržaja (prazna polja i sl.) ostaje FluentValidation-u.
/// </summary>
public sealed class NeispravnoTijeloFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var p in context.ActionDescriptor.Parameters)
        {
            if (p.BindingInfo?.BindingSource != BindingSource.Body) continue;
            if (context.ActionArguments.TryGetValue(p.Name, out var vrijednost) && vrijednost is not null) continue;

            context.Result = new BadRequestObjectResult(
                ApiResponse.Error("invalid_body", "Zahtjev nema ispravno tijelo (JSON)."));
            return;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}

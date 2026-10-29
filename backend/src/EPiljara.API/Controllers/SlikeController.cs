using EPiljara.Application.Features.Slike;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EPiljara.API.Controllers;

/// <summary>
/// Slike iz baze. Frontend ih traži na svom domenu (<c>/slike/...</c>), a Next ih prosljeđuje
/// ovamo (rewrite u <c>next.config.ts</c>), pa ostale putanje ostaju relativne.
/// </summary>
public class SlikeController : ApiControllerBase
{
    /// <summary>
    /// Slika po ključu, bez prijave. Ključ se nikad ne mijenja za drugi sadržaj (upload pravi
    /// novi), pa se smije dugo keširati; ETag pokriva uslovne zahtjeve (304).
    /// </summary>
    [HttpGet("slike/{**kljuc}")]
    public async Task<IActionResult> Slika(string kljuc, CancellationToken ct)
    {
        var s = await Mediator.Send(new GetSlikaQuery(kljuc), ct);
        // Samo uspješan odgovor se kešira — 404 ne smije ostati u kešu kad slika stigne.
        Response.Headers.CacheControl = "public, max-age=604800";
        return File(s.Podaci, s.ContentType, s.Kreirana, new EntityTagHeaderValue(s.ETag));
    }

    /// <summary>Upload slike iz admin panela; vraća URL za polje slike (proizvod, kategorija…).</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("admin/slike")]
    [RequestSizeLimit(SlikePravila.MaxBajtova + 64 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile fajl, [FromForm] string folder, CancellationToken ct)
    {
        await using var tok = new MemoryStream();
        await fajl.CopyToAsync(tok, ct);
        return EnvelopeCreated(await Mediator.Send(
            new SacuvajSlikuCommand(folder, fajl.FileName, fajl.ContentType, tok.ToArray()), ct));
    }
}

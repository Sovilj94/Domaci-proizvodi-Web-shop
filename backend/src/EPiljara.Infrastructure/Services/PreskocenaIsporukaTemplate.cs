using System.Globalization;
using System.Net;
using EPiljara.Domain.Entities;

namespace EPiljara.Infrastructure.Services;

/// <summary>A2: mejl pretplatniku kojem ove sedmice nije poslata korpica. Pisan latinicom — ćirilicu daje PismoPoruke.</summary>
public static class PreskocenaIsporukaTemplate
{
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Build(User kupac, string korpica, DateTime isporukaUtc, string linkPretplate, bool uPonudi = true)
    {
        var lokalno = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(isporukaUtc, DateTimeKind.Utc),
            EPiljara.Application.Features.Korpice.SedmicaHelper.Zona);
        var datum = lokalno.ToString("d.M.yyyy.", CultureInfo.InvariantCulture);
        var ime = Enc(kupac.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? kupac.Name);

        return $$"""
            <!doctype html>
            <html><body style="margin:0;background:#FFF1E4;font-family:Arial,sans-serif;color:#394236">
              <div style="max-width:560px;margin:0 auto;padding:24px">
                <div style="background:#ffffff;border-radius:20px;padding:28px">
                  <div style="font:700 20px Arial,sans-serif;color:#228B22">E piljara</div>
                  <h1 style="font:700 22px Arial,sans-serif;margin:18px 0 8px">Ove sedmice nema isporuke</h1>
                  <p style="font:400 15px/1.5 Arial,sans-serif">Zdravo {{ime}},</p>
                  <p style="font:400 15px/1.5 Arial,sans-serif">
                    tvoja <b>{{Enc(korpica)}}</b> za isporuku {{datum}} nije poslata — {{(uPonudi ? "sadržaj korpice te sedmice nije bio spreman na vrijeme" : "ova korpica trenutno nije u ponudi, pa možeš izabrati drugu u Mojim pretplatama")}}.
                    Ništa ne plaćaš, a pretplata se nastavlja normalno sljedeće sedmice.
                  </p>
                  <p style="font:400 15px/1.5 Arial,sans-serif">Izvini zbog ovoga.</p>
                  <a href="{{Enc(linkPretplate)}}" style="display:inline-block;margin-top:12px;background:#FE7E0F;color:#ffffff;text-decoration:none;font:600 15px Arial,sans-serif;padding:12px 24px;border-radius:999px">Moje pretplate</a>
                </div>
              </div>
            </body></html>
            """;
    }
}

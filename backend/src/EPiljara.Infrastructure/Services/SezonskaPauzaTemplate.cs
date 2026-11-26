using System.Net;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Kupcu voćne pretplate: voća nema do sezone, pretplata se nastavlja sama (spec 2026-11-26, odluka 6).
/// Pisan latinicom — ćirilicu daje PismoPoruke. Isti okvir kao <see cref="PreskocenaIsporukaTemplate"/>.
/// </summary>
public static class SezonskaPauzaTemplate
{
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Naslov(int mjesecPovratka) => $"Voće se vraća u {SezonaVoca.Lokativ(mjesecPovratka)} — E piljara";

    public static string Build(User kupac, string korpica, int mjesecPovratka, string linkPretplate)
    {
        var ime = Enc(kupac.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? kupac.Name);
        var u = SezonaVoca.Lokativ(mjesecPovratka);

        return $$"""
            <!doctype html>
            <html><body style="margin:0;background:#FFF1E4;font-family:Arial,sans-serif;color:#394236">
              <div style="max-width:560px;margin:0 auto;padding:24px">
                <div style="background:#ffffff;border-radius:20px;padding:28px">
                  <div style="font:700 20px Arial,sans-serif;color:#228B22">E piljara</div>
                  <h1 style="font:700 22px Arial,sans-serif;margin:18px 0 8px">Voće se vraća u {{u}}</h1>
                  <p style="font:400 15px/1.5 Arial,sans-serif">Zdravo {{ime}},</p>
                  <p style="font:400 15px/1.5 Arial,sans-serif">
                    tvoja <b>{{Enc(korpica)}}</b> pravi pauzu dok voćnjaci ne rode. Pretplata se nastavlja sama u {{u}},
                    ništa ne moraš raditi. Do tada ništa ne plaćaš.
                  </p>
                  <a href="{{Enc(linkPretplate)}}" style="display:inline-block;margin-top:12px;background:#FE7E0F;color:#ffffff;text-decoration:none;font:600 15px Arial,sans-serif;padding:12px 24px;border-radius:999px">Moje pretplate</a>
                </div>
              </div>
            </body></html>
            """;
    }
}

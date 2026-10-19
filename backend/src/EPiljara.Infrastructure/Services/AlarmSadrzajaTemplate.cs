using System.Globalization;
using System.Net;
using System.Text;

namespace EPiljara.Infrastructure.Services;

/// <summary>A2b: mejl adminu — korpice sa pretplatama kojima sadržaj sedmice nije objavljen, a rok se bliži.</summary>
public static class AlarmSadrzajaTemplate
{
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    /// <summary>1 pretplata, 2–4 pretplate, 5+ pretplata (11–14 kao 5+).</summary>
    private static string Pretplata(int n) =>
        n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "pretplate" : "pretplata";

    public static string Build(IReadOnlyList<(string Korpica, int Pretplata)> stavke, DateTime rokUtc, string linkKorpica)
    {
        var lokalno = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(rokUtc, DateTimeKind.Utc),
            EPiljara.Application.Features.Korpice.SedmicaHelper.Zona);
        var rok = lokalno.ToString("d.M.yyyy. 'u' HH:mm", CultureInfo.InvariantCulture);

        var redovi = new StringBuilder();
        foreach (var (korpica, broj) in stavke)
            redovi.Append($"""<li style="margin:4px 0"><b>{Enc(korpica)}</b> — {broj} {Pretplata(broj)}</li>""");

        return $$"""
            <!doctype html>
            <html><body style="margin:0;background:#FFF1E4;font-family:Arial,sans-serif;color:#394236">
              <div style="max-width:560px;margin:0 auto;padding:24px">
                <div style="background:#ffffff;border-radius:20px;padding:28px">
                  <div style="font:700 20px Arial,sans-serif;color:#228B22">E piljara · admin</div>
                  <h1 style="font:700 22px Arial,sans-serif;margin:18px 0 8px">Sadržaj korpica nije objavljen</h1>
                  <p style="font:400 15px/1.5 Arial,sans-serif">
                    Rok naručivanja je <b>{{rok}}</b>. Ako se sadržaj ne objavi do tada, pretplatnicima ovih
                    korpica ove sedmice neće biti napravljena narudžba:
                  </p>
                  <ul style="font:400 15px/1.5 Arial,sans-serif;padding-left:20px">{{redovi}}</ul>
                  <a href="{{Enc(linkKorpica)}}" style="display:inline-block;margin-top:12px;background:#FE7E0F;color:#ffffff;text-decoration:none;font:600 15px Arial,sans-serif;padding:12px 24px;border-radius:999px">Objavi sadržaj</a>
                </div>
              </div>
            </body></html>
            """;
    }
}

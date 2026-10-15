using System.Globalization;
using System.Text;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Mejl kojim gost potvrđuje narudžbu. Za razliku od OrderEmailTemplate, ovaj
/// opisuje narudžbu koja još NE postoji — nastaje tek klikom na link.
/// </summary>
public static class GuestOrderEmailTemplate
{
    private const string Green = "#2c7a4b";
    private const string Ink = "#1f2421";
    private const string Muted = "#7a8580";

    private static string Money(decimal v) =>
        "KM " + v.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Build(GuestOrderRequest request, string link)
    {
        var stavke = new StringBuilder();
        foreach (var it in request.Items)
        {
            var kolicina = it.Quantity.ToString("0.##", CultureInfo.InvariantCulture) + " " + it.Unit.ToApi();
            stavke.Append($$"""
                <tr>
                  <td style="padding:8px 0;font:600 14px Arial,sans-serif;color:{{Ink}};width:72px;vertical-align:top">{{Enc(kolicina)}}</td>
                  <td style="padding:8px 0;font:400 14px Arial,sans-serif;color:{{Ink}}">{{Enc(it.ProductName)}}</td>
                  <td style="padding:8px 0;font:600 14px Arial,sans-serif;color:{{Ink}};text-align:right;white-space:nowrap">{{Money(it.UnitPrice * it.Quantity)}}</td>
                </tr>
                """);
        }

        var popustRed = request.Popust > 0
            ? $$"""
                <tr><td style="font:400 14px Arial,sans-serif;color:{{Muted}};text-align:right;padding-top:12px">
                  Popust{{(string.IsNullOrWhiteSpace(request.PromoKod) ? "" : " (" + Enc(request.PromoKod) + ")")}}: −{{Money(request.Popust)}}
                </td></tr>
                """
            : "";

        return $$"""
            <!doctype html>
            <html lang="bs"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#f6f7f5">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f6f7f5;padding:24px 0">
                <tr><td align="center">
                  <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="background:#fff;border-radius:12px;padding:32px">
                    <tr><td style="font:700 22px Arial,sans-serif;color:{{Ink}};padding-bottom:8px">Potvrdi svoju narudžbu</td></tr>
                    <tr><td style="font:400 15px Arial,sans-serif;color:{{Muted}};padding-bottom:20px">
                      Narudžba <strong>još nije poslana</strong> — nastaje tek kad klikneš dugme ispod.
                      Link vrijedi <strong>15 minuta</strong>.
                    </td></tr>
                    <tr><td style="padding-bottom:20px">
                      <a href="{{Enc(link)}}" style="display:inline-block;background:{{Green}};color:#fff;font:600 15px Arial,sans-serif;padding:12px 24px;border-radius:8px;text-decoration:none">Potvrdi narudžbu</a>
                    </td></tr>
                    <tr><td style="font:400 12px Arial,sans-serif;color:{{Muted}};padding-bottom:20px;word-break:break-all">
                      Ako dugme ne radi, otvori: {{Enc(link)}}
                    </td></tr>
                    <tr><td>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">{{stavke}}</table>
                    </td></tr>
                    {{popustRed}}
                    <tr><td style="font:700 16px Arial,sans-serif;color:{{Ink}};text-align:right;padding-top:12px;border-top:1px solid #eceeeb">
                      Ukupno: {{Money(request.TotalAmount)}}
                    </td></tr>
                    <tr><td style="font:400 12px Arial,sans-serif;color:{{Muted}};padding-top:20px">
                      Ako ovu narudžbu nisi poslao/la ti, ne treba ništa da radiš — bez potvrde ona nikad neće nastati.
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body></html>
            """;
    }
}

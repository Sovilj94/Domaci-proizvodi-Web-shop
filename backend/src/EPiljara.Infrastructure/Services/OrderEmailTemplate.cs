using System.Globalization;
using System.Text;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Builds the customer order-confirmation email as a self-contained HTML document
/// (table layout + inline styles for email-client compatibility). Glovo-style:
/// itemised products, a totals block, payment method and delivery details.
/// </summary>
public static class OrderEmailTemplate
{
    private const string Green = "#2c7a4b";
    private const string Ink = "#1f2421";
    private const string Muted = "#7a8580";
    private const string Line = "#eceeeb";
    private const string Panel = "#f6f7f5";

    private static string Money(decimal v) =>
        "KM " + v.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Qty(OrderItem i) =>
        i.Quantity.ToString("0.##", CultureInfo.InvariantCulture) + " " + i.Unit.ToApi();

    private static string Enc(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Build(Order order)
    {
        var sb = new StringBuilder();

        var itemRows = new StringBuilder();
        foreach (var it in order.Items)
        {
            itemRows.Append($$"""
                <tr>
                  <td style="padding:8px 0;font:600 14px Arial,sans-serif;color:{{Ink}};width:64px;vertical-align:top">{{Enc(Qty(it))}}</td>
                  <td style="padding:8px 0;font:400 14px Arial,sans-serif;color:{{Ink}}">{{Enc(it.ProductName)}}</td>
                  <td style="padding:8px 0;font:600 14px Arial,sans-serif;color:{{Ink}};text-align:right;white-space:nowrap">{{Money(it.LineTotal)}}</td>
                </tr>
                """);
        }

        var noteRow = string.IsNullOrWhiteSpace(order.Note)
            ? ""
            : $"""<p style="margin:6px 0 0;font:400 13px Arial,sans-serif;color:{Muted}">Napomena: {Enc(order.Note)}</p>""";

        sb.Append($$"""
            <!doctype html>
            <html lang="bs"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#eef1ee;padding:24px 12px">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden">
                <tr><td style="background:{{Green}};padding:24px 28px">
                  <div style="font:800 22px Arial,sans-serif;color:#ffffff">E piljara</div>
                  <div style="font:400 13px Arial,sans-serif;color:#dbefe3;margin-top:2px">Domaće sa Manjače, dostavljeno na tvoja vrata</div>
                </td></tr>

                <tr><td style="padding:28px 28px 8px">
                  <div style="font:800 20px Arial,sans-serif;color:{{Ink}}">Hvala na narudžbi!</div>
                  <div style="font:400 14px Arial,sans-serif;color:{{Muted}};margin-top:4px">
                    Narudžba <b style="color:{{Ink}}">{{Enc(order.Code)}}</b> je zaprimljena i uskoro će biti potvrđena.
                  </div>
                </td></tr>

                <tr><td style="padding:16px 28px 0">
                  <div style="font:700 13px Arial,sans-serif;color:{{Muted}};text-transform:uppercase;letter-spacing:.04em;margin-bottom:6px">Proizvodi</div>
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0">{{itemRows}}</table>
                </td></tr>

                <tr><td style="padding:12px 28px 0">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid {{Line}};padding-top:12px">
                    <tr><td style="padding:10px 0 4px;font:400 14px Arial,sans-serif;color:{{Muted}}">Proizvodi</td>
                        <td style="padding:10px 0 4px;font:400 14px Arial,sans-serif;color:{{Ink}};text-align:right">{{Money(order.Subtotal)}}</td></tr>
                    <tr><td style="padding:4px 0;font:400 14px Arial,sans-serif;color:{{Muted}}">Dostava{{(order.DeliveryZone is not null ? " · " + Enc(order.DeliveryZone.Name) : "")}}</td>
                        <td style="padding:4px 0;font:400 14px Arial,sans-serif;color:{{Ink}};text-align:right">{{Money(order.DeliveryFee)}}</td></tr>
                    <tr><td style="padding:12px 0 0;font:800 17px Arial,sans-serif;color:{{Ink}};border-top:1px solid {{Line}}">Ukupno</td>
                        <td style="padding:12px 0 0;font:800 17px Arial,sans-serif;color:{{Ink}};text-align:right;border-top:1px solid {{Line}}">{{Money(order.TotalAmount)}}</td></tr>
                  </table>
                </td></tr>

                <tr><td style="padding:18px 28px 0">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{{Panel}};border-radius:12px">
                    <tr><td style="padding:16px 18px">
                      <div style="font:700 13px Arial,sans-serif;color:{{Muted}};text-transform:uppercase;letter-spacing:.04em">Detalji dostave</div>
                      <p style="margin:10px 0 0;font:600 15px Arial,sans-serif;color:{{Ink}}">{{Enc(order.DeliveryAddress)}}</p>
                      <p style="margin:2px 0 0;font:400 13px Arial,sans-serif;color:{{Muted}}">Termin: {{Enc(order.DeliverySlot?.Label ?? "-")}}</p>
                      <p style="margin:2px 0 0;font:400 13px Arial,sans-serif;color:{{Muted}}">Kupac: {{Enc(order.CustomerName)}}{{(string.IsNullOrWhiteSpace(order.CustomerPhone) ? "" : " · " + Enc(order.CustomerPhone))}}</p>
                      {{noteRow}}
                      <p style="margin:12px 0 0;padding-top:12px;border-top:1px solid {{Line}};font:600 14px Arial,sans-serif;color:{{Green}}">Plaćanje pouzećem pri dostavi</p>
                    </td></tr>
                  </table>
                </td></tr>

                <tr><td style="padding:24px 28px 28px">
                  <div style="border-top:1px solid {{Line}};padding-top:16px;font:400 12px Arial,sans-serif;color:{{Muted}};line-height:1.5">
                    E piljara · Banja Luka · Dostava iz Manjače za tvoju porodicu.<br>
                    Ovo je automatska potvrda narudžbe {{Enc(order.Code)}}. Za izmjene nas kontaktirajte.
                  </div>
                </td></tr>
              </table>
            </body></html>
            """);

        return sb.ToString();
    }
}

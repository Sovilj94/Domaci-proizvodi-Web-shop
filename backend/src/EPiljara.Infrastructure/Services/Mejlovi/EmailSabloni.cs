using System.Globalization;
using System.Net;
using System.Text;

namespace EPiljara.Infrastructure.Services.Mejlovi;

/// <summary>
/// Zajednički okvir novih mejlova (isti izgled kao potvrda email adrese): zelena traka, naslov, sadržaj,
/// dugme i link za kopiranje. Tabele i inline stilovi — mejl klijenti ne čitaju stylesheet.
/// </summary>
public static class EmailOkvir
{
    private const string Green = "#2c7a4b";
    private const string Ink = "#1f2421";
    private const string Muted = "#7a8580";
    private const string Line = "#eceeeb";

    public static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Paragraf(string tekst) =>
        $"""<p style="margin:10px 0 0;font:400 15px Arial,sans-serif;color:{Ink};line-height:1.5">{tekst}</p>""";

    /// <summary>Običan tekst (prazan red = novi pasus, novi red = prelom) u HTML, uz enkodiranje.</summary>
    public static string TekstUHtml(string tekst)
    {
        var sb = new StringBuilder();
        foreach (var pasus in tekst.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            sb.Append(Paragraf(string.Join("<br>", pasus.Split('\n').Select(Enc))));
        return sb.ToString();
    }

    public static string Build(string oznaka, string naslov, string sadrzajHtml, string? dugme = null, string? link = null, string? podnozje = null)
    {
        var dugmeHtml = dugme is null || link is null ? string.Empty : $$"""
                  <div style="margin:22px 0">
                    <a href="{{Enc(link)}}" style="display:inline-block;background:{{Green}};color:#ffffff;text-decoration:none;font:700 15px Arial,sans-serif;padding:12px 22px;border-radius:10px">{{Enc(dugme)}}</a>
                  </div>
                  <p style="margin:0;font:400 12px Arial,sans-serif;color:{{Muted}};line-height:1.5">
                    Ako dugme ne radi, kopiraj ovaj link u pretraživač:<br>
                    <a href="{{Enc(link)}}" style="color:{{Green}};word-break:break-all">{{Enc(link)}}</a>
                  </p>
            """;
        return $$"""
            <!doctype html>
            <html lang="bs"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#eef1ee;padding:24px 12px">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden">
                <tr><td style="background:{{Green}};padding:24px 28px">
                  <div style="font:800 22px Arial,sans-serif;color:#ffffff">{{Enc(oznaka)}}</div>
                </td></tr>
                <tr><td style="padding:28px">
                  <div style="font:800 20px Arial,sans-serif;color:{{Ink}}">{{Enc(naslov)}}</div>
                  {{sadrzajHtml}}
                  {{dugmeHtml}}
                  <p style="margin:18px 0 0;padding-top:16px;border-top:1px solid {{Line}};font:400 12px Arial,sans-serif;color:{{Muted}};line-height:1.5">
                    {{podnozje ?? "E piljara — domaće sa gazdinstava, na vaša vrata."}}
                  </p>
                </td></tr>
              </table>
            </body></html>
            """;
    }

    public static string Novac(decimal iznos) => iznos.ToString("0.00", CultureInfo.GetCultureInfo("sr-Latn")) + " KM";
}

using System.Net;
using EPiljara.Domain.Entities;

namespace EPiljara.Infrastructure.Services;

/// <summary>Registration email-confirmation message (self-contained HTML, brand-styled).</summary>
public static class ConfirmEmailTemplate
{
    private const string Green = "#2c7a4b";
    private const string Ink = "#1f2421";
    private const string Muted = "#7a8580";
    private const string Line = "#eceeeb";

    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Build(User user, string link)
    {
        var safeLink = Enc(link);
        return $$"""
            <!doctype html>
            <html lang="bs"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#eef1ee;padding:24px 12px">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden">
                <tr><td style="background:{{Green}};padding:24px 28px">
                  <div style="font:800 22px Arial,sans-serif;color:#ffffff">E piljara</div>
                </td></tr>
                <tr><td style="padding:28px">
                  <div style="font:800 20px Arial,sans-serif;color:{{Ink}}">Potvrdite email adresu</div>
                  <p style="margin:10px 0 0;font:400 15px Arial,sans-serif;color:{{Ink}};line-height:1.5">
                    Zdravo {{Enc(user.Name)}}, hvala na registraciji na E piljaru. Kliknite dugme ispod da potvrdite
                    svoju email adresu i aktivirate nalog.
                  </p>
                  <div style="margin:22px 0">
                    <a href="{{safeLink}}" style="display:inline-block;background:{{Green}};color:#ffffff;text-decoration:none;font:700 15px Arial,sans-serif;padding:12px 22px;border-radius:10px">Potvrdi email</a>
                  </div>
                  <p style="margin:0;font:400 12px Arial,sans-serif;color:{{Muted}};line-height:1.5">
                    Ako dugme ne radi, kopirajte ovaj link u pretraživač:<br>
                    <a href="{{safeLink}}" style="color:{{Green}};word-break:break-all">{{safeLink}}</a>
                  </p>
                  <p style="margin:18px 0 0;padding-top:16px;border-top:1px solid {{Line}};font:400 12px Arial,sans-serif;color:{{Muted}}">
                    Ako se niste registrovali, slobodno ignorišite ovu poruku.
                  </p>
                </td></tr>
              </table>
            </body></html>
            """;
    }
}

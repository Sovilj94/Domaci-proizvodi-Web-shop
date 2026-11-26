using System.Net;

namespace EPiljara.Infrastructure.Services;

/// <summary>„Javi mi kad stigne voće": voćne korpice su ponovo u ponudi (spec 2026-11-26, odluka 5).</summary>
public static class SezonaKrenulaTemplate
{
    public const string Naslov = "Stiglo je voće — E piljara";

    public static string Build(string linkVoca) => $$"""
        <!doctype html>
        <html><body style="margin:0;background:#FFF1E4;font-family:Arial,sans-serif;color:#394236">
          <div style="max-width:560px;margin:0 auto;padding:24px">
            <div style="background:#ffffff;border-radius:20px;padding:28px">
              <div style="font:700 20px Arial,sans-serif;color:#228B22">E piljara</div>
              <h1 style="font:700 22px Arial,sans-serif;margin:18px 0 8px">Stiglo je voće</h1>
              <p style="font:400 15px/1.5 Arial,sans-serif">
                Zdravo, javljamo ti kako smo obećali: voćne korpice su ponovo u ponudi i možeš ih naručiti za ovu sedmicu.
              </p>
              <a href="{{WebUtility.HtmlEncode(linkVoca)}}" style="display:inline-block;margin-top:12px;background:#FE7E0F;color:#ffffff;text-decoration:none;font:600 15px Arial,sans-serif;padding:12px 24px;border-radius:999px">Izaberi korpicu voća</a>
            </div>
          </div>
        </body></html>
        """;
}

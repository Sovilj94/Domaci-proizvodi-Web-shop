import { jsonOk } from "@/server/mock/http";
import { nominatim } from "@/server/mock/geocode";

/*
 * Mock dvojnik za /geocode. Mock postoji da aplikacija radi bez backenda, pa ovdje
 * poziv ka Nominatimu ide sa Next servera — što znači da u mock režimu adresa i
 * dalje stiže trećoj strani. To je prihvatljivo samo zato što je mock isključivo
 * razvojna staza (vidi CLAUDE.md); prava granica je backend.
 */
export async function GET(req: Request) {
  const q = new URL(req.url).searchParams.get("q")?.trim() ?? "";
  if (q.length < 4) return jsonOk(null);

  return jsonOk(
    await nominatim(
      "search?format=jsonv2&addressdetails=1&limit=1&countrycodes=ba&accept-language=bs&q=" +
        encodeURIComponent(q),
      true,
    ),
  );
}

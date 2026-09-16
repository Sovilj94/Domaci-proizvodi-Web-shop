import { jsonOk } from "@/server/mock/http";
import { nominatim } from "@/server/mock/geocode";

/** Mock dvojnik za /geocode/reverse — vidi napomenu u ../route.ts. */
export async function GET(req: Request) {
  const params = new URL(req.url).searchParams;
  const lat = Number(params.get("lat"));
  const lon = Number(params.get("lon"));
  if (!Number.isFinite(lat) || !Number.isFinite(lon)) return jsonOk(null);

  return jsonOk(
    await nominatim(
      `reverse?format=jsonv2&addressdetails=1&accept-language=bs&lat=${lat}&lon=${lon}`,
      false,
    ),
  );
}

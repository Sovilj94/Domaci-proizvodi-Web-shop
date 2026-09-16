import { db } from "@/server/mock/store";
import { jsonFail, jsonOk, requireAdmin } from "@/server/mock/http";

/*
 * Magacin — polazište i krajnja tačka tura. Ista granica i isti ključevi grešaka
 * kao na pravom backendu (GeoBounds + UpdateWarehouseValidator), da forma u admin
 * panelu radi isto bez obzira na koji od dva ugovora je uperena.
 */

const MIN_LAT = 42.5;
const MAX_LAT = 45.4;
const MIN_LON = 15.7;
const MAX_LON = 19.7;
const IZVAN = "Odabrana tačka je izvan Bosne i Hercegovine — provjerite lokaciju na mapi.";

export function GET(req: Request) {
  const { response } = requireAdmin(req);
  if (response) return response;
  return jsonOk(db.warehouse);
}

export async function PUT(req: Request) {
  const { response } = requireAdmin(req);
  if (response) return response;

  const body = (await req.json()) as Partial<Record<"name" | "latitude" | "longitude", unknown>>;
  const details: Record<string, string[]> = {};

  const name = typeof body.name === "string" ? body.name.trim() : "";
  if (!name) details.name = ["Naziv je obavezan."];

  const latitude = typeof body.latitude === "number" ? body.latitude : NaN;
  const longitude = typeof body.longitude === "number" ? body.longitude : NaN;
  if (!(latitude >= MIN_LAT && latitude <= MAX_LAT)) details.latitude = [IZVAN];
  if (!(longitude >= MIN_LON && longitude <= MAX_LON)) details.longitude = [IZVAN];

  if (Object.keys(details).length > 0) {
    return jsonFail("validation_error", "Podaci nisu ispravni.", 400, details);
  }

  db.warehouse = { name, latitude, longitude };
  return jsonOk(db.warehouse);
}

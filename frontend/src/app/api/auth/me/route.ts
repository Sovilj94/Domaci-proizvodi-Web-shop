import { currentUser, jsonFail, jsonOk } from "@/server/mock/http";

export function GET(req: Request) {
  const user = currentUser(req);
  if (!user) return jsonFail("unauthorized", "Niste prijavljeni.", 401);
  return jsonOk(user);
}

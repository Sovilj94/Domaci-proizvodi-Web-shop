import { jsonOk } from "@/server/mock/http";

/**
 * Mock email confirmation. The mock doesn't track real tokens, so it returns a demo
 * logged-in user. The real API validates the token, marks the account confirmed and
 * issues a JWT.
 */
export async function POST() {
  const user = {
    id: "mock-confirmed-user",
    name: "Potvrđeni korisnik",
    email: "confirmed@example.com",
    role: "customer" as const,
  };
  return jsonOk({ token: user.id, user });
}

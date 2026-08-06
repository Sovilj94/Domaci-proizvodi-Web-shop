import { jsonOk } from "@/server/mock/http";

/**
 * Mock social login. The mock can't verify a real Google token, so it just returns
 * a demo customer. The real API (auth/google) verifies the ID token and issues a JWT.
 */
export async function POST() {
  const user = {
    id: "mock-google-user",
    name: "Google Demo",
    email: "google-demo@example.com",
    role: "customer" as const,
  };
  return jsonOk({ token: user.id, user });
}

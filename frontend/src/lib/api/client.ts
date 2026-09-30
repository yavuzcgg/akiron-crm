import createClient, { type Middleware } from "openapi-fetch";
import type { components, paths } from "./schema";

export type Schemas = components["schemas"];

/** Fired when the session cannot be renewed; the app shell listens and sends the user to sign in. */
export const sessionExpiredEvent = "akiron:session-expired";

const refreshPath = "/api/v1/identity/auth/refresh";

/** Paths where a 401 is an answer, not an expired access token. */
const authPaths = [
  "/api/v1/identity/auth/login",
  "/api/v1/identity/auth/register",
  refreshPath,
  "/api/v1/identity/auth/logout",
  "/api/v1/identity/auth/forgot-password",
  "/api/v1/identity/auth/reset-password",
];

let refreshInFlight: Promise<boolean> | null = null;

/**
 * Many requests can hit 401 at once when the access token expires; they all wait for one refresh
 * instead of each rotating the token (a second rotation with the old token would look like reuse
 * and revoke the session).
 */
function refreshOnce(): Promise<boolean> {
  refreshInFlight ??= fetch(refreshPath, { method: "POST", credentials: "include" })
    .then((response) => response.ok)
    .catch(() => false)
    .finally(() => {
      refreshInFlight = null;
    });

  return refreshInFlight;
}

const pendingRetries = new Map<string, Request>();

const renewSessionOn401: Middleware = {
  onRequest({ request, id }) {
    const path = new URL(request.url).pathname;
    if (!authPaths.includes(path)) {
      // The body stream is consumed by the first attempt; keep a copy for the retry.
      pendingRetries.set(id, request.clone());
    }
  },
  async onResponse({ response, id }) {
    const retry = pendingRetries.get(id);
    pendingRetries.delete(id);

    if (response.status !== 401 || !retry) return response;

    if (await refreshOnce()) return fetch(retry);

    window.dispatchEvent(new Event(sessionExpiredEvent));
    return response;
  },
  onError({ id }) {
    pendingRetries.delete(id);
  },
};

/**
 * Typed client for the Akiron API. Requests go to the same origin; Next.js rewrites `/api/*` to the
 * backend, so the HttpOnly session cookies work without CORS.
 */
export const api = createClient<paths>({
  baseUrl: typeof window === "undefined" ? (process.env.API_URL ?? "http://localhost:5080") : window.location.origin,
  credentials: "include",
});

api.use(renewSessionOn401);

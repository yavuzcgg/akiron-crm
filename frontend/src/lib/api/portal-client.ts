import createClient, { type Middleware } from "openapi-fetch";
import type { paths } from "./schema";

/** Fired when a portal request is refused; the portal shell listens and sends the client to sign in again. */
export const portalSessionExpiredEvent = "akiron:portal-session-expired";

const authPrefix = "/api/v1/portal/auth/";

/**
 * A portal session has no refresh step (ADR-0011): a 401 means the link-based session ended.
 * Kept apart from the staff client so a client's 401 can never trigger a staff token refresh.
 */
const signOutOn401: Middleware = {
  onResponse({ request, response }) {
    if (response.status === 401 && !new URL(request.url).pathname.startsWith(authPrefix)) {
      window.dispatchEvent(new Event(portalSessionExpiredEvent));
    }
    return response;
  },
};

/** Typed client for the client portal's endpoints; same origin, the portal cookie rides along. */
export const portalApi = createClient<paths>({
  baseUrl: typeof window === "undefined" ? (process.env.API_URL ?? "http://localhost:5080") : window.location.origin,
  credentials: "include",
});

portalApi.use(signOutOn401);

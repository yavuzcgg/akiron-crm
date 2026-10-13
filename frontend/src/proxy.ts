import { NextResponse, type NextRequest } from "next/server";

/** Set by the API next to the HttpOnly session cookies. It proves nothing; it only avoids a flash of the app for signed-out visitors. */
const sessionHintCookie = "akiron_session";

const authPages = ["/login", "/register", "/forgot-password"];

/** Reachable signed in or not: invitation and reset links may be opened by anyone holding them. */
const openPages = ["/invite", "/reset-password"];

/** Links sent to clients (quotes now, payments later); never behind a sign-in. */
const publicPrefixes = ["/q/", "/forms/"];

/** Set by the API next to the HttpOnly portal session cookie; like the staff hint, it proves nothing. */
const portalHintCookie = "akiron_portal_session";

/** Portal pages a signed-out client may open: asking for a link, and the link itself. */
const portalDoors = ["/portal/login", "/portal/enter"];

/**
 * Optimistic routing only (Next.js guidance): the API still checks every request, and the app shell
 * sends the user to sign in if the session turns out to be invalid.
 */
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  // The client portal is a different door: its own cookie, its own sign-in page (ADR-0011).
  if (pathname === "/portal" || pathname.startsWith("/portal/")) {
    const signedIn = request.cookies.has(portalHintCookie);
    const onDoor = portalDoors.includes(pathname);
    if (!signedIn && !onDoor) return NextResponse.redirect(new URL("/portal/login", request.url));
    if (signedIn && pathname === "/portal/login") return NextResponse.redirect(new URL("/portal", request.url));
    return NextResponse.next();
  }
  if (openPages.includes(pathname) || publicPrefixes.some((prefix) => pathname.startsWith(prefix))) return NextResponse.next();

  const hasSession = request.cookies.has(sessionHintCookie);
  const onAuthPage = authPages.includes(pathname);

  if (!hasSession && !onAuthPage) {
    const login = new URL("/login", request.url);
    if (pathname !== "/") login.searchParams.set("next", pathname + search);
    return NextResponse.redirect(login);
  }

  if (hasSession && onAuthPage) {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }

  return NextResponse.next();
}

export const config = {
  // Everything except the API, Next.js internals and static files.
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|.*\\.[\\w]+$).*)"],
};

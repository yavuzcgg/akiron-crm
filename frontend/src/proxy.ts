import { NextResponse, type NextRequest } from "next/server";

/** Set by the API next to the HttpOnly session cookies. It proves nothing; it only avoids a flash of the app for signed-out visitors. */
const sessionHintCookie = "akiron_session";

const authPages = ["/login", "/register"];

/**
 * Optimistic routing only (Next.js guidance): the API still checks every request, and the app shell
 * sends the user to sign in if the session turns out to be invalid.
 */
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
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

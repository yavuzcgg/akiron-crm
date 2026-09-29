import type { NextConfig } from "next";

const apiUrl = process.env.API_URL ?? "http://localhost:5080";

const nextConfig: NextConfig = {
  output: "standalone",

  // The browser only ever talks to this origin. Forwarding /api/* keeps the session cookies
  // first-party (SameSite=Lax, path /api) and removes the need for CORS.
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }];
  },
};

export default nextConfig;

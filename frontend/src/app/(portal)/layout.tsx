import type { Metadata } from "next";
import type { ReactNode } from "react";

// A client's private area: nothing here belongs in a search engine.
export const metadata: Metadata = { robots: { index: false, follow: false }, referrer: "no-referrer" };

export default function PortalLayout({ children }: { children: ReactNode }) {
  return children;
}

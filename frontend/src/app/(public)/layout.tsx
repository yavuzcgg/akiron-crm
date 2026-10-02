import type { Metadata } from "next";
import type { ReactNode } from "react";

// Links carry a secret: keep them out of search engines and referrers.
export const metadata: Metadata = { robots: { index: false, follow: false }, referrer: "no-referrer" };

export default function PublicLayout({ children }: { children: ReactNode }) {
  return children;
}

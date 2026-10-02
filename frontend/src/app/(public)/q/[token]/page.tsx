"use client";

import { use } from "react";
import { PublicQuote } from "@/features/sales/public-quote";

/** The quote link a client receives; no account, no app shell. */
export default function PublicQuoteRoute({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);
  return <PublicQuote token={token} />;
}

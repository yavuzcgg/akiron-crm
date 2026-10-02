"use client";

import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { QuoteEditor } from "@/features/sales/quote-editor";

function NewQuote() {
  const params = useSearchParams();
  const partyId = params.get("partyId");
  const partyName = params.get("partyName");
  return <QuoteEditor initialParty={partyId && partyName ? { id: partyId, name: partyName } : undefined} />;
}

export default function NewQuoteRoute() {
  return (
    <Suspense>
      <NewQuote />
    </Suspense>
  );
}

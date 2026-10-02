"use client";

import { Suspense, use } from "react";
import { QuotePage } from "@/features/sales/quote-page";

export default function QuoteRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return (
    <Suspense>
      <QuotePage id={id} />
    </Suspense>
  );
}

"use client";

import { use } from "react";
import { PortalQuote } from "@/features/portal/portal-pages";

export default function PortalQuoteRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PortalQuote id={id} />;
}

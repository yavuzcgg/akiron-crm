"use client";

import { use } from "react";
import { PartyDetail } from "@/features/crm/party-detail";

export default function PartyPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PartyDetail id={id} />;
}

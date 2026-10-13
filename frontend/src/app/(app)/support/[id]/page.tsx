"use client";

import { use } from "react";
import { TicketDetail } from "@/features/support/support";

export default function TicketRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <TicketDetail id={id} />;
}

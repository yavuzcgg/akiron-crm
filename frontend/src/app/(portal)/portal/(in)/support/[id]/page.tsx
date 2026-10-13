"use client";

import { use } from "react";
import { PortalTicket } from "@/features/portal/portal-support";

export default function PortalTicketRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PortalTicket id={id} />;
}

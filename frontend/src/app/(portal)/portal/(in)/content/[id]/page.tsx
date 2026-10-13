"use client";

import { use } from "react";
import { PortalContentPost } from "@/features/portal/portal-content";

export default function PortalContentPostRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PortalContentPost id={id} />;
}

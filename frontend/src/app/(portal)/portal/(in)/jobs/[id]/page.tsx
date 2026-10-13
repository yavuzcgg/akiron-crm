"use client";

import { use } from "react";
import { PortalJob } from "@/features/portal/portal-pages";

export default function PortalJobRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PortalJob id={id} />;
}

"use client";

import type { ReactNode } from "react";
import { PortalShell } from "@/features/portal/portal-shell";

export default function SignedInPortalLayout({ children }: { children: ReactNode }) {
  return <PortalShell>{children}</PortalShell>;
}

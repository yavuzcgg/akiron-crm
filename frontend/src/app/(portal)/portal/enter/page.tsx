"use client";

import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { PortalEnter } from "@/features/portal/portal-sign-in";

function Enter() {
  return <PortalEnter token={useSearchParams().get("token")} />;
}

/** The page the e-mailed sign-in link opens. */
export default function PortalEnterRoute() {
  return (
    <Suspense>
      <Enter />
    </Suspense>
  );
}

import type { Metadata } from "next";
import { Suspense } from "react";
import { AcceptInvitation } from "@/features/identity/accept-invitation";

export const metadata: Metadata = { title: "Davet" };

export default function InvitePage() {
  return (
    // The invitation token is read from the query string, which needs a Suspense boundary.
    <Suspense>
      <AcceptInvitation />
    </Suspense>
  );
}

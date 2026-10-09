"use client";

import { CircleCheck } from "lucide-react";
import { useI18n } from "@/lib/i18n";

/** Where a website visitor lands after sending the agency's web form (unless the agency set its own page). */
export default function FormThanksRoute() {
  const { t } = useI18n();
  return (
    <main className="bg-background flex min-h-dvh items-center justify-center px-4">
      <div className="bg-card grid max-w-md justify-items-center gap-3 rounded-xl border p-8 text-center shadow-xs">
        <span className="bg-success-soft text-success flex size-12 items-center justify-center rounded-full">
          <CircleCheck className="size-6" aria-hidden />
        </span>
        <h1 className="text-xl font-semibold tracking-tight">{t("leads.thanks.title")}</h1>
        <p className="text-muted-foreground">{t("leads.thanks.body")}</p>
      </div>
    </main>
  );
}

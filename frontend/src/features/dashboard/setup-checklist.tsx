"use client";

import { useQuery } from "@tanstack/react-query";
import { Check, PartyPopper } from "lucide-react";
import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import type { Session } from "@/features/identity/session";
import { useTimeline } from "@/features/timeline/timeline-api";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";

interface Step {
  label: TranslationKey;
  done: boolean;
  action?: { label: TranslationKey; href: string };
}

/**
 * Onboarding built from real state, not a fake progress bar: each step reads what the workspace
 * actually contains. Shares query keys with the team page and cards, so nothing is fetched twice.
 */
export function SetupChecklist({ session }: { session: Session }) {
  const { t } = useI18n();
  const canSeeTeam = hasPermission(session.permissions, permissions.identity.membersRead);
  const canSeeFiles = hasPermission(session.permissions, permissions.files.read);
  const canSeeTimeline = hasPermission(session.permissions, permissions.timeline.read);

  const members = useQuery({
    queryKey: ["identity", "members", { page: 1, pageSize: 100 }],
    queryFn: async () => unwrap(await api.GET("/api/v1/identity/members", { params: { query: { page: 1, pageSize: 100 } } })),
    enabled: canSeeTeam,
  });
  const invitations = useQuery({
    queryKey: ["identity", "invitations"],
    queryFn: async () => unwrap(await api.GET("/api/v1/identity/invitations")),
    enabled: canSeeTeam,
  });
  const files = useQuery({
    queryKey: ["files", "workspace", session.tenantId],
    queryFn: async () =>
      unwrap(await api.GET("/api/v1/files", { params: { query: { subjectType: "workspace", subjectId: session.tenantId } } })),
    enabled: canSeeFiles,
  });
  const timeline = useTimeline("workspace", session.tenantId, canSeeTimeline);

  // Only owners and admins set a workspace up; for others there is nothing to check.
  if (!canSeeTeam) return null;

  const invited = (members.data?.totalCount ?? 0) > 1 || (invitations.data?.length ?? 0) > 0;
  const hasFile = (files.data?.length ?? 0) > 0;
  const hasNote = (timeline.data?.pages ?? []).some((page) => page.items.some((item) => item.type === "timeline.note"));

  const steps: Step[] = [
    { label: "dashboard.setup.created", done: true },
    { label: "dashboard.setup.invite", done: invited, action: { label: "dashboard.setup.invite.action", href: "/settings/team" } },
    { label: "dashboard.setup.file", done: hasFile, action: { label: "dashboard.setup.file.action", href: "#files" } },
    { label: "dashboard.setup.note", done: hasNote, action: { label: "dashboard.setup.note.action", href: "#activity" } },
  ];
  const done = steps.filter((step) => step.done).length;
  const loading = members.isPending || invitations.isPending || files.isPending || timeline.isPending;

  if (!loading && done === steps.length) {
    return (
      <div className="border-success/25 bg-success-soft text-success flex items-center gap-3 rounded-xl border px-5 py-4">
        <PartyPopper className="size-5 shrink-0" aria-hidden />
        <p className="font-medium">{t("dashboard.setup.done")}</p>
      </div>
    );
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="grid gap-1">
            <CardTitle>{t("dashboard.setup.title")}</CardTitle>
            <CardDescription>{t("dashboard.setup.description")}</CardDescription>
          </div>
          <span className="text-muted-foreground tabular shrink-0 text-xs font-medium">
            {t("dashboard.setup.progress", { done, total: steps.length })}
          </span>
        </div>
        <div
          className="bg-muted mt-3 h-1.5 overflow-hidden rounded-full"
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={steps.length}
          aria-valuenow={done}
          aria-label={t("dashboard.setup.title")}
        >
          <div className="bg-primary h-full rounded-full transition-[width] duration-500" style={{ width: `${(done / steps.length) * 100}%` }} />
        </div>
      </CardHeader>
      <CardContent>
        <ol className="divide-border divide-y">
          {steps.map((step) => (
            <li key={step.label} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
              <span
                className={cn(
                  "flex size-6 shrink-0 items-center justify-center rounded-full border",
                  step.done ? "border-success bg-success text-white" : "border-input text-transparent",
                )}
                aria-hidden
              >
                <Check className="size-3.5" />
              </span>
              <span className={cn("flex-1", step.done && "text-muted-foreground line-through decoration-1")}>
                {t(step.label)}
                <span className="sr-only">{step.done ? " ✓" : ""}</span>
              </span>
              {!step.done && step.action ? (
                <Link href={step.action.href} className={buttonVariants({ variant: "outline", size: "sm" })}>
                  {t(step.action.label)}
                </Link>
              ) : null}
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}

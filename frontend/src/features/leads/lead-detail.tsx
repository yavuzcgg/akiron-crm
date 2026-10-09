"use client";

import { AlertCircle, ArrowLeft, Building2, FileText, Pencil, RotateCcw, Trash2, Trophy, UserPlus, XCircle } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useSession } from "@/features/identity/session";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatDate, formatMoney } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { askLostReason } from "./lead-board";
import { LeadDialog } from "./lead-dialog";
import { sourceLabels, stageLabels, stageTone } from "./labels";
import { openStages, useLead, useLeadMutations } from "./leads-api";

function Fact({ label, children }: { label: TranslationKey; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="grid gap-1">
      <dt className="text-muted-foreground text-xs font-medium">{t(label)}</dt>
      <dd className="text-sm break-words">{children}</dd>
    </div>
  );
}

/** One lead: who, what, the stage, the next step, the way to a customer and a quote, and its history. */
export function LeadDetail({ id }: { id: string }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const lead = useLead(id);
  const { move, convert, remove } = useLeadMutations();
  const [editing, setEditing] = useState(false);
  const granted = session?.permissions;
  const canWrite = hasPermission(granted, permissions.crm.leadsWrite);
  const canConvert = canWrite && hasPermission(granted, permissions.crm.partiesWrite);
  const canQuote = hasPermission(granted, permissions.sales.quotesWrite);
  const fail = (error: unknown) => toast.error(tError(error instanceof ApiError ? error.code : "common.network"));

  const back = (
    <Link href="/crm/leads" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("leads.title")}
    </Link>
  );

  if (lead.isPending) return <Skeleton className="h-96 w-full" />;
  if (lead.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{tError(lead.error instanceof ApiError ? lead.error.code : "common.network")}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const data = lead.data;
  const open = data.stage !== "won" && data.stage !== "lost";
  const quoteHref = data.partyId ? `/sales/quotes/new?partyId=${data.partyId}&partyName=${encodeURIComponent(data.partyName ?? "")}` : null;

  return (
    <div className="grid gap-6">
      {back}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="grid gap-1.5">
          <div className="flex items-center gap-2">
            <span className={cn("size-2 rounded-full", stageTone[data.stage])} aria-hidden />
            <Badge variant={data.stage === "won" ? "default" : "outline"}>{t(stageLabels[data.stage]!)}</Badge>
            <span className="text-muted-foreground text-xs">{t(sourceLabels[data.source] ?? "leads.source.other")}</span>
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{data.title}</h1>
          {data.partyId ? (
            <Link href={`/crm/parties/${data.partyId}`} className="text-primary flex items-center gap-1.5 text-sm underline-offset-4 hover:underline">
              <Building2 className="size-4" aria-hidden /> {data.partyName}
            </Link>
          ) : (
            <p className="text-muted-foreground text-sm">{[data.companyName, data.contactName].filter(Boolean).join(" · ")}</p>
          )}
        </div>
        {canWrite ? (
          <div className="flex flex-wrap gap-2">
            {open && !data.partyId && canConvert ? (
              <Button
                variant="outline"
                disabled={convert.isPending}
                onClick={() => convert.mutate({ id }, { onSuccess: (result) => toast.success(t("leads.converted", { name: result.lead.partyName ?? "" })), onError: fail })}
              >
                <UserPlus /> {t("leads.action.convert")}
              </Button>
            ) : null}
            {open && quoteHref && canQuote ? (
              <Link href={quoteHref} className={buttonVariants({ variant: "outline" })}>
                <FileText /> {t("sales.quote.new")}
              </Link>
            ) : null}
            {open ? (
              <>
                <Button variant="outline" onClick={() => move.mutate({ id, stage: "won" }, { onSuccess: () => toast.success(t("leads.wonToast", { title: data.title })), onError: fail })}>
                  <Trophy /> {t("leads.action.won")}
                </Button>
                <Button
                  variant="outline"
                  onClick={() => {
                    const reason = askLostReason(t, data.title);
                    if (reason) move.mutate({ id, stage: "lost", lostReason: reason }, { onError: fail });
                  }}
                >
                  <XCircle /> {t("leads.action.lost")}
                </Button>
              </>
            ) : (
              <Button variant="outline" onClick={() => move.mutate({ id, stage: "proposal" }, { onError: fail })}>
                <RotateCcw /> {t("leads.action.reopen")}
              </Button>
            )}
            <Button onClick={() => setEditing(true)}>
              <Pencil /> {t("leads.edit")}
            </Button>
          </div>
        ) : null}
      </div>

      {data.stage === "lost" && data.lostReason ? (
        <Alert>
          <XCircle />
          <AlertDescription>{t("leads.lostBecause", { reason: data.lostReason })}</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="grid content-start gap-6">
          {open && canWrite ? (
            <div role="group" aria-label={t("leads.card.moveTo")} className="bg-muted inline-flex flex-wrap justify-self-start rounded-lg p-0.5">
              {openStages.map((stage) => (
                <button
                  key={stage}
                  type="button"
                  aria-pressed={data.stage === stage}
                  onClick={() => data.stage !== stage && move.mutate({ id, stage }, { onError: fail })}
                  className={cn(
                    "focus-visible:ring-ring/40 h-8 cursor-pointer rounded-md px-3 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                    data.stage === stage ? "bg-card text-foreground font-medium shadow-xs" : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {t(stageLabels[stage]!)}
                </button>
              ))}
            </div>
          ) : null}
          <Card>
            <CardHeader>
              <CardTitle>{t("timeline.title")}</CardTitle>
              <CardDescription>{t("leads.timelineHint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed subjectType="lead" subjectId={id} canWriteNotes={hasPermission(granted, permissions.timeline.notesWrite)} />
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle>{t("leads.details")}</CardTitle>
              <CardDescription>{t("leads.createdOn", { date: formatDate(data.createdAt) })}</CardDescription>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-4">
                {data.nextStepDate || data.nextStep ? (
                  <Fact label="leads.field.nextStep">
                    {data.nextStep ?? "—"}
                    {data.nextStepDate ? <span className="text-muted-foreground"> · {formatCalendarDate(data.nextStepDate)}</span> : null}
                  </Fact>
                ) : null}
                <Fact label="leads.field.value">{data.estimatedValue ? formatMoney(data.estimatedValue, data.currency) : "—"}</Fact>
                <Fact label="leads.field.expectedClose">{data.expectedCloseDate ? formatCalendarDate(data.expectedCloseDate) : "—"}</Fact>
                <Fact label="leads.field.owner">{data.ownerName ?? "—"}</Fact>
                <Fact label="leads.field.company">{data.companyName ?? "—"}</Fact>
                <Fact label="leads.field.contact">{data.contactName ?? "—"}</Fact>
                <Fact label="crm.field.email">{data.email ? <a className="text-primary underline-offset-4 hover:underline" href={`mailto:${data.email}`}>{data.email}</a> : "—"}</Fact>
                <Fact label="crm.field.phone">{data.phone ? <a className="text-primary underline-offset-4 hover:underline" href={`tel:${data.phone}`}>{data.phone}</a> : "—"}</Fact>
                {data.quoteId ? (
                  <Fact label="leads.field.quote">
                    <Link className="text-primary underline-offset-4 hover:underline" href={`/sales/quotes/${data.quoteId}`}>
                      {t("leads.openQuote")}
                    </Link>
                  </Fact>
                ) : null}
                {data.notes ? <Fact label="leads.field.notes"><span className="whitespace-pre-wrap">{data.notes}</span></Fact> : null}
              </dl>
            </CardContent>
          </Card>
          {canWrite ? (
            <Button
              variant="ghost"
              className="text-destructive justify-self-start"
              onClick={() => {
                if (!window.confirm(t("leads.deleteConfirm", { title: data.title }))) return;
                remove.mutate(id, { onSuccess: () => router.push("/crm/leads"), onError: fail });
              }}
            >
              <Trash2 /> {t("leads.delete")}
            </Button>
          ) : null}
        </div>
      </div>

      {editing ? <LeadDialog open onOpenChange={setEditing} lead={data} /> : null}
    </div>
  );
}

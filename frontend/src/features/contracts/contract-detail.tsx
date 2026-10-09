"use client";

import { AlertCircle, ArrowLeft, Building2, CirclePause, CirclePlay, CircleStop, FileText, Pencil } from "lucide-react";
import Link from "next/link";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useSession } from "@/features/identity/session";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatMoney } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { ContractDialog } from "./contract-dialog";
import { ContractStatusBadge } from "./contracts-page";
import { useContract, useContractCharges, useContractMutations } from "./contracts-api";

function Fact({ label, children }: { label: TranslationKey; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="grid gap-1">
      <dt className="text-muted-foreground text-xs font-medium">{t(label)}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

/** One retainer: its terms, what it billed, and what happens next (renewal, end, pause). */
export function ContractDetail({ id }: { id: string }) {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const contract = useContract(id);
  const granted = session?.permissions;
  const canWrite = hasPermission(granted, permissions.sales.contractsWrite);
  const canSeeMoney = hasPermission(granted, permissions.finance.read);
  const charges = useContractCharges(id, canSeeMoney);
  const { pause, resume, end } = useContractMutations();
  const [editing, setEditing] = useState(false);
  const fail = (error: unknown) => toast.error(tError(error instanceof ApiError ? error.code : "common.network"));

  const back = (
    <Link href="/sales/contracts" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("contracts.title")}
    </Link>
  );

  if (contract.isPending) return <Skeleton className="h-96 w-full" />;
  if (contract.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{tError(contract.error instanceof ApiError ? contract.error.code : "common.network")}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const data = contract.data;
  const money = (value: number) => formatMoney(value, data.currency);

  return (
    <div className="grid gap-6">
      {back}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="grid gap-1.5">
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground font-mono text-xs">{data.number}</span>
            <ContractStatusBadge status={data.status} />
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{data.title}</h1>
          <Link href={`/crm/parties/${data.partyId}`} className="text-primary flex items-center gap-1.5 text-sm underline-offset-4 hover:underline">
            <Building2 className="size-4" aria-hidden /> {data.partyName}
          </Link>
        </div>
        {canWrite && data.status !== "ended" ? (
          <div className="flex flex-wrap gap-2">
            {data.status === "active" ? (
              <Button variant="outline" onClick={() => pause.mutate(id, { onSuccess: () => toast.success(t("contracts.paused")), onError: fail })}>
                <CirclePause /> {t("contracts.action.pause")}
              </Button>
            ) : (
              <Button variant="outline" onClick={() => resume.mutate(id, { onSuccess: () => toast.success(t("contracts.resumed")), onError: fail })}>
                <CirclePlay /> {t("contracts.action.resume")}
              </Button>
            )}
            <Button
              variant="outline"
              onClick={() => {
                const reason = window.prompt(t("contracts.endPrompt", { number: data.number }));
                if (reason?.trim()) end.mutate({ id, reason: reason.trim() }, { onSuccess: () => toast.success(t("contracts.ended")), onError: fail });
              }}
            >
              <CircleStop /> {t("contracts.action.end")}
            </Button>
            <Button onClick={() => setEditing(true)}>
              <Pencil /> {t("contracts.edit")}
            </Button>
          </div>
        ) : null}
      </div>

      {data.status === "ended" && data.endReason ? (
        <Alert>
          <CircleStop />
          <AlertDescription>{t("contracts.endedBecause", { date: data.endedOn ? formatCalendarDate(data.endedOn) : "", reason: data.endReason })}</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="grid content-start gap-6">
          {canSeeMoney ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("contracts.billed")}</CardTitle>
                <CardDescription>{t("contracts.billedHint")}</CardDescription>
              </CardHeader>
              <CardContent>
                {charges.isPending ? (
                  <Skeleton className="h-20 w-full" />
                ) : (charges.data ?? []).length === 0 ? (
                  <p className="text-muted-foreground text-sm">{t("contracts.billedEmpty")}</p>
                ) : (
                  <ul className="divide-y rounded-lg border">
                    {charges.data!.map((charge) => {
                      const instalment = charge.instalments[0];
                      const paid = charge.collected >= charge.amount;
                      return (
                        <li key={charge.id} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                          <div className="grid min-w-0">
                            <span className="font-medium tabular-nums">{charge.period ? formatCalendarDate(charge.period) : formatCalendarDate(charge.date)}</span>
                            <span className="text-muted-foreground font-mono text-xs">
                              {charge.number}
                              {instalment ? ` · ${t("contracts.dueOn", { date: formatCalendarDate(instalment.dueDate) })}` : ""}
                            </span>
                          </div>
                          <div className="text-right">
                            <p className="font-medium tabular-nums">{formatMoney(charge.amount, charge.currency)}</p>
                            <p className={paid ? "text-success text-xs" : instalment?.isOverdue ? "text-destructive text-xs" : "text-muted-foreground text-xs"}>
                              {charge.status === "cancelled" ? t("contracts.chargeCancelled") : paid ? t("contracts.paid") : instalment?.isOverdue ? t("contracts.overdue") : t("contracts.open")}
                            </p>
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </CardContent>
            </Card>
          ) : null}
          <Card>
            <CardHeader>
              <CardTitle>{t("timeline.title")}</CardTitle>
              <CardDescription>{t("contracts.timelineHint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed subjectType="contract" subjectId={id} canWriteNotes={hasPermission(granted, permissions.timeline.notesWrite)} />
            </CardContent>
          </Card>
        </div>

        <Card className="content-start">
          <CardHeader>
            <CardTitle>{t("contracts.terms")}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-4">
              <Fact label="contracts.field.amount">
                <span className="text-lg font-semibold tabular-nums">{money(data.amount)}</span>
              </Fact>
              <Fact label="contracts.column.nextBilling">{data.nextBillingDate ? formatCalendarDate(data.nextBillingDate) : "—"}</Fact>
              <Fact label="contracts.field.start">{formatCalendarDate(data.startDate)}</Fact>
              <Fact label="contracts.field.end">
                {data.endDate ? formatCalendarDate(data.endDate) : t("contracts.term.open")}
                {data.endDate && data.autoRenew ? <span className="text-muted-foreground"> · {t("contracts.autoRenews")}</span> : null}
              </Fact>
              <Fact label="contracts.field.paymentTerm">{t("contracts.days", { count: data.paymentTermDays })}</Fact>
              <Fact label="contracts.billedCount">{data.billedPeriods}</Fact>
              {data.quoteId ? (
                <Fact label="contracts.quote">
                  <Link href={`/sales/quotes/${data.quoteId}`} className="text-primary flex items-center gap-1 underline-offset-4 hover:underline">
                    <FileText className="size-3.5" aria-hidden /> {t("leads.openQuote")}
                  </Link>
                </Fact>
              ) : null}
              {data.scope ? (
                <Fact label="contracts.field.scope">
                  <span className="whitespace-pre-wrap">{data.scope}</span>
                </Fact>
              ) : null}
            </dl>
          </CardContent>
        </Card>
      </div>

      {editing ? <ContractDialog open onOpenChange={setEditing} contract={data} /> : null}
    </div>
  );
}

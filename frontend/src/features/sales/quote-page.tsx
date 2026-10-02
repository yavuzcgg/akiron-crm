"use client";

import { AlertCircle, ArrowLeft, BriefcaseBusiness, Building2, CircleCheck, CircleX, Eye, FileDown, PencilLine, Send } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useSession } from "@/features/identity/session";
import { useSaveWorkOrder } from "@/features/jobs/jobs-api";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatDateTime, formatMoney, formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { unitLabels } from "./labels";
import { QuoteEditor, TotalRow } from "./quote-editor";
import { QuoteStatusBadge } from "./quotes-list";
import { SendQuoteDialog } from "./send-dialog";
import { useQuote, useQuoteMutations, type Quote } from "./sales-api";

/** A quote that left the building: the document as sent, where it stands, and what can happen next. */
function QuoteView({ quote }: { quote: Quote }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const { revise } = useQuoteMutations(quote.id);
  const workOrder = useSaveWorkOrder();
  const canWrite = hasPermission(session?.permissions, permissions.sales.quotesWrite);
  const canOpenJobs = hasPermission(session?.permissions, permissions.jobs.workOrdersWrite);
  const money = (value: number) => formatMoney(value, quote.currency);
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  const steps = [
    { icon: Send, label: t("sales.quote.sentAt"), at: quote.sentAt },
    { icon: Eye, label: t("sales.quote.viewedAt"), at: quote.viewedAt },
    quote.decidedAt
      ? {
          icon: quote.status === "accepted" ? CircleCheck : CircleX,
          label: t(quote.status === "accepted" ? "sales.quote.acceptedBy" : "sales.quote.rejectedBy", { name: quote.decidedByName ?? "" }),
          at: quote.decidedAt,
        }
      : null,
  ].filter((step) => step !== null);

  return (
    <div className="grid gap-6">
      <Link href="/sales/quotes" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
        <ArrowLeft /> {t("sales.quotes.title")}
      </Link>

      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="grid gap-1.5">
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground font-mono text-xs">
              {quote.number}
              {quote.revision > 1 ? ` · R${quote.revision}` : ""}
            </span>
            <QuoteStatusBadge status={quote.status} />
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{quote.title}</h1>
          <Link href={`/crm/parties/${quote.partyId}`} className="text-primary flex items-center gap-1.5 text-sm underline-offset-4 hover:underline">
            <Building2 className="size-4" aria-hidden /> {quote.partyName}
          </Link>
        </div>
        <div className="flex flex-wrap gap-2">
          <a href={`/api/v1/sales/quotes/${quote.id}/pdf`} target="_blank" rel="noopener" className={buttonVariants({ variant: "outline" })}>
            <FileDown /> PDF
          </a>
          {canWrite && (quote.status === "sent" || quote.status === "rejected" || quote.status === "expired") ? (
            <Button
              variant="outline"
              onClick={() => {
                if (!window.confirm(t("sales.quote.reviseConfirm"))) return;
                revise.mutate(undefined, { onError: fail });
              }}
              disabled={revise.isPending}
            >
              <PencilLine /> {t("sales.quote.revise")}
            </Button>
          ) : null}
          {quote.status === "accepted" && canOpenJobs ? (
            <Button
              onClick={() =>
                workOrder.mutate(
                  { title: quote.title, quoteId: quote.id, assigneeIds: [] },
                  { onSuccess: (created) => router.push(`/jobs/${created.id}`), onError: fail },
                )
              }
              disabled={workOrder.isPending}
            >
              <BriefcaseBusiness /> {t("sales.quote.openWorkOrder")}
            </Button>
          ) : null}
        </div>
      </div>

      {quote.status === "rejected" && quote.decisionNote ? (
        <Alert variant="destructive">
          <CircleX />
          <AlertDescription>{t("sales.quote.rejectionNote", { name: quote.decidedByName ?? "", note: quote.decisionNote })}</AlertDescription>
        </Alert>
      ) : null}
      {quote.status === "accepted" && quote.decisionNote ? (
        <Alert>
          <CircleCheck />
          <AlertDescription>{t("sales.quote.acceptanceNote", { name: quote.decidedByName ?? "", note: quote.decisionNote })}</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="grid content-start gap-6">
          <Card className="py-0">
            <CardContent className="overflow-x-auto px-0">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/60 hover:bg-muted/60">
                    <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("sales.quote.lineName")}</TableHead>
                    <TableHead className="text-muted-foreground h-10 text-right text-xs font-medium">{t("sales.field.quantity")}</TableHead>
                    <TableHead className="text-muted-foreground hidden h-10 text-right text-xs font-medium sm:table-cell">{t("sales.field.unitPrice")}</TableHead>
                    <TableHead className="text-muted-foreground hidden h-10 text-right text-xs font-medium md:table-cell">{t("sales.field.vatRate")}</TableHead>
                    <TableHead className="text-muted-foreground h-10 pr-5 text-right text-xs font-medium">{t("sales.quote.lineNet")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {quote.lines.map((line) => (
                    <TableRow key={line.id}>
                      <TableCell className="py-3 pl-5">
                        <span className="font-medium">{line.name}</span>
                        {line.description ? <span className="text-muted-foreground block text-xs">{line.description}</span> : null}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatNumber(line.quantity)} {t(unitLabels[line.unit] ?? "sales.unit.piece")}
                      </TableCell>
                      <TableCell className="hidden text-right tabular-nums sm:table-cell">
                        {money(line.unitPrice)}
                        {line.discountPercent > 0 ? <span className="text-muted-foreground block text-xs">-%{formatNumber(line.discountPercent)}</span> : null}
                      </TableCell>
                      <TableCell className="text-muted-foreground hidden text-right md:table-cell">%{line.vatRate}</TableCell>
                      <TableCell className="pr-5 text-right font-medium tabular-nums">{money(line.net)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          {quote.notes ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("sales.field.notes")}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm whitespace-pre-wrap">{quote.notes}</p>
              </CardContent>
            </Card>
          ) : null}

          <Card>
            <CardHeader>
              <CardTitle>{t("timeline.title")}</CardTitle>
              <CardDescription>{t("sales.quote.timelineHint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed subjectType="quote" subjectId={quote.id} canWriteNotes={hasPermission(session?.permissions, permissions.timeline.notesWrite)} />
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card>
            <CardContent className="grid gap-2 text-sm">
              {quote.totals.discount > 0 ? (
                <>
                  <TotalRow label={t("sales.totals.gross")} value={money(quote.totals.gross)} />
                  <TotalRow label={t("sales.totals.discount")} value={money(-quote.totals.discount)} />
                </>
              ) : null}
              <TotalRow label={t("sales.totals.net")} value={money(quote.totals.net)} />
              <TotalRow label={t("sales.totals.vat")} value={money(quote.totals.vat)} />
              {quote.totals.withholding > 0 ? <TotalRow label={t("sales.totals.withholding")} value={money(-quote.totals.withholding)} /> : null}
              <div className="border-primary mt-1 border-t pt-2">
                <TotalRow label={t("sales.totals.grand")} value={money(quote.totals.grand)} strong />
              </div>
              {quote.exchangeRate ? (
                <p className="text-muted-foreground text-xs tabular-nums">
                  {t("sales.totals.rateNote", { rate: String(quote.exchangeRate).replace(".", ","), currency: quote.currency, net: formatMoney(quote.totals.netTry) })}
                </p>
              ) : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t("sales.quote.progress")}</CardTitle>
            </CardHeader>
            <CardContent>
              <ol className="grid gap-3 text-sm">
                {steps.map((step) => (
                  <li key={step.label} className="flex items-start gap-3">
                    <step.icon className={step.at ? "text-primary mt-0.5 size-4" : "text-muted-foreground/50 mt-0.5 size-4"} aria-hidden />
                    <div className="grid">
                      <span className={step.at ? "font-medium" : "text-muted-foreground"}>{step.label}</span>
                      <span className="text-muted-foreground text-xs">{step.at ? formatDateTime(step.at) : t("sales.quote.notYet")}</span>
                    </div>
                  </li>
                ))}
              </ol>
              <dl className="text-muted-foreground mt-4 grid grid-cols-2 gap-2 border-t pt-4 text-xs">
                <div>
                  <dt>{t("sales.field.issueDate")}</dt>
                  <dd className="text-foreground text-sm">{formatCalendarDate(quote.issueDate)}</dd>
                </div>
                <div>
                  <dt>{t("sales.field.validUntil")}</dt>
                  <dd className="text-foreground text-sm">{formatCalendarDate(quote.validUntil)}</dd>
                </div>
              </dl>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

/** /sales/quotes/[id]: the editor while a draft, the document afterwards. */
export function QuotePage({ id }: { id: string }) {
  const { tError } = useI18n();
  const quote = useQuote(id);
  const searchParams = useSearchParams();
  // A brand-new quote sent from the editor arrives with ?send=1 to open the send dialog.
  const [sendOpen, setSendOpen] = useState(() => searchParams.get("send") === "1");
  const router = useRouter();

  // Drop ?send=1 once read, so a reload does not offer to send again.
  useEffect(() => {
    if (searchParams.get("send") === "1") router.replace(`/sales/quotes/${id}`);
  }, [id, router, searchParams]);

  if (quote.isPending) return <Skeleton className="h-96 w-full" />;
  if (quote.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>{tError(quote.error instanceof ApiError ? quote.error.code : "common.network")}</AlertDescription>
      </Alert>
    );
  }

  // The dialog stays mounted across the draft → sent switch: it holds the link, shown only once.
  return (
    <>
      {quote.data.status === "draft" ? (
        <QuoteEditor key={`${quote.data.id}:${quote.data.revision}`} quote={quote.data} onSend={() => setSendOpen(true)} />
      ) : (
        <QuoteView quote={quote.data} />
      )}
      <SendQuoteDialog quote={quote.data} open={sendOpen} onOpenChange={setSendOpen} />
    </>
  );
}


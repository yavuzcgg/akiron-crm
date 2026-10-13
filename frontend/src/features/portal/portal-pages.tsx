"use client";

import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, BriefcaseBusiness, CalendarDays, ChevronRight, CircleCheck, CircleX, FileDown, FileText, Wallet } from "lucide-react";
import Link from "next/link";
import { useState, type ReactNode } from "react";
import { EmptyState } from "@/components/empty-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { methodLabels } from "@/features/finance/money-dialogs";
import { statusBadges, unitLabels } from "@/features/sales/labels";
import { TotalRow } from "@/features/sales/quote-editor";
import { ApiError, unwrap } from "@/lib/api/errors";
import { portalApi } from "@/lib/api/portal-client";
import { formatCalendarDate, formatDateTime, formatMoney, formatNumber } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  usePortalAccount,
  usePortalContracts,
  usePortalDecide,
  usePortalMe,
  usePortalQuote,
  usePortalQuotes,
  usePortalWorkOrder,
  usePortalWorkOrders,
  type ClientQuoteRow,
  type ClientWorkOrder,
} from "./portal-api";
import { FileBox, PortalError, ProgressBar, StageBadge, Thread } from "./portal-parts";

function Title({ title, description }: { title: string; description?: string }) {
  return (
    <div className="grid gap-1">
      <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{title}</h1>
      {description ? <p className="text-muted-foreground">{description}</p> : null}
    </div>
  );
}

function Back({ href, label }: { href: string; label: string }) {
  return (
    <Link href={href} className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {label}
    </Link>
  );
}

/** A tappable row: what it is on the left, where it stands on the right. */
function Row({ href, children }: { href: string; children: ReactNode }) {
  return (
    <li>
      <Link href={href} className="hover:bg-muted/40 focus-visible:bg-muted/40 flex items-center gap-3 px-4 py-3.5 focus-visible:outline-none">
        <div className="grid min-w-0 flex-1 gap-1">{children}</div>
        <ChevronRight className="text-muted-foreground size-4 shrink-0" aria-hidden />
      </Link>
    </li>
  );
}

function JobRow({ job }: { job: ClientWorkOrder }) {
  const { t } = useI18n();
  return (
    <Row href={`/portal/jobs/${job.id}`}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{job.title}</span>
        <StageBadge workOrder={job} />
      </div>
      <span className="text-muted-foreground text-xs">
        {job.number}
        {job.dueDate && job.stageCategory !== "done" ? ` · ${t("portal.jobs.due", { date: formatCalendarDate(job.dueDate) })}` : ""}
      </span>
      <ProgressBar workOrder={job} />
    </Row>
  );
}

function QuoteStatus({ status }: { status: string }) {
  const { t } = useI18n();
  const badge = statusBadges[status] ?? statusBadges.sent!;
  return <Badge variant={badge.variant}>{t(status === "sent" ? "portal.quotes.awaiting" : badge.label)}</Badge>;
}

function QuoteRow({ quote }: { quote: ClientQuoteRow }) {
  const { t } = useI18n();
  return (
    <Row href={`/portal/quotes/${quote.id}`}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{quote.title}</span>
        <QuoteStatus status={quote.status} />
      </div>
      <span className="text-muted-foreground text-xs tabular-nums">
        {quote.number} · {formatMoney(quote.grandTotal, quote.currency)}
        {quote.status === "sent" ? ` · ${t("portal.quotes.validUntil", { date: formatCalendarDate(quote.validUntil) })}` : ""}
      </span>
    </Row>
  );
}

/** How many posts wait for the client's answer; the portal's most frequent reason for a visit. */
function ContentWaiting() {
  const { t } = useI18n();
  const posts = useQuery({ queryKey: ["portal", "content"], queryFn: async () => unwrap(await portalApi.GET("/api/v1/social/portal/posts")) });
  const waiting = (posts.data ?? []).filter((post) => post.status === "client_review").length;
  if (waiting === 0) return null;
  return (
    <Link href="/portal/content" className="focus-visible:ring-ring/40 rounded-xl focus-visible:ring-2 focus-visible:outline-none">
      <Card>
        <CardContent className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <span className="bg-primary-soft text-primary flex size-10 shrink-0 items-center justify-center rounded-full">
              <CalendarDays className="size-5" aria-hidden />
            </span>
            <p className="font-medium">{t("portal.home.contentWaiting", { count: waiting })}</p>
          </div>
          <span className={buttonVariants({ size: "sm" })}>{t("portal.home.review")}</span>
        </CardContent>
      </Card>
    </Link>
  );
}

function ListCard({ children }: { children: ReactNode }) {
  return (
    <Card className="py-0">
      <CardContent className="px-0">
        <ul className="divide-border divide-y">{children}</ul>
      </CardContent>
    </Card>
  );
}

/** The first screen: what needs the client (a quote to answer, money due), then their open work. */
export function PortalHome() {
  const { t } = useI18n();
  const me = usePortalMe();
  const jobs = usePortalWorkOrders();
  const quotes = usePortalQuotes();
  const account = usePortalAccount();

  if (me.isError) return <PortalError error={me.error} />;

  const waiting = (quotes.data ?? []).filter((quote) => quote.status === "sent");
  const openJobs = (jobs.data ?? []).filter((job) => job.stageCategory !== "done");
  const due = (account.data?.balances ?? []).filter((balance) => balance.balance > 0);
  const overdue = due.some((balance) => balance.overdue > 0);
  const firstName = me.data?.name.split(" ")[0] ?? "";

  return (
    <div className="grid gap-6">
      <Title title={me.data ? t("portal.home.greeting", { name: firstName }) : "…"} description={me.data ? t("portal.home.intro", { party: me.data.partyName }) : undefined} />
      {me.data?.welcomeNote ? <p className="bg-primary-soft rounded-xl px-4 py-3 text-sm whitespace-pre-wrap">{me.data.welcomeNote}</p> : null}

      <ContentWaiting />

      {waiting.length > 0 ? (
        <section className="grid gap-2" aria-labelledby="waiting-title">
          <h2 id="waiting-title" className="text-sm font-semibold">
            {t("portal.home.waiting")}
          </h2>
          <ListCard>
            {waiting.map((quote) => (
              <QuoteRow key={quote.id} quote={quote} />
            ))}
          </ListCard>
        </section>
      ) : null}

      {due.length > 0 ? (
        <Link href="/portal/account" className="focus-visible:ring-ring/40 rounded-xl focus-visible:ring-2 focus-visible:outline-none">
          <Card>
            <CardContent className="flex items-center justify-between gap-4">
              <div className="grid gap-1">
                <p className="text-muted-foreground text-sm">{t(overdue ? "portal.home.dueOverdue" : "portal.home.due")}</p>
                {due.map((balance) => (
                  <p key={balance.currency} className={cn("text-[22px] leading-7 font-semibold tabular-nums", overdue && "text-destructive")}>
                    {formatMoney(balance.balance, balance.currency)}
                  </p>
                ))}
              </div>
              <span className={buttonVariants({ variant: "outline", size: "sm" })}>{t("portal.home.seeAccount")}</span>
            </CardContent>
          </Card>
        </Link>
      ) : null}

      <section className="grid gap-2" aria-labelledby="jobs-title">
        <h2 id="jobs-title" className="text-sm font-semibold">
          {t("portal.home.openJobs")}
        </h2>
        {jobs.isPending ? (
          <Skeleton className="h-24 w-full" />
        ) : jobs.isError ? (
          <PortalError error={jobs.error} />
        ) : openJobs.length === 0 ? (
          <Card>
            <EmptyState icon={BriefcaseBusiness} title={t("portal.home.noOpenJobs")} className="py-8" />
          </Card>
        ) : (
          <ListCard>
            {openJobs.map((job) => (
              <JobRow key={job.id} job={job} />
            ))}
          </ListCard>
        )}
      </section>
    </div>
  );
}

export function PortalJobs() {
  const { t } = useI18n();
  const jobs = usePortalWorkOrders();
  return (
    <div className="grid gap-6">
      <Title title={t("portal.jobs.title")} description={t("portal.jobs.description")} />
      {jobs.isPending ? (
        <Skeleton className="h-40 w-full" />
      ) : jobs.isError ? (
        <PortalError error={jobs.error} />
      ) : jobs.data.length === 0 ? (
        <Card>
          <EmptyState icon={BriefcaseBusiness} title={t("portal.jobs.empty")} description={t("portal.jobs.emptyHint")} />
        </Card>
      ) : (
        <ListCard>
          {jobs.data.map((job) => (
            <JobRow key={job.id} job={job} />
          ))}
        </ListCard>
      )}
    </div>
  );
}

export function PortalJob({ id }: { id: string }) {
  const { t } = useI18n();
  const job = usePortalWorkOrder(id);
  const back = <Back href="/portal/jobs" label={t("portal.jobs.title")} />;

  if (job.isPending) return <Skeleton className="h-80 w-full" />;
  if (job.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <PortalError error={job.error} />
      </div>
    );
  }

  const data = job.data;
  return (
    <div className="grid gap-6">
      {back}
      <div className="grid gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-muted-foreground font-mono text-xs">{data.number}</span>
          <StageBadge workOrder={data} />
        </div>
        <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{data.title}</h1>
        <p className="text-muted-foreground text-sm">
          {data.completedAt
            ? t("portal.jobs.completedOn", { date: formatCalendarDate(data.completedAt.slice(0, 10)) })
            : data.dueDate
              ? t("portal.jobs.due", { date: formatCalendarDate(data.dueDate) })
              : t("portal.jobs.noDue")}
        </p>
        <ProgressBar workOrder={data} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("portal.thread.title")}</CardTitle>
          <CardDescription>{t("portal.thread.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <Thread subjectType="work_order" subjectId={id} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("portal.files.title")}</CardTitle>
          <CardDescription>{t("portal.files.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <FileBox subjectType="work_order" subjectId={id} uploadLabel={t("portal.files.send")} />
        </CardContent>
      </Card>
    </div>
  );
}

const contractStatus: Record<string, TranslationKey> = {
  active: "contracts.status.active",
  paused: "contracts.status.paused",
  ended: "contracts.status.ended",
};

export function PortalQuotes() {
  const { t } = useI18n();
  const quotes = usePortalQuotes();
  const contracts = usePortalContracts();
  return (
    <div className="grid gap-6">
      <Title title={t("portal.quotes.title")} description={t("portal.quotes.description")} />
      {quotes.isPending ? (
        <Skeleton className="h-40 w-full" />
      ) : quotes.isError ? (
        <PortalError error={quotes.error} />
      ) : quotes.data.length === 0 ? (
        <Card>
          <EmptyState icon={FileText} title={t("portal.quotes.empty")} />
        </Card>
      ) : (
        <ListCard>
          {quotes.data.map((quote) => (
            <QuoteRow key={quote.id} quote={quote} />
          ))}
        </ListCard>
      )}

      {(contracts.data ?? []).length > 0 ? (
        <section className="grid gap-2" aria-labelledby="contracts-title">
          <h2 id="contracts-title" className="text-sm font-semibold">
            {t("portal.contracts.title")}
          </h2>
          <Card className="py-0">
            <CardContent className="px-0">
              <ul className="divide-border divide-y">
                {contracts.data!.map((contract) => (
                  <li key={contract.id} className="grid gap-1 px-4 py-3.5">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">{contract.title}</span>
                        <Badge variant={contract.status === "active" ? "default" : "outline"}>{t(contractStatus[contract.status] ?? "contracts.status.ended")}</Badge>
                      </div>
                      <span className="font-medium tabular-nums">{t("portal.contracts.monthly", { amount: formatMoney(contract.amount, contract.currency) })}</span>
                    </div>
                    <span className="text-muted-foreground text-xs">
                      {contract.number} · {formatCalendarDate(contract.startDate)} – {contract.endDate ? formatCalendarDate(contract.endDate) : t("contracts.term.open")}
                      {contract.nextBillingDate ? ` · ${t("portal.contracts.next", { date: formatCalendarDate(contract.nextBillingDate) })}` : ""}
                    </span>
                    {contract.scope ? <p className="text-muted-foreground text-sm whitespace-pre-wrap">{contract.scope}</p> : null}
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        </section>
      ) : null}
    </div>
  );
}

export function PortalQuote({ id }: { id: string }) {
  const { t, tError, locale } = useI18n();
  const quote = usePortalQuote(id);
  const decide = usePortalDecide(id);
  const [mode, setMode] = useState<"accept" | "reject" | null>(null);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const back = <Back href="/portal/quotes" label={t("portal.quotes.title")} />;

  if (quote.isPending) return <Skeleton className="h-96 w-full" />;
  if (quote.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <PortalError error={quote.error} />
      </div>
    );
  }

  const data = decide.data ?? quote.data;
  const money = (value: number) => formatMoney(value, data.currency);

  const submit = async () => {
    setError(null);
    try {
      await decide.mutateAsync({ accept: mode === "accept", note: note.trim() });
      setMode(null);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <div className="grid gap-6">
      {back}
      <Card>
        <CardContent className="grid gap-6">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="grid gap-1.5">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-muted-foreground font-mono text-xs">{data.number}</span>
                <QuoteStatus status={data.status} />
              </div>
              <h1 className="text-xl font-semibold tracking-tight">{data.title}</h1>
            </div>
            <div className="text-muted-foreground grid gap-0.5 text-sm sm:text-right">
              <p>
                {t("sales.field.issueDate")}: {formatCalendarDate(data.issueDate)}
              </p>
              <p>
                {t("sales.field.validUntil")}: {formatCalendarDate(data.validUntil)}
              </p>
            </div>
          </div>

          <ul className="divide-border divide-y border-y">
            {data.lines.map((line, index) => (
              <li key={index} className="flex items-start justify-between gap-4 py-3 text-sm">
                <div className="grid min-w-0 gap-0.5">
                  <span className="font-medium">{line.name}</span>
                  {line.description ? <span className="text-muted-foreground text-xs">{line.description}</span> : null}
                  <span className="text-muted-foreground text-xs tabular-nums">
                    {formatNumber(line.quantity)} {t(unitLabels[line.unit] ?? "sales.unit.piece")} × {money(line.unitPrice)}
                    {line.discountPercent > 0 ? ` · -%${formatNumber(line.discountPercent)}` : ""} · KDV %{line.vatRate}
                  </span>
                </div>
                <span className="shrink-0 font-medium tabular-nums">{money(line.net)}</span>
              </li>
            ))}
          </ul>

          <div className="grid gap-2 text-sm sm:ml-auto sm:w-72">
            <TotalRow label={t("sales.totals.net")} value={money(data.totals.net)} />
            <TotalRow label={t("sales.totals.vat")} value={money(data.totals.vat)} />
            {data.totals.withholding > 0 ? <TotalRow label={t("sales.totals.withholding")} value={money(-data.totals.withholding)} /> : null}
            <div className="border-primary mt-1 border-t pt-2">
              <TotalRow label={t("sales.totals.grand")} value={money(data.totals.grand)} strong />
            </div>
          </div>

          {data.notes ? (
            <div className="bg-muted/50 rounded-lg p-4 text-sm">
              <p className="mb-1 font-medium">{t("sales.field.notes")}</p>
              <p className="whitespace-pre-wrap">{data.notes}</p>
            </div>
          ) : null}

          <a href={`/api/v1/sales/portal/quotes/${id}/pdf?lang=${locale}`} target="_blank" rel="noopener" className={buttonVariants({ variant: "outline", className: "justify-self-start" })}>
            <FileDown /> {t("sales.public.downloadPdf")}
          </a>
        </CardContent>
      </Card>

      {data.status === "accepted" || data.status === "rejected" ? (
        <Alert variant={data.status === "accepted" ? "default" : "destructive"}>
          {data.status === "accepted" ? <CircleCheck /> : <CircleX />}
          <AlertDescription>
            {t(data.status === "accepted" ? "sales.public.accepted" : "sales.public.rejected", { name: data.decidedByName ?? "", date: data.decidedAt ? formatDateTime(data.decidedAt) : "" })}
          </AlertDescription>
        </Alert>
      ) : data.status === "expired" ? (
        <Alert variant="destructive">
          <CircleX />
          <AlertDescription>{t("sales.public.expired", { workspace: data.workspaceName })}</AlertDescription>
        </Alert>
      ) : mode === null ? (
        <div className="flex flex-col gap-3 sm:flex-row sm:justify-end">
          <Button variant="outline" size="lg" onClick={() => setMode("reject")}>
            <CircleX /> {t("sales.public.reject")}
          </Button>
          <Button size="lg" onClick={() => setMode("accept")}>
            <CircleCheck /> {t("sales.public.accept")}
          </Button>
        </div>
      ) : (
        <Card>
          <CardContent className="grid gap-4">
            <p className="font-semibold">{t(mode === "accept" ? "sales.public.acceptTitle" : "sales.public.rejectTitle")}</p>
            {error ? (
              <Alert variant="destructive">
                <CircleX />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            ) : null}
            <div className="grid gap-1.5">
              <Label htmlFor="portal-decision-note" className="text-[13px] font-medium">
                {t(mode === "accept" ? "sales.public.noteOptional" : "sales.public.reason")}
              </Label>
              <Textarea id="portal-decision-note" rows={3} maxLength={2000} value={note} onChange={(event) => setNote(event.target.value)} className="text-base md:text-sm" />
            </div>
            {mode === "accept" ? <p className="text-muted-foreground text-[13px]">{t("sales.public.acceptHint", { total: money(data.totals.grand) })}</p> : null}
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setMode(null)}>
                {t("common.cancel")}
              </Button>
              <Button variant={mode === "accept" ? "default" : "destructive"} disabled={decide.isPending} onClick={() => void submit()}>
                {t(mode === "accept" ? "sales.public.confirmAccept" : "sales.public.confirmReject")}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

/** What is due, how to pay, what was paid; the full statement sits behind a disclosure. */
export function PortalAccount() {
  const { t } = useI18n();
  const me = usePortalMe();
  const account = usePortalAccount();

  if (account.isPending) {
    return (
      <div className="grid gap-6">
        <Title title={t("portal.account.title")} />
        <Skeleton className="h-60 w-full" />
      </div>
    );
  }
  if (account.isError) return <PortalError error={account.error} />;

  const data = account.data;
  const nothing = data.lines.length === 0;

  return (
    <div className="grid gap-6">
      <Title title={t("portal.account.title")} description={t("portal.account.description")} />

      {nothing ? (
        <Card>
          <EmptyState icon={Wallet} title={t("portal.account.empty")} />
        </Card>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          {data.balances.map((balance) => (
            <Card key={balance.currency}>
              <CardContent className="grid gap-1">
                <p className="text-muted-foreground text-sm">{t(balance.balance < 0 ? "portal.account.credit" : "portal.account.balance")}</p>
                <p className={cn("text-[22px] leading-7 font-semibold tabular-nums", balance.balance < 0 && "text-success")}>{formatMoney(Math.abs(balance.balance), balance.currency)}</p>
                {balance.overdue > 0 ? <p className="text-destructive text-sm font-medium tabular-nums">{t("portal.account.overdue", { amount: formatMoney(balance.overdue, balance.currency) })}</p> : null}
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {data.openItems.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>{t("portal.account.toPay")}</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="divide-border divide-y">
              {data.openItems.map((item) => (
                <li key={item.id} className="flex items-start justify-between gap-3 py-3 first:pt-0 last:pb-0">
                  <div className="grid min-w-0 gap-0.5">
                    <span className="text-sm font-medium">{item.description}</span>
                    <span className={cn("text-xs", item.isOverdue ? "text-destructive font-medium" : "text-muted-foreground")}>
                      {item.instalmentCount > 1 ? `${t("finance.instalmentOf", { number: item.sequence, count: item.instalmentCount })} · ` : ""}
                      {item.isOverdue
                        ? t("portal.account.lateSince", { date: formatCalendarDate(item.dueDate), days: item.daysOverdue })
                        : t("portal.account.dueOn", { date: formatCalendarDate(item.dueDate) })}
                    </span>
                  </div>
                  <span className="shrink-0 font-medium tabular-nums">{formatMoney(item.remaining, item.currency)}</span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("portal.account.howToPay")}</CardTitle>
          <CardDescription>{t("portal.account.howToPayHint")}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          {me.data?.paymentInstructions ? (
            <p className="bg-muted/50 rounded-lg p-4 font-mono text-sm break-words whitespace-pre-wrap">{me.data.paymentInstructions}</p>
          ) : (
            <p className="text-muted-foreground text-sm">{t("portal.account.noInstructions", { workspace: me.data?.workspaceName ?? "" })}</p>
          )}
          {me.data ? <FileBox subjectType="party" subjectId={me.data.partyId} uploadLabel={t("portal.account.sendReceipt")} /> : null}
        </CardContent>
      </Card>

      {data.payments.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>{t("portal.account.payments")}</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="divide-border divide-y">
              {data.payments.map((payment) => (
                <li key={payment.number} className="flex items-center justify-between gap-3 py-2.5 text-sm first:pt-0 last:pb-0">
                  <span className="text-muted-foreground">
                    {formatCalendarDate(payment.date)} · {t(methodLabels[payment.method] ?? "finance.method.other")}
                  </span>
                  <span className="text-success font-medium tabular-nums">{formatMoney(payment.amount, payment.currency)}</span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      {!nothing ? (
        <details className="bg-card rounded-xl border">
          <summary className="cursor-pointer px-4 py-3 text-sm font-medium">{t("portal.account.statement", { count: data.lines.length })}</summary>
          <div className="overflow-x-auto border-t">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-muted/60 text-muted-foreground text-xs">
                  <th className="h-9 pl-4 text-left font-medium">{t("finance.date")}</th>
                  <th className="h-9 text-left font-medium">{t("finance.description")}</th>
                  <th className="h-9 text-right font-medium">{t("portal.account.charged")}</th>
                  <th className="h-9 text-right font-medium">{t("portal.account.paid")}</th>
                  <th className="h-9 pr-4 text-right font-medium">{t("finance.column.balance")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {data.lines.map((line) => (
                  <tr key={line.id} className={cn(line.isReversal && "text-muted-foreground")}>
                    <td className="py-2 pl-4 whitespace-nowrap tabular-nums">{formatCalendarDate(line.date)}</td>
                    <td className="max-w-56 truncate py-2">{line.description || line.documentNumber}</td>
                    <td className="py-2 text-right tabular-nums">{line.debit > 0 ? formatMoney(line.debit, line.currency) : ""}</td>
                    <td className="py-2 text-right tabular-nums">{line.credit > 0 ? formatMoney(line.credit, line.currency) : ""}</td>
                    <td className="py-2 pr-4 text-right font-medium tabular-nums">{formatMoney(line.balance, line.currency)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
      ) : null}
    </div>
  );
}

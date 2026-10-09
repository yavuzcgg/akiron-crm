"use client";

import { AlertCircle, ChevronLeft, ChevronRight, HandCoins, ReceiptText, Undo2, Wallet } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useSession } from "@/features/identity/session";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatMoney } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { useBalances, useFinanceMutations, useFinanceSummary, useOpenItems, useReceipts, useToInvoice, type FinanceSummary } from "./finance-api";
import { ChargeDialog, DueLabel, methodLabels, ReceiptDialog } from "./money-dialogs";

type Tab = "due" | "balances" | "receipts" | "invoice";

const tabs: { value: Tab; label: TranslationKey }[] = [
  { value: "due", label: "finance.tab.due" },
  { value: "balances", label: "finance.tab.balances" },
  { value: "receipts", label: "finance.tab.receipts" },
  { value: "invoice", label: "finance.tab.toInvoice" },
];

const head = "text-muted-foreground h-10 text-xs font-medium";

function Amounts({ values, tone }: { values: FinanceSummary["receivable"]; tone?: string }) {
  if (values.length === 0) return <p className={cn("text-2xl font-semibold tabular-nums", tone)}>{formatMoney(0)}</p>;
  return (
    <div className="grid gap-0.5">
      {values.map((value) => (
        <p key={value.currency} className={cn("text-2xl font-semibold tabular-nums first:text-2xl [&:not(:first-child)]:text-base", tone)}>
          {formatMoney(value.amount, value.currency)}
        </p>
      ))}
    </div>
  );
}

function SummaryCards() {
  const { t } = useI18n();
  const summary = useFinanceSummary();
  if (summary.isPending) return <Skeleton className="h-28 w-full" />;
  if (summary.isError) return null;
  const data = summary.data;
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      <Card>
        <CardContent className="grid gap-1.5">
          <p className="text-muted-foreground text-sm">{t("finance.summary.receivable")}</p>
          <Amounts values={data.receivable} />
        </CardContent>
      </Card>
      <Card>
        <CardContent className="grid gap-1.5">
          <p className="text-muted-foreground text-sm">{t("finance.summary.overdue", { count: data.overdueCount })}</p>
          <Amounts values={data.overdue} tone={data.overdueCount > 0 ? "text-destructive" : undefined} />
        </CardContent>
      </Card>
      <Card>
        <CardContent className="grid gap-1.5">
          <p className="text-muted-foreground text-sm">{t("finance.summary.collected")}</p>
          <Amounts values={data.collectedThisMonth} tone="text-success" />
        </CardContent>
      </Card>
    </div>
  );
}

function Failure({ error }: { error: unknown }) {
  const { t, tError } = useI18n();
  return (
    <div className="p-5">
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>
          {error instanceof ApiError && error.status === 403 ? t("finance.noPermission") : tError(error instanceof ApiError ? error.code : "common.network")}
        </AlertDescription>
      </Alert>
    </div>
  );
}

function Loading() {
  return (
    <div className="grid gap-2 p-5">
      <Skeleton className="h-11 w-full" />
      <Skeleton className="h-11 w-full" />
    </div>
  );
}

function DueTab() {
  const { t } = useI18n();
  const [overdue, setOverdue] = useState(false);
  const items = useOpenItems({ overdue });
  return (
    <>
      <div className="flex items-center gap-2 border-b px-5 py-3 text-sm">
        <label className="flex cursor-pointer items-center gap-2">
          <input type="checkbox" className="accent-primary size-4" checked={overdue} onChange={(event) => setOverdue(event.target.checked)} />
          {t("finance.onlyOverdue")}
        </label>
      </div>
      {items.isPending ? (
        <Loading />
      ) : items.isError ? (
        <Failure error={items.error} />
      ) : items.data.length === 0 ? (
        <EmptyState icon={HandCoins} title={t(overdue ? "finance.due.noneOverdue" : "finance.due.empty")} description={overdue ? undefined : t("finance.due.emptyHint")} />
      ) : (
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/60 hover:bg-muted/60">
                <TableHead className={cn(head, "pl-5")}>{t("finance.column.party")}</TableHead>
                <TableHead className={cn(head, "hidden md:table-cell")}>{t("finance.description")}</TableHead>
                <TableHead className={head}>{t("finance.column.due")}</TableHead>
                <TableHead className={cn(head, "pr-5 text-right")}>{t("finance.column.remaining")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.data.map((item) => (
                <TableRow key={item.id} className="hover:bg-muted/40 relative">
                  <TableCell className="py-3 pl-5">
                    <div className="grid min-w-0">
                      <Link href={`/crm/parties/${item.partyId}`} className="truncate font-medium after:absolute after:inset-0 focus-visible:outline-none">
                        {item.partyName}
                      </Link>
                      <span className="text-muted-foreground font-mono text-xs">
                        {item.chargeNumber}
                        {item.instalmentCount > 1 ? ` · ${t("finance.instalmentOf", { number: item.sequence, count: item.instalmentCount })}` : ""}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground hidden max-w-72 truncate md:table-cell">{item.description}</TableCell>
                  <TableCell className="tabular-nums">
                    <DueLabel dueDate={item.dueDate} overdue={item.isOverdue} days={item.daysOverdue} />
                  </TableCell>
                  <TableCell className="pr-5 text-right font-medium tabular-nums">{formatMoney(item.remaining, item.currency)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}

function BalancesTab() {
  const { t } = useI18n();
  const balances = useBalances();
  if (balances.isPending) return <Loading />;
  if (balances.isError) return <Failure error={balances.error} />;
  if (balances.data.length === 0) return <EmptyState icon={Wallet} title={t("finance.balances.empty")} />;
  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/60 hover:bg-muted/60">
            <TableHead className={cn(head, "pl-5")}>{t("finance.column.party")}</TableHead>
            <TableHead className={cn(head, "hidden sm:table-cell")}>{t("finance.column.oldestDue")}</TableHead>
            <TableHead className={cn(head, "text-right")}>{t("finance.column.overdue")}</TableHead>
            <TableHead className={cn(head, "pr-5 text-right")}>{t("finance.column.balance")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {balances.data.map((row) => (
            <TableRow key={`${row.partyId}-${row.currency}`} className="hover:bg-muted/40 relative">
              <TableCell className="py-3 pl-5">
                <Link href={`/crm/parties/${row.partyId}`} className="font-medium after:absolute after:inset-0 focus-visible:outline-none">
                  {row.partyName}
                </Link>
              </TableCell>
              <TableCell className="text-muted-foreground hidden tabular-nums sm:table-cell">{row.oldestDue ? formatCalendarDate(row.oldestDue) : "—"}</TableCell>
              <TableCell className={cn("text-right tabular-nums", row.overdue > 0 ? "text-destructive font-medium" : "text-muted-foreground")}>
                {row.overdue > 0 ? formatMoney(row.overdue, row.currency) : "—"}
              </TableCell>
              <TableCell className={cn("pr-5 text-right font-medium tabular-nums", row.balance < 0 && "text-success")}>{formatMoney(row.balance, row.currency)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function ReceiptsTab({ canWrite }: { canWrite: boolean }) {
  const { t, tError } = useI18n();
  const [page, setPage] = useState(1);
  const receipts = useReceipts(page);
  const { reverse, allocate } = useFinanceMutations();
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  if (receipts.isPending) return <Loading />;
  if (receipts.isError) return <Failure error={receipts.error} />;
  if (receipts.data.items.length === 0) return <EmptyState icon={HandCoins} title={t("finance.receipts.empty")} description={t("finance.receipts.emptyHint")} />;
  const pageCount = Math.max(1, Math.ceil(receipts.data.totalCount / 25));

  return (
    <>
      <div className={cn("overflow-x-auto", receipts.isPlaceholderData && "opacity-60")}>
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/60 hover:bg-muted/60">
              <TableHead className={cn(head, "pl-5")}>{t("finance.column.party")}</TableHead>
              <TableHead className={cn(head, "hidden md:table-cell")}>{t("finance.method")}</TableHead>
              <TableHead className={cn(head, "hidden sm:table-cell")}>{t("finance.date")}</TableHead>
              <TableHead className={cn(head, "text-right")}>{t("finance.amount")}</TableHead>
              <TableHead className={cn(head, "w-10 pr-5")}>
                <span className="sr-only">{t("finance.column.actions")}</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {receipts.data.items.map((receipt) => {
              const reversed = receipt.status === "reversed";
              return (
                <TableRow key={receipt.id} className={cn(reversed && "text-muted-foreground")}>
                  <TableCell className="py-3 pl-5">
                    <div className="grid min-w-0">
                      <Link href={`/crm/parties/${receipt.partyId}`} className="truncate font-medium underline-offset-4 hover:underline">
                        {receipt.partyName}
                      </Link>
                      <span className="text-muted-foreground flex items-center gap-1.5 font-mono text-xs">
                        {receipt.number}
                        {reversed ? <Badge variant="outline">{t("finance.receipt.reversed")}</Badge> : null}
                        {!reversed && receipt.unallocated > 0 ? <Badge variant="secondary">{t("finance.receipt.advance", { amount: formatMoney(receipt.unallocated, receipt.currency) })}</Badge> : null}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    {t(methodLabels[receipt.method] ?? "finance.method.other")}
                    {receipt.account ? <span className="text-muted-foreground"> · {receipt.account}</span> : null}
                  </TableCell>
                  <TableCell className="hidden tabular-nums sm:table-cell">{formatCalendarDate(receipt.date)}</TableCell>
                  <TableCell className={cn("text-right font-medium tabular-nums", reversed && "line-through")}>{formatMoney(receipt.amount, receipt.currency)}</TableCell>
                  <TableCell className="pr-5">
                    {canWrite && !reversed ? (
                      <div className="flex justify-end gap-1">
                        {receipt.unallocated > 0 ? (
                          <Button variant="ghost" size="icon-sm" aria-label={t("finance.receipt.allocate")} title={t("finance.receipt.allocate")} onClick={() => allocate.mutate(receipt.id, { onError: fail })}>
                            <HandCoins />
                          </Button>
                        ) : null}
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          aria-label={t("finance.receipt.reverse")}
                          title={t("finance.receipt.reverse")}
                          onClick={() => {
                            const reason = window.prompt(t("finance.receipt.reversePrompt", { number: receipt.number }));
                            if (!reason?.trim()) return;
                            reverse.mutate({ id: receipt.id, reason: reason.trim() }, { onSuccess: () => toast.success(t("finance.receipt.reversedToast")), onError: fail });
                          }}
                        >
                          <Undo2 />
                        </Button>
                      </div>
                    ) : null}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
      {pageCount > 1 ? (
        <div className="text-muted-foreground flex items-center justify-end gap-2 border-t px-5 py-3 text-sm">
          <span className="tabular-nums">{t("crm.list.page", { page, pageCount })}</span>
          <Button variant="outline" size="icon-sm" disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label={t("crm.list.previous")}>
            <ChevronLeft />
          </Button>
          <Button variant="outline" size="icon-sm" disabled={page >= pageCount} onClick={() => setPage(page + 1)} aria-label={t("crm.list.next")}>
            <ChevronRight />
          </Button>
        </div>
      ) : null}
    </>
  );
}

function ToInvoiceTab({ canWrite }: { canWrite: boolean }) {
  const { t, tError } = useI18n();
  const charges = useToInvoice();
  const { invoiced } = useFinanceMutations();
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  if (charges.isPending) return <Loading />;
  if (charges.isError) return <Failure error={charges.error} />;
  if (charges.data.length === 0) return <EmptyState icon={ReceiptText} title={t("finance.toInvoice.empty")} description={t("finance.toInvoice.emptyHint")} />;

  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/60 hover:bg-muted/60">
            <TableHead className={cn(head, "pl-5")}>{t("finance.column.party")}</TableHead>
            <TableHead className={cn(head, "hidden md:table-cell")}>{t("finance.description")}</TableHead>
            <TableHead className={cn(head, "text-right")}>{t("finance.amount")}</TableHead>
            <TableHead className={cn(head, "pr-5 text-right")}>
              <span className="sr-only">{t("finance.column.actions")}</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {charges.data.map((charge) => (
            <TableRow key={charge.id}>
              <TableCell className="py-3 pl-5">
                <div className="grid min-w-0">
                  <Link href={`/crm/parties/${charge.partyId}`} className="truncate font-medium underline-offset-4 hover:underline">
                    {charge.partyName}
                  </Link>
                  <span className="text-muted-foreground font-mono text-xs">
                    {charge.number} · {formatCalendarDate(charge.date)}
                  </span>
                </div>
              </TableCell>
              <TableCell className="text-muted-foreground hidden max-w-72 truncate md:table-cell">{charge.description}</TableCell>
              <TableCell className="text-right font-medium tabular-nums">{formatMoney(charge.amount, charge.currency)}</TableCell>
              <TableCell className="pr-5 text-right">
                {canWrite ? (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      const invoiceNumber = window.prompt(t("finance.toInvoice.prompt", { number: charge.number }));
                      if (!invoiceNumber?.trim()) return;
                      invoiced.mutate(
                        { id: charge.id, invoiceNumber: invoiceNumber.trim(), invoiceDate: charge.date },
                        { onSuccess: () => toast.success(t("finance.toInvoice.done")), onError: fail },
                      );
                    }}
                  >
                    {t("finance.toInvoice.mark")}
                  </Button>
                ) : null}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

/** Who owes what and when, what came in, and what still needs an invoice from the accountant. */
export function FinancePage() {
  const { t } = useI18n();
  const { data: session } = useSession();
  const canWrite = hasPermission(session?.permissions, permissions.finance.write);
  const [tab, setTab] = useState<Tab>("due");
  const [receiptOpen, setReceiptOpen] = useState(false);
  const [chargeOpen, setChargeOpen] = useState(false);

  const actions = canWrite ? (
    <>
      <Button variant="outline" onClick={() => setChargeOpen(true)}>
        <ReceiptText /> {t("finance.charge.new")}
      </Button>
      <Button onClick={() => setReceiptOpen(true)}>
        <HandCoins /> {t("finance.receipt.new")}
      </Button>
    </>
  ) : null;

  return (
    <div className="grid gap-8">
      <PageHeader title={t("finance.title")} description={t("finance.intro")} actions={actions} />
      <SummaryCards />

      <Card className="py-0">
        <CardContent className="px-0">
          <div className="border-b p-4">
            <div role="tablist" aria-label={t("finance.title")} className="bg-muted inline-flex flex-wrap rounded-lg p-0.5">
              {tabs.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  role="tab"
                  aria-selected={tab === option.value}
                  onClick={() => setTab(option.value)}
                  className={cn(
                    "focus-visible:ring-ring/40 h-8 cursor-pointer rounded-md px-3 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                    tab === option.value ? "bg-card text-foreground font-medium shadow-xs" : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {t(option.label)}
                </button>
              ))}
            </div>
          </div>
          {tab === "due" ? <DueTab /> : tab === "balances" ? <BalancesTab /> : tab === "receipts" ? <ReceiptsTab canWrite={canWrite} /> : <ToInvoiceTab canWrite={canWrite} />}
        </CardContent>
      </Card>

      {receiptOpen ? <ReceiptDialog open onOpenChange={setReceiptOpen} /> : null}
      {chargeOpen ? <ChargeDialog open onOpenChange={setChargeOpen} /> : null}
    </div>
  );
}

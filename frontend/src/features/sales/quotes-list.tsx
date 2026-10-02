"use client";

import { AlertCircle, ChevronLeft, ChevronRight, Eye, FileText, Plus, Search, SearchX } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useSession } from "@/features/identity/session";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatMoney } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { statusBadges } from "./labels";
import { useQuotes, type QuoteListItem } from "./sales-api";

const filters: { value: string; label: TranslationKey }[] = [
  { value: "all", label: "sales.filter.all" },
  { value: "draft", label: "sales.status.draft" },
  { value: "sent", label: "sales.status.sent" },
  { value: "accepted", label: "sales.status.accepted" },
  { value: "rejected", label: "sales.status.rejected" },
  { value: "expired", label: "sales.status.expired" },
];

export function QuoteStatusBadge({ status }: { status: string }) {
  const { t } = useI18n();
  const badge = statusBadges[status] ?? statusBadges.draft!;
  return <Badge variant={badge.variant}>{t(badge.label)}</Badge>;
}

export function QuoteRows({ items }: { items: QuoteListItem[] }) {
  const { t } = useI18n();
  return (
    <Table>
      <TableHeader>
        <TableRow className="bg-muted/60 hover:bg-muted/60">
          <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("sales.column.quote")}</TableHead>
          <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium md:table-cell">{t("sales.column.client")}</TableHead>
          <TableHead className="text-muted-foreground h-10 text-xs font-medium">{t("sales.column.status")}</TableHead>
          <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium lg:table-cell">{t("sales.field.validUntil")}</TableHead>
          <TableHead className="text-muted-foreground h-10 pr-5 text-right text-xs font-medium">{t("sales.column.total")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((quote) => (
          <TableRow key={quote.id} className="hover:bg-muted/40 relative">
            <TableCell className="py-3 pl-5">
              <div className="grid min-w-0">
                <Link href={`/sales/quotes/${quote.id}`} className="truncate font-medium after:absolute after:inset-0 focus-visible:outline-none">
                  {quote.title}
                </Link>
                <span className="text-muted-foreground font-mono text-xs">
                  {quote.number}
                  {quote.revision > 1 ? ` · R${quote.revision}` : ""}
                </span>
              </div>
            </TableCell>
            <TableCell className="text-muted-foreground hidden max-w-56 truncate md:table-cell">{quote.partyName}</TableCell>
            <TableCell>
              <div className="flex items-center gap-1.5">
                <QuoteStatusBadge status={quote.status} />
                {quote.status === "sent" && quote.viewedAt ? (
                  <Eye className="text-muted-foreground size-3.5" aria-label={t("sales.quote.viewed")} />
                ) : null}
              </div>
            </TableCell>
            <TableCell className="text-muted-foreground hidden tabular-nums lg:table-cell">{formatCalendarDate(quote.validUntil)}</TableCell>
            <TableCell className="pr-5 text-right font-medium tabular-nums">{formatMoney(quote.grandTotal, quote.currency)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export function QuotesPage() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const canWrite = hasPermission(session?.permissions, permissions.sales.quotesWrite);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const filterKey = `${status}|${search}`;
  const [paging, setPaging] = useState({ filterKey, page: 1 });
  const page = paging.filterKey === filterKey ? paging.page : 1;
  const quotes = useQuotes({ search, status }, page);

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const newButton = canWrite ? (
    <Link href="/sales/quotes/new" className={buttonVariants()}>
      <Plus /> {t("sales.quote.new")}
    </Link>
  ) : null;
  const pageCount = Math.max(1, Math.ceil((quotes.data?.totalCount ?? 0) / 25));

  return (
    <div className="grid gap-8">
      <PageHeader title={t("sales.quotes.title")} description={t("sales.quotes.description")} actions={newButton} />

      <Card className="py-0">
        <CardContent className="px-0">
          <div className="flex flex-col gap-3 border-b p-4 lg:flex-row lg:items-center lg:justify-between">
            <div className="relative w-full lg:max-w-xs">
              <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
              <Input type="search" className="h-9 pl-9" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={t("sales.quotes.search")} aria-label={t("sales.quotes.search")} />
            </div>
            <div role="tablist" aria-label={t("sales.column.status")} className="bg-muted inline-flex flex-wrap self-start rounded-lg p-0.5">
              {filters.map((filter) => (
                <button
                  key={filter.value}
                  type="button"
                  role="tab"
                  aria-selected={status === filter.value}
                  onClick={() => setStatus(filter.value)}
                  className={cn(
                    "focus-visible:ring-ring/40 h-8 cursor-pointer rounded-md px-3 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                    status === filter.value ? "bg-card text-foreground font-medium shadow-xs" : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {t(filter.label)}
                </button>
              ))}
            </div>
          </div>

          {quotes.isPending ? (
            <div className="grid gap-2 p-5">
              <Skeleton className="h-11 w-full" />
              <Skeleton className="h-11 w-full" />
            </div>
          ) : quotes.isError ? (
            <div className="p-5">
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>
                  {quotes.error instanceof ApiError && quotes.error.status === 403 ? t("sales.noPermission") : tError(quotes.error instanceof ApiError ? quotes.error.code : "common.network")}
                </AlertDescription>
              </Alert>
            </div>
          ) : quotes.data.items.length === 0 ? (
            search || status !== "all" ? (
              <EmptyState icon={SearchX} title={t("sales.quotes.noResults")} />
            ) : (
              <EmptyState icon={FileText} title={t("sales.quotes.empty")} description={t("sales.quotes.emptyHint")} action={newButton} />
            )
          ) : (
            <div className={cn("overflow-x-auto", quotes.isPlaceholderData && "opacity-60")}>
              <QuoteRows items={quotes.data.items} />
            </div>
          )}

          {pageCount > 1 ? (
            <div className="text-muted-foreground flex items-center justify-end gap-2 border-t px-5 py-3 text-sm">
              <span className="tabular-nums">{t("crm.list.page", { page, pageCount })}</span>
              <Button variant="outline" size="icon-sm" disabled={page <= 1} onClick={() => setPaging({ filterKey, page: page - 1 })} aria-label={t("crm.list.previous")}>
                <ChevronLeft />
              </Button>
              <Button variant="outline" size="icon-sm" disabled={page >= pageCount} onClick={() => setPaging({ filterKey, page: page + 1 })} aria-label={t("crm.list.next")}>
                <ChevronRight />
              </Button>
            </div>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}

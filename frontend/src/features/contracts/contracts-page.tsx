"use client";

import { AlertCircle, FileSignature, Plus, Search } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
import { ContractDialog } from "./contract-dialog";
import { monthlyTotals, useContracts, type Contract } from "./contracts-api";

const filters: { value: string; label: TranslationKey }[] = [
  { value: "", label: "sales.filter.all" },
  { value: "active", label: "contracts.status.active" },
  { value: "paused", label: "contracts.status.paused" },
  { value: "ended", label: "contracts.status.ended" },
];

export function ContractStatusBadge({ status }: { status: string }) {
  const { t } = useI18n();
  const label: TranslationKey = status === "active" ? "contracts.status.active" : status === "paused" ? "contracts.status.paused" : "contracts.status.ended";
  return <Badge variant={status === "active" ? "default" : status === "paused" ? "secondary" : "outline"}>{t(label)}</Badge>;
}

export function ContractRows({ items, showParty = true }: { items: Contract[]; showParty?: boolean }) {
  const { t } = useI18n();
  return (
    <Table>
      <TableHeader>
        <TableRow className="bg-muted/60 hover:bg-muted/60">
          <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("contracts.column.contract")}</TableHead>
          {showParty ? <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium md:table-cell">{t("sales.column.client")}</TableHead> : null}
          <TableHead className="text-muted-foreground h-10 text-xs font-medium">{t("sales.column.status")}</TableHead>
          <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium lg:table-cell">{t("contracts.column.nextBilling")}</TableHead>
          <TableHead className="text-muted-foreground h-10 pr-5 text-right text-xs font-medium">{t("contracts.column.monthly")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((contract) => (
          <TableRow key={contract.id} className="hover:bg-muted/40 relative">
            <TableCell className="py-3 pl-5">
              <div className="grid min-w-0">
                <Link href={`/sales/contracts/${contract.id}`} className="truncate font-medium after:absolute after:inset-0 focus-visible:outline-none">
                  {contract.title}
                </Link>
                <span className="text-muted-foreground font-mono text-xs">{contract.number}</span>
              </div>
            </TableCell>
            {showParty ? <TableCell className="text-muted-foreground hidden max-w-56 truncate md:table-cell">{contract.partyName}</TableCell> : null}
            <TableCell>
              <ContractStatusBadge status={contract.status} />
            </TableCell>
            <TableCell className="text-muted-foreground hidden tabular-nums lg:table-cell">{contract.nextBillingDate ? formatCalendarDate(contract.nextBillingDate) : "—"}</TableCell>
            <TableCell className="pr-5 text-right font-medium tabular-nums">{formatMoney(contract.amount, contract.currency)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/** Retainers: what bills every month, the monthly recurring total, and what comes up next. */
export function ContractsPage() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const canWrite = hasPermission(session?.permissions, permissions.sales.contractsWrite);
  const [status, setStatus] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);
  const contracts = useContracts({ status, search });
  const active = useContracts({ status: "active" }, 1, 100);

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const mrr = monthlyTotals(active.data?.items ?? []);
  const newButton = canWrite ? (
    <Button onClick={() => setCreating(true)}>
      <Plus /> {t("contracts.new")}
    </Button>
  ) : null;

  return (
    <div className="grid gap-8">
      <PageHeader title={t("contracts.title")} description={t("contracts.description")} actions={newButton} />

      {mrr.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2">
          <Card>
            <CardContent className="grid gap-1.5">
              <p className="text-muted-foreground text-sm">{t("contracts.mrr")}</p>
              {mrr.map((total) => (
                <p key={total.currency} className="text-2xl font-semibold tabular-nums">
                  {formatMoney(total.amount, total.currency)}
                </p>
              ))}
            </CardContent>
          </Card>
          <Card>
            <CardContent className="grid gap-1.5">
              <p className="text-muted-foreground text-sm">{t("contracts.activeCount")}</p>
              <p className="text-2xl font-semibold tabular-nums">{active.data?.totalCount ?? 0}</p>
            </CardContent>
          </Card>
        </div>
      ) : null}

      <Card className="py-0">
        <CardContent className="px-0">
          <div className="flex flex-col gap-3 border-b p-4 lg:flex-row lg:items-center lg:justify-between">
            <div className="relative w-full lg:max-w-xs">
              <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
              <Input type="search" className="h-9 pl-9" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={t("contracts.search")} aria-label={t("contracts.search")} />
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
          {contracts.isPending ? (
            <div className="grid gap-2 p-5">
              <Skeleton className="h-11 w-full" />
              <Skeleton className="h-11 w-full" />
            </div>
          ) : contracts.isError ? (
            <div className="p-5">
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>
                  {contracts.error instanceof ApiError && contracts.error.status === 403 ? t("contracts.noPermission") : tError(contracts.error instanceof ApiError ? contracts.error.code : "common.network")}
                </AlertDescription>
              </Alert>
            </div>
          ) : contracts.data.items.length === 0 ? (
            <EmptyState icon={FileSignature} title={t(search || status ? "contracts.noResults" : "contracts.empty")} description={search || status ? undefined : t("contracts.emptyHint")} action={search || status ? undefined : newButton} />
          ) : (
            <div className={cn("overflow-x-auto", contracts.isPlaceholderData && "opacity-60")}>
              <ContractRows items={contracts.data.items} />
            </div>
          )}
        </CardContent>
      </Card>

      {creating ? <ContractDialog open onOpenChange={setCreating} /> : null}
    </div>
  );
}

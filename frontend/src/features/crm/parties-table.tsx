"use client";

import { AlertCircle, Building2, ChevronLeft, ChevronRight, Search, SearchX, UserRound, UsersRound } from "lucide-react";
import Link from "next/link";
import { useEffect, useState, type ReactNode } from "react";
import { EmptyState } from "@/components/empty-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ApiError } from "@/lib/api/errors";
import { formatNumber } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { partiesPageSize, useParties, type PartyRoleFilter } from "./parties-api";
import { PartyRoleBadges } from "./party-badges";

const roleFilters: { value: PartyRoleFilter; label: TranslationKey }[] = [
  { value: "all", label: "crm.filter.all" },
  { value: "customer", label: "crm.filter.customers" },
  { value: "supplier", label: "crm.filter.suppliers" },
];

/** Waits until typing pauses, so a search runs once per word and not per keystroke. */
function useDebounced(value: string, delayMs = 250) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

export function PartiesTable({ createAction }: { createAction: ReactNode }) {
  const { t, tError } = useI18n();
  const [searchInput, setSearchInput] = useState("");
  const [role, setRole] = useState<PartyRoleFilter>("all");
  const search = useDebounced(searchInput.trim());

  // The page belongs to the search and filter it was chosen for; a new one starts from page 1.
  const filterKey = `${role}|${search}`;
  const [paging, setPaging] = useState({ filterKey, page: 1 });
  const page = paging.filterKey === filterKey ? paging.page : 1;
  const setPage = (next: number) => setPaging({ filterKey, page: next });

  const parties = useParties(search, role, page);

  const filtered = search.length > 0 || role !== "all";
  const total = parties.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / partiesPageSize));

  let body: ReactNode;
  if (parties.isPending) {
    body = (
      <div className="grid gap-2 p-5">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} className="h-11 w-full" />
        ))}
      </div>
    );
  } else if (parties.isError) {
    const error = parties.error;
    body = (
      <div className="p-5">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {error instanceof ApiError && error.status === 403
              ? t("crm.noPermission")
              : tError(error instanceof ApiError ? error.code : "common.network")}
          </AlertDescription>
        </Alert>
      </div>
    );
  } else if (parties.data.items.length === 0) {
    body = filtered ? (
      <EmptyState
        icon={SearchX}
        title={t("crm.list.noResults")}
        description={t("crm.list.noResultsHint")}
        action={
          <Button
            variant="outline"
            onClick={() => {
              setSearchInput("");
              setRole("all");
            }}
          >
            {t("crm.list.clearFilters")}
          </Button>
        }
      />
    ) : (
      <EmptyState icon={UsersRound} title={t("crm.list.empty")} description={t("crm.list.emptyHint")} action={createAction} />
    );
  } else {
    body = (
      <div className="overflow-x-auto">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/60 hover:bg-muted/60">
              <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("crm.column.name")}</TableHead>
              <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium md:table-cell">{t("crm.column.roles")}</TableHead>
              <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium lg:table-cell">{t("crm.column.taxNumber")}</TableHead>
              <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium lg:table-cell">{t("crm.column.contact")}</TableHead>
              <TableHead className="text-muted-foreground hidden h-10 pr-5 text-xs font-medium sm:table-cell">{t("crm.column.city")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody className={cn(parties.isPlaceholderData && "opacity-60 transition-opacity")}>
            {parties.data.items.map((party) => {
              const Icon = party.kind === "person" ? UserRound : Building2;
              return (
                <TableRow key={party.id} className="hover:bg-muted/40 relative">
                  <TableCell className="py-3 pl-5">
                    <div className="flex items-center gap-3">
                      <span className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-lg">
                        <Icon className="size-4" aria-hidden />
                      </span>
                      <div className="grid min-w-0">
                        {/* The link covers the row, so the whole row is one target (and one tab stop). */}
                        <Link
                          href={`/crm/parties/${party.id}`}
                          className="focus-visible:ring-ring/40 truncate font-medium after:absolute after:inset-0 focus-visible:rounded focus-visible:ring-2 focus-visible:outline-none"
                        >
                          {party.name}
                        </Link>
                        <span className="text-muted-foreground font-mono text-xs">{party.code}</span>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <PartyRoleBadges isCustomer={party.isCustomer} isSupplier={party.isSupplier} />
                  </TableCell>
                  <TableCell className="tabular text-muted-foreground hidden lg:table-cell">{party.taxNumber ?? "—"}</TableCell>
                  <TableCell className="text-muted-foreground hidden max-w-56 truncate lg:table-cell">
                    {party.email ?? party.phone ?? "—"}
                  </TableCell>
                  <TableCell className="text-muted-foreground hidden pr-5 sm:table-cell">{party.city ?? "—"}</TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    );
  }

  return (
    <div className="grid">
      <div className="flex flex-col gap-3 border-b p-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="relative w-full sm:max-w-sm">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
          <Input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder={t("crm.list.searchPlaceholder")}
            aria-label={t("crm.list.search")}
            className="h-9 pl-9"
          />
        </div>
        <div role="tablist" aria-label={t("crm.list.roleFilter")} className="bg-muted inline-flex self-start rounded-lg p-0.5">
          {roleFilters.map((filter) => (
            <button
              key={filter.value}
              type="button"
              role="tab"
              aria-selected={role === filter.value}
              onClick={() => setRole(filter.value)}
              className={cn(
                "focus-visible:ring-ring/40 h-8 cursor-pointer rounded-md px-3 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                role === filter.value ? "bg-card text-foreground font-medium shadow-xs" : "text-muted-foreground hover:text-foreground",
              )}
            >
              {t(filter.label)}
            </button>
          ))}
        </div>
      </div>

      {body}

      {parties.data && total > 0 ? (
        <div className="text-muted-foreground flex items-center justify-between gap-3 border-t px-5 py-3 text-sm">
          <span className="tabular">{t("crm.list.count", { count: formatNumber(total) })}</span>
          {pageCount > 1 ? (
            <div className="flex items-center gap-2">
              <span className="tabular">{t("crm.list.page", { page, pageCount })}</span>
              <Button
                variant="outline"
                size="icon-sm"
                onClick={() => setPage(page - 1)}
                disabled={page <= 1}
                aria-label={t("crm.list.previous")}
              >
                <ChevronLeft />
              </Button>
              <Button
                variant="outline"
                size="icon-sm"
                onClick={() => setPage(page + 1)}
                disabled={page >= pageCount}
                aria-label={t("crm.list.next")}
              >
                <ChevronRight />
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

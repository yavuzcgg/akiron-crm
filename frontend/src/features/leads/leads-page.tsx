"use client";

import { AlertCircle, Globe, Plus, Search, Target } from "lucide-react";
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
import { formatDate, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { stageLabels } from "./labels";
import { LeadBoard } from "./lead-board";
import { LeadDialog } from "./lead-dialog";
import { pipelineTotals, useLeads } from "./leads-api";
import { WebFormDialog } from "./web-form-dialog";

function ClosedList({ search }: { search: string }) {
  const { t } = useI18n();
  const leads = useLeads("closed", { search });
  if (leads.isPending) return <Skeleton className="h-40 w-full" />;
  if (leads.isError || leads.data.length === 0) return <EmptyState icon={Target} title={t("leads.closed.empty")} />;
  return (
    <Card className="py-0">
      <CardContent className="overflow-x-auto px-0">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/60 hover:bg-muted/60">
              <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("leads.field.title")}</TableHead>
              <TableHead className="text-muted-foreground h-10 text-xs font-medium">{t("leads.column.result")}</TableHead>
              <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium md:table-cell">{t("leads.column.closedAt")}</TableHead>
              <TableHead className="text-muted-foreground h-10 pr-5 text-right text-xs font-medium">{t("leads.field.value")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {leads.data.map((lead) => (
              <TableRow key={lead.id} className="hover:bg-muted/40 relative">
                <TableCell className="py-3 pl-5">
                  <Link href={`/crm/leads/${lead.id}`} className="font-medium after:absolute after:inset-0 focus-visible:outline-none">
                    {lead.title}
                  </Link>
                  <p className="text-muted-foreground text-xs">{lead.partyName ?? lead.companyName ?? lead.contactName}</p>
                </TableCell>
                <TableCell>
                  <Badge variant={lead.stage === "won" ? "default" : "outline"}>{t(stageLabels[lead.stage]!)}</Badge>
                  {lead.lostReason ? <p className="text-muted-foreground mt-1 max-w-64 truncate text-xs">{lead.lostReason}</p> : null}
                </TableCell>
                <TableCell className="text-muted-foreground hidden tabular-nums md:table-cell">{lead.closedAt ? formatDate(lead.closedAt) : "—"}</TableCell>
                <TableCell className="pr-5 text-right tabular-nums">{lead.estimatedValue ? formatMoney(lead.estimatedValue, lead.currency) : "—"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

/** The sales funnel: who might buy, where each stands, what the pipeline is worth. */
export function LeadsPage() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const canWrite = hasPermission(session?.permissions, permissions.crm.leadsWrite);
  const [view, setView] = useState<"open" | "closed">("open");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [mine, setMine] = useState(false);
  const [adding, setAdding] = useState(false);
  const [webForm, setWebForm] = useState(false);
  const leads = useLeads("open", { search, ownerId: mine ? session?.userId : undefined });

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const totals = pipelineTotals(leads.data ?? []);
  const actions = canWrite ? (
    <>
      <Button variant="outline" onClick={() => setWebForm(true)}>
        <Globe /> {t("leads.webForm.button")}
      </Button>
      <Button onClick={() => setAdding(true)}>
        <Plus /> {t("leads.new")}
      </Button>
    </>
  ) : null;

  return (
    <div className="grid gap-6">
      <PageHeader title={t("leads.title")} description={t("leads.description")} actions={actions} />

      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap items-center gap-3">
          <div role="tablist" aria-label={t("leads.title")} className="bg-muted inline-flex rounded-lg p-0.5">
            {(["open", "closed"] as const).map((option) => (
              <button
                key={option}
                type="button"
                role="tab"
                aria-selected={view === option}
                onClick={() => setView(option)}
                className={cn(
                  "focus-visible:ring-ring/40 h-8 cursor-pointer rounded-md px-3 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                  view === option ? "bg-card text-foreground font-medium shadow-xs" : "text-muted-foreground hover:text-foreground",
                )}
              >
                {t(option === "open" ? "leads.view.open" : "leads.view.closed")}
              </button>
            ))}
          </div>
          {view === "open" ? (
            <label className="flex cursor-pointer items-center gap-2 text-sm">
              <input type="checkbox" className="accent-primary size-4" checked={mine} onChange={(event) => setMine(event.target.checked)} />
              {t("leads.onlyMine")}
            </label>
          ) : null}
          {view === "open" && totals.length > 0 ? (
            <span className="text-muted-foreground text-sm">
              {t("leads.pipeline")}: <span className="text-foreground font-medium tabular-nums">{totals.map((total) => formatMoney(total.amount, total.currency)).join(" · ")}</span>
            </span>
          ) : null}
        </div>
        <div className="relative w-full lg:max-w-xs">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
          <Input type="search" className="h-9 pl-9" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={t("leads.search")} aria-label={t("leads.search")} />
        </div>
      </div>

      {view === "closed" ? (
        <ClosedList search={search} />
      ) : leads.isPending ? (
        <div className="flex gap-4 overflow-hidden">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-80 w-[272px] shrink-0 rounded-xl" />
          ))}
        </div>
      ) : leads.isError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {leads.error instanceof ApiError && leads.error.status === 403 ? t("leads.noPermission") : tError(leads.error instanceof ApiError ? leads.error.code : "common.network")}
          </AlertDescription>
        </Alert>
      ) : leads.data.length === 0 && !search && !mine ? (
        <Card>
          <EmptyState
            icon={Target}
            title={t("leads.empty")}
            description={t("leads.emptyHint")}
            action={
              canWrite ? (
                <Button onClick={() => setAdding(true)}>
                  <Plus /> {t("leads.new")}
                </Button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <LeadBoard leads={leads.data} updatedAt={leads.dataUpdatedAt} canWrite={canWrite} onAdd={() => setAdding(true)} />
      )}

      {adding ? <LeadDialog open onOpenChange={setAdding} defaultOwnerId={session?.userId} /> : null}
      {webForm ? <WebFormDialog open onOpenChange={setWebForm} /> : null}
    </div>
  );
}

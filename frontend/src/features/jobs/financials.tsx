"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Info } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatDuration, formatMoney, formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { jobsKey, type WorkOrder } from "./jobs-api";

/** "12.500,50" or "12500.5" → 12500.5; null for empty, NaN for nonsense. */
export function parseAmount(value: string): number | null {
  const text = value.trim().replace(/\s|₺/g, "");
  if (!text) return null;
  // A comma is the decimal mark; dots in groups of three ("7.000") are thousands, as Turkish writes them.
  const normalised = text.includes(",") || /^\d{1,3}(\.\d{3})+$/.test(text) ? text.replace(/\./g, "").replace(",", ".") : text;
  return /^\d+(\.\d{1,2})?$/.test(normalised) ? Number(normalised) : Number.NaN;
}

/** Budget, cost of logged time and what is left; only for people with jobs.financials. */
export function Financials({ workOrder }: { workOrder: WorkOrder }) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const financials = workOrder.financials!;
  const [budget, setBudget] = useState(financials.budget === null ? "" : formatNumber(financials.budget));
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: async (value: number | null) =>
      unwrap(await api.PUT("/api/v1/jobs/work-orders/{id}/budget", { params: { path: { id: workOrder.id } }, body: { budget: value } })),
    onSuccess: (updated) => {
      queryClient.setQueryData([...jobsKey, "work-order", workOrder.id], updated);
      toast.success(t("jobs.financials.saved"));
    },
    onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
  });

  const profit = financials.profit;
  const losing = profit !== null && profit < 0;

  return (
    <div className="grid gap-4">
      <form
        className="grid gap-1.5"
        onSubmit={(event) => {
          event.preventDefault();
          const value = parseAmount(budget);
          if (value !== null && Number.isNaN(value)) {
            setError(t("jobs.financials.budgetInvalid"));
            return;
          }
          setError(null);
          save.mutate(value);
        }}
      >
        <Label htmlFor="wo-budget" className="text-[13px] font-medium">
          {t("jobs.financials.budget")}
        </Label>
        <div className="flex gap-2">
          <Input
            id="wo-budget"
            inputMode="decimal"
            className="h-9 tabular-nums"
            placeholder="0,00"
            value={budget}
            onChange={(event) => setBudget(event.target.value)}
            aria-invalid={!!error}
            aria-describedby={error ? "wo-budget-error" : "wo-budget-hint"}
          />
          <Button type="submit" variant="secondary" size="sm" className="h-9" disabled={save.isPending}>
            {t("common.save")}
          </Button>
        </div>
        {error ? (
          <p id="wo-budget-error" role="alert" className="text-destructive text-[13px]">
            {error}
          </p>
        ) : (
          <p id="wo-budget-hint" className="text-muted-foreground text-[13px]">
            {t("jobs.financials.budgetHint")}
          </p>
        )}
      </form>

      <dl className="grid grid-cols-2 gap-3">
        <div className="bg-muted/50 rounded-lg p-3">
          <dt className="text-muted-foreground text-xs">{t("jobs.financials.cost")}</dt>
          <dd className="text-base font-semibold tabular-nums">{formatMoney(financials.cost)}</dd>
        </div>
        <div className={cn("rounded-lg p-3", profit === null ? "bg-muted/50" : losing ? "bg-destructive/10" : "bg-success-soft")}>
          <dt className="text-muted-foreground text-xs">{t("jobs.financials.profit")}</dt>
          <dd className={cn("text-base font-semibold tabular-nums", profit === null ? "" : losing ? "text-destructive" : "text-success")}>
            {profit === null ? "—" : formatMoney(profit)}
            {financials.marginPercent !== null ? <span className="ml-1 text-xs font-medium">(%{formatNumber(financials.marginPercent)})</span> : null}
          </dd>
        </div>
      </dl>

      {financials.uncostedMinutes > 0 ? (
        <p className="text-muted-foreground flex gap-2 text-[13px]">
          <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          {t("jobs.financials.uncosted", { duration: formatDuration(financials.uncostedMinutes) })}
        </p>
      ) : null}
    </div>
  );
}

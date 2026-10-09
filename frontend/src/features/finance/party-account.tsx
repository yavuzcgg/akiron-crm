"use client";

import { HandCoins, Mail, ReceiptText, Wallet, XCircle } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useFinanceMutations, useStatement, type OpenItemRow } from "./finance-api";
import { ChargeDialog, DueLabel, ReceiptDialog, RemindDialog } from "./money-dialogs";

/** A party's account on its CRM page: balance per currency, what is due, and the statement newest first. */
export function PartyAccount({ partyId, partyName, canWrite }: { partyId: string; partyName: string; canWrite: boolean }) {
  const { t, tError } = useI18n();
  const statement = useStatement(partyId);
  const { cancelCharge } = useFinanceMutations();
  const [dialog, setDialog] = useState<"receipt" | "charge" | null>(null);
  const [showAll, setShowAll] = useState(false);
  const [reminding, setReminding] = useState<OpenItemRow | null>(null);
  const party = { id: partyId, name: partyName };

  const actions = canWrite ? (
    <div className="flex flex-wrap gap-2">
      <Button size="sm" onClick={() => setDialog("receipt")}>
        <HandCoins /> {t("finance.receipt.new")}
      </Button>
      <Button size="sm" variant="outline" onClick={() => setDialog("charge")}>
        <ReceiptText /> {t("finance.charge.new")}
      </Button>
    </div>
  ) : null;

  const dialogs = (
    <>
      {dialog === "receipt" ? <ReceiptDialog open party={party} onOpenChange={(open) => setDialog(open ? "receipt" : null)} /> : null}
      {dialog === "charge" ? <ChargeDialog open party={party} onOpenChange={(open) => setDialog(open ? "charge" : null)} /> : null}
      {reminding ? <RemindDialog item={reminding} onOpenChange={(open) => !open && setReminding(null)} /> : null}
    </>
  );

  if (statement.isPending) return <Skeleton className="h-24 w-full" />;
  if (statement.isError) return null;
  const data = statement.data;
  if (data.lines.length === 0) {
    return (
      <>
        <EmptyState icon={Wallet} title={t("finance.party.empty")} className="py-6" action={actions} />
        {dialogs}
      </>
    );
  }

  const lines = showAll ? data.lines : data.lines.slice(0, 8);
  const cancel = (chargeId: string, number: string) => {
    const reason = window.prompt(t("finance.charge.cancelPrompt", { number }));
    if (!reason?.trim()) return;
    cancelCharge.mutate(
      { id: chargeId, reason: reason.trim() },
      {
        onSuccess: () => toast.success(t("finance.charge.cancelled")),
        onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
      },
    );
  };

  return (
    <div className="grid gap-5">
      <div className="flex flex-wrap gap-6">
        {data.balances.map((balance) => (
          <div key={balance.currency} className="grid gap-0.5">
            <span className="text-muted-foreground text-xs">{t(balance.balance < 0 ? "finance.party.credit" : "finance.party.balance")}</span>
            <span className={cn("text-xl font-semibold tabular-nums", balance.balance < 0 && "text-success")}>{formatMoney(Math.abs(balance.balance), balance.currency)}</span>
            {balance.overdue > 0 ? <span className="text-destructive text-xs font-medium tabular-nums">{t("finance.party.overdue", { amount: formatMoney(balance.overdue, balance.currency) })}</span> : null}
          </div>
        ))}
      </div>

      {data.openItems.length > 0 ? (
        <div className="grid gap-2">
          <h3 className="text-sm font-medium">{t("finance.party.openItems")}</h3>
          <ul className="divide-y rounded-lg border">
            {data.openItems.map((item) => (
              <li key={item.id} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                <div className="grid min-w-0">
                  <span className="truncate">{item.description}</span>
                  <span className="text-xs tabular-nums">
                    <span className="text-muted-foreground font-mono">
                      {item.chargeNumber}
                      {item.instalmentCount > 1 ? ` · ${t("finance.instalmentOf", { number: item.sequence, count: item.instalmentCount })}` : ""} ·{" "}
                    </span>
                    <DueLabel dueDate={item.dueDate} overdue={item.isOverdue} days={item.daysOverdue} />
                  </span>
                </div>
                <div className="flex shrink-0 items-center gap-1">
                  <span className="font-medium tabular-nums">{formatMoney(item.remaining, item.currency)}</span>
                  {canWrite ? (
                    <Button variant="ghost" size="icon-sm" aria-label={t("finance.remind.title")} title={t("finance.remind.title")} onClick={() => setReminding(item)}>
                      <Mail />
                    </Button>
                  ) : null}
                  {canWrite && item.remaining === item.amount && item.sequence === 1 ? (
                    <Button variant="ghost" size="icon-sm" aria-label={t("finance.charge.cancel")} title={t("finance.charge.cancel")} onClick={() => cancel(item.chargeId, item.chargeNumber)}>
                      <XCircle />
                    </Button>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {data.advances.length > 0 ? (
        <p className="bg-success-soft text-success rounded-lg px-3 py-2 text-[13px]">
          {t("finance.party.advances", { list: data.advances.map((advance) => `${advance.number} ${formatMoney(advance.unallocated, advance.currency)}`).join(", ") })}
        </p>
      ) : null}

      <div className="grid gap-2">
        <h3 className="text-sm font-medium">{t("finance.party.statement")}</h3>
        <div className="-mx-6 overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-muted/60 text-muted-foreground text-xs">
                <th className="h-9 pl-6 text-left font-medium">{t("finance.date")}</th>
                <th className="h-9 text-left font-medium">{t("finance.description")}</th>
                <th className="h-9 text-right font-medium">{t("finance.debit")}</th>
                <th className="h-9 text-right font-medium">{t("finance.credit")}</th>
                <th className="h-9 pr-6 text-right font-medium">{t("finance.column.balance")}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {lines.map((line) => (
                <tr key={line.id} className={cn(line.isReversal && "text-muted-foreground")}>
                  <td className="py-2 pl-6 whitespace-nowrap tabular-nums">{formatCalendarDate(line.date)}</td>
                  <td className="max-w-64 py-2">
                    <span className="flex items-center gap-1.5">
                      <span className="text-muted-foreground font-mono text-xs">{line.documentNumber}</span>
                      {line.isReversal ? <Badge variant="outline">{t("finance.party.reversal")}</Badge> : null}
                    </span>
                    <span className="block truncate">{line.description}</span>
                  </td>
                  <td className="py-2 text-right tabular-nums">{line.debit > 0 ? formatMoney(line.debit, line.currency) : ""}</td>
                  <td className="py-2 text-right tabular-nums">{line.credit > 0 ? formatMoney(line.credit, line.currency) : ""}</td>
                  <td className="py-2 pr-6 text-right font-medium tabular-nums">{formatMoney(line.balance, line.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {data.lines.length > 8 && !showAll ? (
          <Button variant="ghost" size="sm" className="justify-self-start" onClick={() => setShowAll(true)}>
            {t("finance.party.showAll", { count: data.lines.length })}
          </Button>
        ) : null}
      </div>

      {actions}
      {dialogs}
    </div>
  );
}

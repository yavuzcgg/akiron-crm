"use client";

import { AlertCircle } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { selectClass } from "@/features/sales/catalog";
import { currencies } from "@/features/sales/labels";
import { PartyPicker, type PickedParty } from "@/features/sales/party-picker";
import { parseDecimal } from "@/features/sales/quote-math";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatCalendarDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { useContractMutations, type Contract } from "./contracts-api";

interface ContractDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  contract?: Contract;
  party?: PickedParty;
  /** Starting from an accepted quote: its client and title are taken over. */
  quote?: { id: string; title: string; partyId: string; partyName: string; currency: string };
  onSaved?: (contract: Contract) => void;
}

const termOptions = [0, 3, 6, 12, 24] as const;

function addMonths(date: string, months: number): string {
  const [year, month, day] = date.split("-").map(Number) as [number, number, number];
  const lastDay = new Date(year, month - 1 + months + 1, 0).getDate();
  const result = new Date(year, month - 1 + months, Math.min(day, lastDay));
  return dayKey(result);
}

/** Start or change a retainer: what it costs monthly, from when, for how long, and when the client pays. */
export function ContractDialog({ open, onOpenChange, contract, party: fixedParty, quote, onSaved }: ContractDialogProps) {
  const { t, tError } = useI18n();
  const { create, update } = useContractMutations();
  const editing = !!contract;
  const [party, setParty] = useState<PickedParty | null>(fixedParty ?? (quote ? { id: quote.partyId, name: quote.partyName } : null));
  const [title, setTitle] = useState(contract?.title ?? quote?.title ?? "");
  const [amount, setAmount] = useState(contract ? String(contract.amount).replace(".", ",") : "");
  const [currency, setCurrency] = useState(contract?.currency ?? quote?.currency ?? "TRY");
  const [startDate, setStartDate] = useState(contract?.startDate ?? dayKey(new Date()));
  const [term, setTerm] = useState<number>(0);
  const [endDate, setEndDate] = useState(contract?.endDate ?? "");
  const [autoRenew, setAutoRenew] = useState(contract?.autoRenew ?? true);
  const [noticeDays, setNoticeDays] = useState(String(contract?.noticeDays ?? 30));
  const [paymentTermDays, setPaymentTermDays] = useState(String(contract?.paymentTermDays ?? 0));
  const [firstBillingDate, setFirstBillingDate] = useState("");
  const [scope, setScope] = useState(contract?.scope ?? "");
  const [error, setError] = useState<string | null>(null);

  const effectiveEnd = editing ? endDate : term > 0 ? addMonths(startDate, term) : endDate;
  const pending = create.isPending || update.isPending;

  const submit = async () => {
    const value = parseDecimal(amount);
    if (!editing && !party) {
      setError(t("sales.quote.clientRequired"));
      return;
    }
    if (!title.trim()) {
      setError(t("contracts.titleRequired"));
      return;
    }
    if (Number.isNaN(value) || value <= 0) {
      setError(t("finance.amountInvalid"));
      return;
    }
    setError(null);
    const common = {
      title: title.trim(),
      amount: value,
      endDate: effectiveEnd || null,
      autoRenew: !!effectiveEnd && autoRenew,
      noticeDays: Number(noticeDays) || 0,
      paymentTermDays: Number(paymentTermDays) || 0,
      scope: scope.trim() || null,
    };
    try {
      const saved = editing
        ? await update.mutateAsync({ id: contract.id, ...common })
        : await create.mutateAsync({
            ...common,
            partyId: quote ? null : party!.id,
            quoteId: quote?.id ?? null,
            currency,
            startDate,
            firstBillingDate: firstBillingDate || null,
          });
      toast.success(editing ? t("contracts.saved") : t("contracts.created", { number: saved.number, count: saved.billedPeriods }));
      onSaved?.(saved);
      onOpenChange(false);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  const startsInPast = !editing && startDate < dayKey(new Date());

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t(editing ? "contracts.edit" : quote ? "contracts.fromQuote" : "contracts.new")}</DialogTitle>
          <DialogDescription>{t("contracts.dialogHint")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          {error ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}
          {editing || fixedParty || quote ? null : <PartyPicker id="contract-party" value={party} onChange={setParty} />}
          <FormField id="contract-title" label={t("contracts.field.title")} placeholder={t("contracts.field.titlePlaceholder")} value={title} onChange={(event) => setTitle(event.target.value)} maxLength={200} />
          <div className="grid items-start gap-3 sm:grid-cols-[1fr_120px]">
            <FormField id="contract-amount" inputMode="decimal" label={t("contracts.field.amount")} hint={t("contracts.field.amountHint")} placeholder="0,00" value={amount} onChange={(event) => setAmount(event.target.value)} />
            <div className="grid gap-1.5">
              <Label htmlFor="contract-currency" className="text-[13px] font-medium">
                {t("sales.field.currency")}
              </Label>
              <select id="contract-currency" className={selectClass} value={currency} onChange={(event) => setCurrency(event.target.value)} disabled={editing}>
                {currencies.map((code) => (
                  <option key={code}>{code}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField id="contract-start" type="date" label={t("contracts.field.start")} value={startDate} onChange={(event) => setStartDate(event.target.value)} disabled={editing} />
            {editing ? (
              <FormField id="contract-end" type="date" label={t("contracts.field.end")} hint={t("contracts.field.endHint")} value={endDate} onChange={(event) => setEndDate(event.target.value)} />
            ) : (
              <div className="grid gap-1.5">
                <Label htmlFor="contract-term" className="text-[13px] font-medium">
                  {t("contracts.field.term")}
                </Label>
                <select id="contract-term" className={selectClass} value={term} onChange={(event) => setTerm(Number(event.target.value))}>
                  {termOptions.map((option) => (
                    <option key={option} value={option}>
                      {option === 0 ? t("contracts.term.open") : t("contracts.term.months", { count: option })}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>
          {effectiveEnd ? (
            <div className="grid gap-3 rounded-lg border p-3 sm:grid-cols-2">
              <label className="flex cursor-pointer items-center gap-2 text-sm sm:col-span-2">
                <input type="checkbox" className="accent-primary size-4" checked={autoRenew} onChange={(event) => setAutoRenew(event.target.checked)} />
                {t("contracts.field.autoRenew")}
              </label>
              <FormField id="contract-notice" type="number" min={0} max={365} label={t("contracts.field.notice")} value={noticeDays} onChange={(event) => setNoticeDays(event.target.value)} />
              {!editing ? <p className="text-muted-foreground self-end pb-2 text-xs">{t("contracts.endsOn", { date: formatCalendarDate(effectiveEnd) })}</p> : null}
            </div>
          ) : null}
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField id="contract-payment-term" type="number" min={0} max={180} label={t("contracts.field.paymentTerm")} hint={t("contracts.field.paymentTermHint")} value={paymentTermDays} onChange={(event) => setPaymentTermDays(event.target.value)} />
            {startsInPast ? (
              <FormField id="contract-first-billing" type="date" label={t("contracts.field.firstBilling")} hint={t("contracts.field.firstBillingHint")} value={firstBillingDate} onChange={(event) => setFirstBillingDate(event.target.value)} />
            ) : null}
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="contract-scope" className="text-[13px] font-medium">
              {t("contracts.field.scope")}
            </Label>
            <Textarea id="contract-scope" rows={3} placeholder={t("contracts.field.scopePlaceholder")} value={scope} onChange={(event) => setScope(event.target.value)} maxLength={4000} />
          </div>
          {!editing ? <p className="text-muted-foreground text-[13px]">{t("contracts.billingHint")}</p> : null}
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={pending}>
              {pending ? t("common.saving") : t(editing ? "common.save" : "contracts.start")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

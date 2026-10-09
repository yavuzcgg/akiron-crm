"use client";

import { AlertCircle } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { selectClass } from "@/features/sales/catalog";
import { currencies } from "@/features/sales/labels";
import { PartyPicker, type PickedParty } from "@/features/sales/party-picker";
import { parseDecimal } from "@/features/sales/quote-math";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatCalendarDate, formatMoney } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { paymentMethods, splitMonthly, useFinanceMutations, useOpenItems } from "./finance-api";

export const methodLabels: Record<string, TranslationKey> = {
  transfer: "finance.method.transfer",
  cash: "finance.method.cash",
  card: "finance.method.card",
  check: "finance.method.check",
  note: "finance.method.note",
  other: "finance.method.other",
};

function ErrorAlert({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <Alert variant="destructive">
      <AlertCircle />
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}

/** Money received: party, amount, how and where; allocated to the oldest due items automatically. */
export function ReceiptDialog({ open, onOpenChange, party: fixedParty }: { open: boolean; onOpenChange: (open: boolean) => void; party?: PickedParty }) {
  const { t, tError } = useI18n();
  const { receive } = useFinanceMutations();
  const [party, setParty] = useState<PickedParty | null>(fixedParty ?? null);
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("TRY");
  const [rate, setRate] = useState("");
  const [date, setDate] = useState(dayKey(new Date()));
  const [method, setMethod] = useState("transfer");
  const [account, setAccount] = useState("");
  const [reference, setReference] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const open_ = useOpenItems({ partyId: party?.id });
  const dueSameCurrency = (party ? (open_.data ?? []) : []).filter((item) => item.currency === currency);
  const outstanding = dueSameCurrency.reduce((sum, item) => sum + item.remaining, 0);

  const submit = async () => {
    const value = parseDecimal(amount);
    const exchangeRate = currency !== "TRY" && rate.trim() ? parseDecimal(rate) : null;
    if (!party || Number.isNaN(value) || value <= 0 || (exchangeRate !== null && Number.isNaN(exchangeRate))) {
      setError(t(party ? "finance.amountInvalid" : "sales.quote.clientRequired"));
      return;
    }
    setError(null);
    try {
      const receipt = await receive.mutateAsync({
        partyId: party.id,
        amount: value,
        currency,
        exchangeRate,
        date,
        method,
        account: account.trim() || null,
        reference: reference.trim() || null,
        note: note.trim() || null,
        autoAllocate: true,
      });
      toast.success(
        receipt.unallocated > 0
          ? t("finance.receipt.savedWithAdvance", { number: receipt.number, advance: formatMoney(receipt.unallocated, receipt.currency) })
          : t("finance.receipt.saved", { number: receipt.number }),
      );
      onOpenChange(false);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t("finance.receipt.new")}</DialogTitle>
          <DialogDescription>{t("finance.receipt.newHint")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <ErrorAlert message={error} />
          {fixedParty ? null : <PartyPicker id="receipt-party" value={party} onChange={setParty} />}
          {party && outstanding > 0 ? (
            <p className="bg-muted/60 rounded-lg px-3 py-2 text-[13px]">
              {t("finance.receipt.outstanding", { amount: formatMoney(outstanding, currency), count: dueSameCurrency.length })}
            </p>
          ) : null}
          <div className="grid items-start gap-3 sm:grid-cols-[1fr_120px]">
            <FormField id="receipt-amount" inputMode="decimal" label={t("finance.amount")} value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="0,00" />
            <div className="grid gap-1.5">
              <Label htmlFor="receipt-currency" className="text-[13px] font-medium">
                {t("sales.field.currency")}
              </Label>
              <select id="receipt-currency" className={selectClass} value={currency} onChange={(event) => setCurrency(event.target.value)}>
                {currencies.map((code) => (
                  <option key={code}>{code}</option>
                ))}
              </select>
            </div>
          </div>
          {currency !== "TRY" ? <FormField id="receipt-rate" inputMode="decimal" label={t("sales.field.rate")} hint={t("sales.field.rateHint")} value={rate} onChange={(event) => setRate(event.target.value)} /> : null}
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField id="receipt-date" type="date" label={t("finance.date")} value={date} onChange={(event) => setDate(event.target.value)} />
            <div className="grid gap-1.5">
              <Label htmlFor="receipt-method" className="text-[13px] font-medium">
                {t("finance.method")}
              </Label>
              <select id="receipt-method" className={selectClass} value={method} onChange={(event) => setMethod(event.target.value)}>
                {paymentMethods.map((option) => (
                  <option key={option} value={option}>
                    {t(methodLabels[option]!)}
                  </option>
                ))}
              </select>
            </div>
            <FormField id="receipt-account" label={t("finance.account")} placeholder={t("finance.accountPlaceholder")} value={account} onChange={(event) => setAccount(event.target.value)} maxLength={100} />
            <FormField id="receipt-reference" label={t("finance.reference")} value={reference} onChange={(event) => setReference(event.target.value)} maxLength={100} />
          </div>
          <FormField id="receipt-note" label={t("finance.note")} value={note} onChange={(event) => setNote(event.target.value)} maxLength={500} />
          <p className="text-muted-foreground text-[13px]">{t("finance.receipt.allocationHint")}</p>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={receive.isPending}>
              {receive.isPending ? t("common.saving") : t("finance.receipt.save")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

interface ChargeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  party?: PickedParty;
  /** Charging an accepted quote: its total, currency and client are fixed. */
  quote?: { id: string; number: string; title: string; total: number; currency: string };
}

/** What a client owes, with a payment plan: one payment or equal monthly instalments (each date and amount editable). */
export function ChargeDialog({ open, onOpenChange, party: fixedParty, quote }: ChargeDialogProps) {
  const { t, tError } = useI18n();
  const { charge } = useFinanceMutations();
  const [party, setParty] = useState<PickedParty | null>(fixedParty ?? null);
  const [description, setDescription] = useState(quote ? `${quote.number} ${quote.title}` : "");
  const [amount, setAmount] = useState(quote ? String(quote.total).replace(".", ",") : "");
  const [currency, setCurrency] = useState(quote?.currency ?? "TRY");
  const [date, setDate] = useState(dayKey(new Date()));
  const [count, setCount] = useState(1);
  const [plan, setPlan] = useState<{ dueDate: string; amount: string }[] | null>(null);
  const [kind, setKind] = useState("sale");
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [error, setError] = useState<string | null>(null);

  const total = parseDecimal(amount);
  const generated = useMemo(
    () => (Number.isNaN(total) || total <= 0 ? [] : splitMonthly(total, count, date).map((item) => ({ dueDate: item.dueDate, amount: String(item.amount).replace(".", ",") }))),
    [total, count, date],
  );
  const instalments = plan ?? generated;
  const planTotal = instalments.reduce((sum, item) => sum + (parseDecimal(item.amount) || 0), 0);
  const planMatches = Math.abs(planTotal - total) < 0.005;

  const submit = async () => {
    if (!quote && !party) {
      setError(t("sales.quote.clientRequired"));
      return;
    }
    if (Number.isNaN(total) || total <= 0 || (!quote && !description.trim())) {
      setError(t("finance.amountInvalid"));
      return;
    }
    if (!planMatches) {
      setError(t("finance.charge.planMismatch"));
      return;
    }
    setError(null);
    try {
      const saved = await charge.mutateAsync({
        partyId: quote ? null : party!.id,
        quoteId: quote?.id ?? null,
        description: description.trim() || null,
        amount: total,
        currency,
        date,
        kind,
        invoiceNumber: invoiceNumber.trim() || null,
        instalments: instalments.map((item) => ({ dueDate: item.dueDate, amount: parseDecimal(item.amount) })),
      });
      toast.success(t("finance.charge.saved", { number: saved.number }));
      onOpenChange(false);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  const editPlan = (index: number, patch: Partial<{ dueDate: string; amount: string }>) =>
    setPlan(instalments.map((item, position) => (position === index ? { ...item, ...patch } : item)));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t(quote ? "finance.charge.fromQuote" : "finance.charge.new")}</DialogTitle>
          <DialogDescription>{t("finance.charge.newHint")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <ErrorAlert message={error} />
          {fixedParty || quote ? null : <PartyPicker id="charge-party" value={party} onChange={setParty} />}
          <FormField id="charge-description" label={t("finance.description")} value={description} onChange={(event) => setDescription(event.target.value)} maxLength={300} placeholder={t("finance.charge.descriptionPlaceholder")} />
          <div className="grid items-start gap-3 sm:grid-cols-[1fr_120px]">
            <FormField
              id="charge-amount"
              inputMode="decimal"
              label={t("finance.amount")}
              value={amount}
              onChange={(event) => {
                setAmount(event.target.value);
                setPlan(null);
              }}
              disabled={!!quote}
              hint={quote ? t("finance.charge.quoteTotalHint") : undefined}
            />
            <div className="grid gap-1.5">
              <Label htmlFor="charge-currency" className="text-[13px] font-medium">
                {t("sales.field.currency")}
              </Label>
              <select id="charge-currency" className={selectClass} value={currency} onChange={(event) => setCurrency(event.target.value)} disabled={!!quote}>
                {currencies.map((code) => (
                  <option key={code}>{code}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField
              id="charge-date"
              type="date"
              label={t("finance.charge.firstDue")}
              value={date}
              onChange={(event) => {
                setDate(event.target.value);
                setPlan(null);
              }}
            />
            <div className="grid gap-1.5">
              <Label htmlFor="charge-count" className="text-[13px] font-medium">
                {t("finance.charge.instalments")}
              </Label>
              <select
                id="charge-count"
                className={selectClass}
                value={count}
                onChange={(event) => {
                  setCount(Number(event.target.value));
                  setPlan(null);
                }}
              >
                {[1, 2, 3, 4, 6, 9, 12].map((option) => (
                  <option key={option} value={option}>
                    {option === 1 ? t("finance.charge.single") : t("finance.charge.monthly", { count: option })}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {instalments.length > 1 ? (
            <ul className="grid gap-2 rounded-lg border p-3">
              {instalments.map((item, index) => (
                <li key={index} className="grid grid-cols-[24px_1fr_1fr] items-center gap-2 text-sm">
                  <span className="text-muted-foreground tabular-nums">{index + 1}.</span>
                  <Input type="date" className="h-8" value={item.dueDate} onChange={(event) => editPlan(index, { dueDate: event.target.value })} aria-label={t("finance.charge.dueOf", { number: index + 1 })} />
                  <Input inputMode="decimal" className="h-8 tabular-nums" value={item.amount} onChange={(event) => editPlan(index, { amount: event.target.value })} aria-label={t("finance.charge.amountOf", { number: index + 1 })} />
                </li>
              ))}
              <li className={planMatches ? "text-muted-foreground text-right text-xs tabular-nums" : "text-destructive text-right text-xs tabular-nums"}>
                {t("finance.charge.planTotal", { total: formatMoney(planTotal, currency) })}
              </li>
            </ul>
          ) : null}

          {quote ? null : (
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label htmlFor="charge-kind" className="text-[13px] font-medium">
                  {t("finance.charge.kind")}
                </Label>
                <select id="charge-kind" className={selectClass} value={kind} onChange={(event) => setKind(event.target.value)}>
                  <option value="sale">{t("finance.charge.kind.sale")}</option>
                  <option value="opening">{t("finance.charge.kind.opening")}</option>
                </select>
              </div>
              <FormField id="charge-invoice" label={t("finance.charge.invoiceNumber")} hint={t("finance.charge.invoiceHint")} value={invoiceNumber} onChange={(event) => setInvoiceNumber(event.target.value)} maxLength={50} />
            </div>
          )}
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={charge.isPending}>
              {charge.isPending ? t("common.saving") : t("finance.charge.save")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export function DueLabel({ dueDate, overdue, days }: { dueDate: string; overdue: boolean; days: number }) {
  const { t } = useI18n();
  return overdue ? (
    <span className="text-destructive font-medium">{t("finance.overdueDays", { date: formatCalendarDate(dueDate), days })}</span>
  ) : (
    <span className="text-muted-foreground">{formatCalendarDate(dueDate)}</span>
  );
}

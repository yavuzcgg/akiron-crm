"use client";

import { AlertCircle, ArrowLeft, FileDown, Plus, Save, Send, Trash2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { selectClass } from "./catalog";
import { currencies, unitLabels, units, vatRates, withholdingOptions } from "./labels";
import { PartyPicker, type PickedParty } from "./party-picker";
import { lineAmounts, parseDecimal, quoteTotals } from "./quote-math";
import { QuoteStatusBadge } from "./quotes-list";
import { useCatalog, useQuoteMutations, type Quote } from "./sales-api";

interface LineDraft {
  key: string;
  catalogItemId: string | null;
  name: string;
  description: string;
  quantity: string;
  unit: string;
  unitPrice: string;
  discountPercent: string;
  vatRate: number;
  withholdingTenths: number;
}

let lineKey = 0;
const newKey = () => `line-${++lineKey}`;

const blankLine = (): LineDraft => ({
  key: newKey(),
  catalogItemId: null,
  name: "",
  description: "",
  quantity: "1",
  unit: "piece",
  unitPrice: "",
  discountPercent: "",
  vatRate: 20,
  withholdingTenths: 0,
});

const decimalText = (value: number) => String(value).replace(".", ",");

function today(): string {
  const date = new Date();
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function plusDays(day: string, days: number): string {
  const date = new Date(`${day}T00:00:00`);
  date.setDate(date.getDate() + days);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

/** The draft editor: client, dates, currency, lines with live totals. Saving keeps it a draft; sending freezes it. */
/** <paramref name="onSend"/> opens the send dialog, which lives above the editor so it survives the switch to the sent view. */
export function QuoteEditor({ quote, initialParty, onSend: requestSend }: { quote?: Quote; initialParty?: PickedParty; onSend?: () => void }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { save, remove } = useQuoteMutations(quote?.id);
  const catalog = useCatalog();
  const [party, setParty] = useState<PickedParty | null>(quote ? { id: quote.partyId, name: quote.partyName } : (initialParty ?? null));
  const [title, setTitle] = useState(quote?.title ?? "");
  const [recipientName, setRecipientName] = useState(quote?.recipientName ?? "");
  const [recipientEmail, setRecipientEmail] = useState(quote?.recipientEmail ?? "");
  const [currency, setCurrency] = useState(quote?.currency ?? "TRY");
  const [rate, setRate] = useState(quote?.exchangeRate ? decimalText(quote.exchangeRate) : "");
  const [issueDate, setIssueDate] = useState(quote?.issueDate ?? today());
  const [validUntil, setValidUntil] = useState(quote?.validUntil ?? plusDays(today(), 30));
  const [notes, setNotes] = useState(quote?.notes ?? "");
  const [lines, setLines] = useState<LineDraft[]>(() =>
    quote && quote.lines.length > 0
      ? quote.lines.map((line) => ({
          key: newKey(),
          catalogItemId: line.catalogItemId,
          name: line.name,
          description: line.description ?? "",
          quantity: decimalText(line.quantity),
          unit: line.unit,
          unitPrice: decimalText(line.unitPrice),
          discountPercent: line.discountPercent ? decimalText(line.discountPercent) : "",
          vatRate: line.vatRate,
          withholdingTenths: line.withholdingTenths,
        }))
      : [blankLine()],
  );
  const [error, setError] = useState<string | null>(null);

  const parsed = useMemo(
    () =>
      lines.map((line) => ({
        quantity: parseDecimal(line.quantity),
        unitPrice: parseDecimal(line.unitPrice),
        discountPercent: line.discountPercent.trim() ? parseDecimal(line.discountPercent) : 0,
        vatRate: line.vatRate,
        withholdingTenths: line.withholdingTenths,
      })),
    [lines],
  );
  const valid = parsed.map((line) => !Number.isNaN(line.quantity) && line.quantity > 0 && !Number.isNaN(line.unitPrice) && line.unitPrice >= 0 && !Number.isNaN(line.discountPercent));
  const totals = quoteTotals(parsed.filter((_, index) => valid[index]));
  const money = (value: number) => formatMoney(value, currency);

  const update = (key: string, patch: Partial<LineDraft>) => setLines((current) => current.map((line) => (line.key === key ? { ...line, ...patch } : line)));

  const addFromCatalog = (itemId: string) => {
    const item = catalog.data?.find((candidate) => candidate.id === itemId);
    if (!item) return;
    const line: LineDraft = {
      key: newKey(),
      catalogItemId: item.id,
      name: item.name,
      description: item.description ?? "",
      quantity: "1",
      unit: item.unit,
      unitPrice: decimalText(item.netUnitPrice),
      discountPercent: "",
      vatRate: item.vatRate,
      withholdingTenths: item.withholdingTenths,
    };
    setLines((current) => (current.length === 1 && !current[0]!.name && !current[0]!.unitPrice ? [line] : [...current, line]));
  };

  const submit = async (): Promise<Quote | null> => {
    if (!party) {
      setError(t("sales.quote.clientRequired"));
      return null;
    }
    const filled = lines.map((line, index) => ({ line, index })).filter(({ line }) => line.name.trim() || line.unitPrice.trim());
    if (!title.trim() || filled.some(({ line, index }) => !line.name.trim() || !valid[index])) {
      setError(t("sales.quote.fixLines"));
      return null;
    }
    const exchangeRate = currency !== "TRY" && rate.trim() ? parseDecimal(rate) : null;
    if (exchangeRate !== null && (Number.isNaN(exchangeRate) || exchangeRate <= 0)) {
      setError(t("sales.quote.rateInvalid"));
      return null;
    }

    setError(null);
    try {
      const saved = await save.mutateAsync({
        partyId: party.id,
        title: title.trim(),
        recipientName: recipientName.trim() || null,
        recipientEmail: recipientEmail.trim() || null,
        currency,
        exchangeRate,
        issueDate,
        validUntil,
        notes: notes.trim() || null,
        lines: filled.map(({ line, index }) => ({
          catalogItemId: line.catalogItemId,
          name: line.name.trim(),
          description: line.description.trim() || null,
          quantity: parsed[index]!.quantity,
          unit: line.unit,
          unitPrice: parsed[index]!.unitPrice,
          discountPercent: parsed[index]!.discountPercent,
          vatRate: line.vatRate,
          withholdingTenths: line.withholdingTenths,
          priceIncludesVat: false,
        })),
      });
      return saved;
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network", failure instanceof ApiError ? failure.params : undefined));
      return null;
    }
  };

  const onSave = async () => {
    const saved = await submit();
    if (!saved) return;
    toast.success(t("sales.quote.saved", { number: saved.number }));
    if (!quote) router.replace(`/sales/quotes/${saved.id}`);
  };

  const onSend = async () => {
    const saved = await submit();
    if (!saved) return;
    if (!quote) {
      router.replace(`/sales/quotes/${saved.id}?send=1`);
      return;
    }
    requestSend?.();
  };

  /** Picking a client fills the recipient from its primary contact, when it has one. */
  const pickParty = async (picked: PickedParty | null) => {
    setParty(picked);
    if (!picked || recipientEmail) return;
    try {
      const detail = unwrap(await api.GET("/api/v1/crm/parties/{id}", { params: { path: { id: picked.id } } }));
      const contact = detail.contacts.find((candidate) => candidate.isPrimary) ?? detail.contacts[0];
      if (contact) {
        setRecipientName(contact.fullName);
        setRecipientEmail(contact.email ?? detail.email ?? "");
      } else if (detail.email) {
        setRecipientEmail(detail.email);
      }
    } catch {
      // The recipient is optional; typing it by hand still works.
    }
  };

  return (
    <div className="grid gap-6">
      <Link href="/sales/quotes" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
        <ArrowLeft /> {t("sales.quotes.title")}
      </Link>

      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="grid gap-1">
          <div className="flex items-center gap-2">
            {quote ? <span className="text-muted-foreground font-mono text-xs">{quote.number}{quote.revision > 1 ? ` · R${quote.revision}` : ""}</span> : null}
            <QuoteStatusBadge status="draft" />
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{quote ? quote.title : t("sales.quote.new")}</h1>
        </div>
        <div className="flex flex-wrap gap-2">
          {quote ? (
            <>
              <Button
                variant="ghost"
                onClick={() => {
                  if (!window.confirm(t("sales.quote.deleteConfirm"))) return;
                  remove.mutate(undefined, {
                    onSuccess: () => router.replace("/sales/quotes"),
                    onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
                  });
                }}
                disabled={remove.isPending || quote.revision > 1}
              >
                <Trash2 /> {t("sales.quote.delete")}
              </Button>
              <a href={`/api/v1/sales/quotes/${quote.id}/pdf`} target="_blank" rel="noopener" className={buttonVariants({ variant: "outline" })}>
                <FileDown /> PDF
              </a>
            </>
          ) : null}
          <Button variant="outline" onClick={() => void onSave()} disabled={save.isPending}>
            <Save /> {save.isPending ? t("common.saving") : t("sales.quote.saveDraft")}
          </Button>
          <Button onClick={() => void onSend()} disabled={save.isPending}>
            <Send /> {t("sales.quote.send")}
          </Button>
        </div>
      </div>

      {error ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="grid content-start gap-6">
          <Card>
            <CardContent className="grid gap-4">
              <div className="grid items-start gap-4 sm:grid-cols-2">
                <PartyPicker id="quote-party" value={party} onChange={(picked) => void pickParty(picked)} />
                <FormField id="quote-title" label={t("sales.field.title")} value={title} onChange={(event) => setTitle(event.target.value)} maxLength={200} />
                <FormField id="quote-recipient" label={t("sales.field.recipientName")} value={recipientName} onChange={(event) => setRecipientName(event.target.value)} maxLength={200} />
                <FormField id="quote-email" type="email" label={t("sales.field.recipientEmail")} hint={t("sales.field.recipientEmailHint")} value={recipientEmail} onChange={(event) => setRecipientEmail(event.target.value)} maxLength={254} />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between gap-3">
              <CardTitle>{t("sales.quote.lines")}</CardTitle>
              {(catalog.data?.length ?? 0) > 0 ? (
                <select
                  className={`${selectClass} h-9 w-auto max-w-64`}
                  value=""
                  onChange={(event) => addFromCatalog(event.target.value)}
                  aria-label={t("sales.quote.fromCatalog")}
                >
                  <option value="">{t("sales.quote.fromCatalog")}</option>
                  {catalog.data!.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              ) : null}
            </CardHeader>
            <CardContent className="grid gap-3">
              {lines.map((line, index) => {
                const amounts = valid[index] ? lineAmounts(parsed[index]!) : null;
                return (
                  <fieldset key={line.key} className="bg-muted/30 grid gap-3 rounded-lg border p-3" aria-label={t("sales.quote.line", { number: index + 1 })}>
                    <div className="flex gap-2">
                      <Input className="h-9 flex-1 font-medium" value={line.name} onChange={(event) => update(line.key, { name: event.target.value })} placeholder={t("sales.quote.lineName")} aria-label={t("sales.quote.lineName")} maxLength={300} />
                      <Button type="button" variant="ghost" size="icon-sm" className="mt-0.5" onClick={() => setLines((current) => (current.length > 1 ? current.filter((candidate) => candidate.key !== line.key) : [blankLine()]))} aria-label={t("sales.quote.removeLine", { number: index + 1 })}>
                        <Trash2 />
                      </Button>
                    </div>
                    <Input className="h-8 text-[13px]" value={line.description} onChange={(event) => update(line.key, { description: event.target.value })} placeholder={t("sales.quote.lineDescription")} aria-label={t("sales.quote.lineDescription")} maxLength={2000} />
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-6">
                      <LineField label={t("sales.field.quantity")}>
                        <Input className="h-9 tabular-nums" inputMode="decimal" value={line.quantity} onChange={(event) => update(line.key, { quantity: event.target.value })} />
                      </LineField>
                      <LineField label={t("sales.field.unit")}>
                        <select className={`${selectClass} h-9`} value={line.unit} onChange={(event) => update(line.key, { unit: event.target.value })}>
                          {units.map((option) => (
                            <option key={option} value={option}>
                              {t(unitLabels[option]!)}
                            </option>
                          ))}
                        </select>
                      </LineField>
                      <LineField label={t("sales.field.unitPrice")}>
                        <Input className="h-9 tabular-nums" inputMode="decimal" value={line.unitPrice} onChange={(event) => update(line.key, { unitPrice: event.target.value })} placeholder="0,00" />
                      </LineField>
                      <LineField label={t("sales.field.discount")}>
                        <Input className="h-9 tabular-nums" inputMode="decimal" value={line.discountPercent} onChange={(event) => update(line.key, { discountPercent: event.target.value })} placeholder="%" />
                      </LineField>
                      <LineField label={t("sales.field.vatRate")}>
                        <select className={`${selectClass} h-9`} value={line.vatRate} onChange={(event) => update(line.key, { vatRate: Number(event.target.value) })}>
                          {vatRates.map((vat) => (
                            <option key={vat} value={vat}>
                              %{vat}
                            </option>
                          ))}
                        </select>
                      </LineField>
                      <LineField label={t("sales.field.withholding")}>
                        <select className={`${selectClass} h-9`} value={line.withholdingTenths} onChange={(event) => update(line.key, { withholdingTenths: Number(event.target.value) })}>
                          {withholdingOptions.map((tenths) => (
                            <option key={tenths} value={tenths}>
                              {tenths === 0 ? t("sales.withholding.none") : `${tenths}/10`}
                            </option>
                          ))}
                        </select>
                      </LineField>
                    </div>
                    <p className="text-muted-foreground text-right text-[13px] tabular-nums">
                      {amounts ? (
                        <>
                          {t("sales.quote.lineNet")}: <span className="text-foreground font-medium">{money(amounts.net)}</span>
                        </>
                      ) : line.unitPrice || line.name ? (
                        <span className="text-destructive">{t("sales.quote.lineInvalid")}</span>
                      ) : null}
                    </p>
                  </fieldset>
                );
              })}
              <Button type="button" variant="outline" size="sm" className="justify-self-start" onClick={() => setLines((current) => [...current, blankLine()])}>
                <Plus /> {t("sales.quote.addLine")}
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="grid gap-1.5">
              <Label htmlFor="quote-notes" className="text-[13px] font-medium">
                {t("sales.field.notes")}
              </Label>
              <Textarea id="quote-notes" rows={4} maxLength={4000} value={notes} onChange={(event) => setNotes(event.target.value)} placeholder={t("sales.field.notesHint")} />
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6 lg:sticky lg:top-20">
          <Card>
            <CardContent className="grid gap-4">
              <div className="grid grid-cols-2 gap-3">
                <FormField id="quote-issue" type="date" label={t("sales.field.issueDate")} value={issueDate} onChange={(event) => setIssueDate(event.target.value)} />
                <FormField id="quote-valid" type="date" label={t("sales.field.validUntil")} value={validUntil} min={issueDate} onChange={(event) => setValidUntil(event.target.value)} />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="grid gap-1.5">
                  <Label htmlFor="quote-currency" className="text-[13px] font-medium">
                    {t("sales.field.currency")}
                  </Label>
                  <select id="quote-currency" className={selectClass} value={currency} onChange={(event) => setCurrency(event.target.value)}>
                    {currencies.map((code) => (
                      <option key={code}>{code}</option>
                    ))}
                  </select>
                </div>
                {currency !== "TRY" ? (
                  <FormField id="quote-rate" inputMode="decimal" label={t("sales.field.rate")} hint={t("sales.field.rateHint")} value={rate} onChange={(event) => setRate(event.target.value)} placeholder="TCMB" />
                ) : null}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="grid gap-2 text-sm">
              {totals.discount > 0 ? (
                <>
                  <TotalRow label={t("sales.totals.gross")} value={money(totals.gross)} />
                  <TotalRow label={t("sales.totals.discount")} value={money(-totals.discount)} />
                </>
              ) : null}
              <TotalRow label={t("sales.totals.net")} value={money(totals.net)} />
              <TotalRow label={t("sales.totals.vat")} value={money(totals.vat)} />
              {totals.withholding > 0 ? <TotalRow label={t("sales.totals.withholding")} value={money(-totals.withholding)} /> : null}
              <div className="border-primary mt-1 border-t pt-2">
                <TotalRow label={t("sales.totals.grand")} value={money(totals.total)} strong />
              </div>
              {quote?.exchangeRate ? (
                <p className="text-muted-foreground text-xs tabular-nums">
                  {t("sales.totals.rateNote", { rate: decimalText(quote.exchangeRate), currency: quote.currency, net: formatMoney(quote.totals.netTry) })}
                </p>
              ) : null}
            </CardContent>
          </Card>
        </div>
      </div>

    </div>
  );
}

function LineField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-1">
      <span className="text-muted-foreground text-[11px] font-medium">{label}</span>
      {children}
    </label>
  );
}

function TotalRow({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={strong ? "flex items-baseline justify-between text-base font-semibold" : "flex items-baseline justify-between"}>
      <span className={strong ? undefined : "text-muted-foreground"}>{label}</span>
      <span className="tabular-nums">{value}</span>
    </div>
  );
}

export { TotalRow };

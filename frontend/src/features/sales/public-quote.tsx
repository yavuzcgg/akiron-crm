"use client";

import { AlertCircle, CircleCheck, CircleX, FileDown, Languages } from "lucide-react";
import { useState } from "react";
import { LogoMark } from "@/components/brand/logo";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatDateTime, formatMoney, formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { unitLabels } from "./labels";
import { TotalRow } from "./quote-editor";
import { useDecideQuote, usePublicQuote } from "./sales-api";

/**
 * What the client sees when they open the quote link: the document, a PDF, and accept or decline.
 * No account; the link is the key. Plain and calm: this page represents the agency, not Akiron.
 */
export function PublicQuote({ token }: { token: string }) {
  const { t, tError, locale, setLocale } = useI18n();
  const quote = usePublicQuote(token);
  const decide = useDecideQuote(token);
  const [mode, setMode] = useState<"accept" | "reject" | null>(null);
  const [name, setName] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const languageToggle = (
    <Button variant="ghost" size="sm" onClick={() => setLocale(locale === "tr" ? "en" : "tr")}>
      <Languages /> {locale === "tr" ? "English" : "Türkçe"}
    </Button>
  );

  if (quote.isPending) {
    return (
      <div className="mx-auto grid max-w-3xl gap-4 p-6">
        <Skeleton className="h-10 w-60" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  if (quote.isError) {
    return (
      <div className="mx-auto grid max-w-xl gap-6 p-6 pt-24">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{tError(quote.error instanceof ApiError ? quote.error.code : "common.network")}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const data = quote.data;
  const money = (value: number) => formatMoney(value, data.currency);
  const open = data.status === "sent";

  return (
    <div className="bg-muted/40 min-h-svh">
      <div className="mx-auto grid max-w-3xl gap-6 px-4 py-8 md:py-12">
        <div className="flex items-center justify-between gap-3">
          <p className="text-lg font-semibold">{data.workspaceName}</p>
          {languageToggle}
        </div>

        <Card>
          <CardContent className="grid gap-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div className="grid gap-1">
                <p className="text-muted-foreground text-xs font-semibold tracking-wider uppercase">{t("sales.public.quoteFor")}</p>
                <p className="font-semibold">{data.partyName}</p>
                {data.recipientName ? <p className="text-muted-foreground text-sm">{data.recipientName}</p> : null}
              </div>
              <div className="grid gap-0.5 text-sm sm:text-right">
                <p className="text-primary text-xl font-bold tracking-tight">{data.number}</p>
                <p className="text-muted-foreground">
                  {t("sales.field.issueDate")}: {formatCalendarDate(data.issueDate)}
                </p>
                <p className="text-muted-foreground">
                  {t("sales.field.validUntil")}: {formatCalendarDate(data.validUntil)}
                </p>
              </div>
            </div>

            <h1 className="text-xl font-semibold tracking-tight">{data.title}</h1>

            <ul className="divide-border divide-y border-y">
              {data.lines.map((line, index) => (
                <li key={index} className="flex items-start justify-between gap-4 py-3 text-sm">
                  <div className="grid min-w-0 gap-0.5">
                    <span className="font-medium">{line.name}</span>
                    {line.description ? <span className="text-muted-foreground text-xs">{line.description}</span> : null}
                    <span className="text-muted-foreground text-xs tabular-nums">
                      {formatNumber(line.quantity)} {t(unitLabels[line.unit] ?? "sales.unit.piece")} × {money(line.unitPrice)}
                      {line.discountPercent > 0 ? ` · -%${formatNumber(line.discountPercent)}` : ""} · KDV %{line.vatRate}
                    </span>
                  </div>
                  <span className="shrink-0 font-medium tabular-nums">{money(line.net)}</span>
                </li>
              ))}
            </ul>

            <div className="grid gap-2 text-sm sm:ml-auto sm:w-72">
              {data.totals.discount > 0 ? (
                <>
                  <TotalRow label={t("sales.totals.gross")} value={money(data.totals.gross)} />
                  <TotalRow label={t("sales.totals.discount")} value={money(-data.totals.discount)} />
                </>
              ) : null}
              <TotalRow label={t("sales.totals.net")} value={money(data.totals.net)} />
              <TotalRow label={t("sales.totals.vat")} value={money(data.totals.vat)} />
              {data.totals.withholding > 0 ? <TotalRow label={t("sales.totals.withholding")} value={money(-data.totals.withholding)} /> : null}
              <div className="border-primary mt-1 border-t pt-2">
                <TotalRow label={t("sales.totals.grand")} value={money(data.totals.grand)} strong />
              </div>
            </div>

            {data.notes ? (
              <div className="bg-muted/50 rounded-lg p-4 text-sm">
                <p className="mb-1 font-medium">{t("sales.field.notes")}</p>
                <p className="whitespace-pre-wrap">{data.notes}</p>
              </div>
            ) : null}

            <a href={`/api/v1/sales/public/quotes/${token}/pdf?lang=${locale}`} target="_blank" rel="noopener" className={buttonVariants({ variant: "outline", className: "justify-self-start" })}>
              <FileDown /> {t("sales.public.downloadPdf")}
            </a>
          </CardContent>
        </Card>

        {data.status === "accepted" || data.status === "rejected" ? (
          <Alert variant={data.status === "accepted" ? "default" : "destructive"}>
            {data.status === "accepted" ? <CircleCheck /> : <CircleX />}
            <AlertDescription>
              {t(data.status === "accepted" ? "sales.public.accepted" : "sales.public.rejected", {
                name: data.decidedByName ?? "",
                date: data.decidedAt ? formatDateTime(data.decidedAt) : "",
              })}
            </AlertDescription>
          </Alert>
        ) : data.status === "expired" ? (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{t("sales.public.expired", { workspace: data.workspaceName })}</AlertDescription>
          </Alert>
        ) : open && mode === null ? (
          <div className="flex flex-col gap-3 sm:flex-row sm:justify-end">
            <Button variant="outline" size="lg" onClick={() => setMode("reject")}>
              <CircleX /> {t("sales.public.reject")}
            </Button>
            <Button size="lg" onClick={() => setMode("accept")}>
              <CircleCheck /> {t("sales.public.accept")}
            </Button>
          </div>
        ) : open ? (
          <Card>
            <CardContent>
              <form
                className="grid gap-4"
                onSubmit={async (event) => {
                  event.preventDefault();
                  if (!name.trim()) {
                    setError(t("sales.public.nameRequired"));
                    return;
                  }
                  setError(null);
                  try {
                    await decide.mutateAsync({ accept: mode === "accept", name: name.trim(), note: note.trim() });
                    setMode(null);
                  } catch (failure) {
                    setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
                  }
                }}
              >
                <p className="font-semibold">{t(mode === "accept" ? "sales.public.acceptTitle" : "sales.public.rejectTitle")}</p>
                {error ? (
                  <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>{error}</AlertDescription>
                  </Alert>
                ) : null}
                <div className="grid gap-1.5">
                  <Label htmlFor="decider-name" className="text-[13px] font-medium">
                    {t("sales.public.yourName")}
                  </Label>
                  <Input id="decider-name" className="h-10" value={name} onChange={(event) => setName(event.target.value)} maxLength={200} autoComplete="name" />
                </div>
                <div className="grid gap-1.5">
                  <Label htmlFor="decider-note" className="text-[13px] font-medium">
                    {t(mode === "accept" ? "sales.public.noteOptional" : "sales.public.reason")}
                  </Label>
                  <Textarea id="decider-note" rows={3} maxLength={2000} value={note} onChange={(event) => setNote(event.target.value)} />
                </div>
                {mode === "accept" ? <p className="text-muted-foreground text-[13px]">{t("sales.public.acceptHint", { total: money(data.totals.grand) })}</p> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="outline" onClick={() => setMode(null)}>
                    {t("common.cancel")}
                  </Button>
                  <Button type="submit" variant={mode === "accept" ? "default" : "destructive"} disabled={decide.isPending}>
                    {t(mode === "accept" ? "sales.public.confirmAccept" : "sales.public.confirmReject")}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        ) : null}

        <div className="text-muted-foreground flex items-center justify-center gap-2 text-xs">
          <span>{t("sales.public.poweredBy")}</span>
          <LogoMark className="size-4" />
          <span className="font-semibold">Akiron</span>
        </div>
      </div>
    </div>
  );
}

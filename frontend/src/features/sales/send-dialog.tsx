"use client";

import { Check, Copy, MessageCircle, Send } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { useQuoteMutations, type Quote, type SendResult } from "./sales-api";

/**
 * Sends a draft: e-mails the link to the recipient (when there is one) and shows the link once,
 * to copy or share on WhatsApp by hand until the WhatsApp integration exists.
 */
export function SendQuoteDialog({ quote, open, onOpenChange }: { quote: Quote; open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t, tError } = useI18n();
  const { send } = useQuoteMutations(quote.id);
  const [email, setEmail] = useState(!!quote.recipientEmail);
  const [result, setResult] = useState<SendResult | null>(null);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const whatsapp = result
    ? `https://wa.me/?text=${encodeURIComponent(t("sales.send.whatsappText", { title: quote.title, link: result.link }))}`
    : "";

  return (
    <Dialog
      // Only a draft can be sent; afterwards the dialog stays open just to show the link.
      open={open && (quote.status === "draft" || result !== null)}
      onOpenChange={(next) => {
        onOpenChange(next);
        if (!next) {
          setResult(null);
          setError(null);
        }
      }}
    >
      <DialogContent className="sm:max-w-md" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t(result ? "sales.send.doneTitle" : "sales.send.title")}</DialogTitle>
          <DialogDescription>{t(result ? "sales.send.doneDescription" : "sales.send.description")}</DialogDescription>
        </DialogHeader>

        {result ? (
          <div className="grid gap-4">
            {result.emailed ? (
              <Alert>
                <Check />
                <AlertDescription>{t("sales.send.emailed", { email: quote.recipientEmail ?? "" })}</AlertDescription>
              </Alert>
            ) : null}
            <div className="flex gap-2">
              <Input readOnly value={result.link} className="h-9 font-mono text-xs" aria-label={t("sales.send.link")} onFocus={(event) => event.target.select()} />
              <Button
                variant="outline"
                size="sm"
                className="h-9"
                onClick={async () => {
                  await navigator.clipboard.writeText(result.link);
                  setCopied(true);
                  toast.success(t("sales.send.copied"));
                }}
              >
                {copied ? <Check /> : <Copy />} {t("sales.send.copy")}
              </Button>
            </div>
            <a href={whatsapp} target="_blank" rel="noopener noreferrer" className={buttonVariants({ variant: "outline" })}>
              <MessageCircle /> {t("sales.send.whatsapp")}
            </a>
            <p className="text-muted-foreground text-[13px]">{t("sales.send.linkOnce")}</p>
          </div>
        ) : (
          <div className="grid gap-4">
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            ) : null}
            <label className="flex cursor-pointer items-start gap-2 text-sm">
              <input type="checkbox" className="accent-primary mt-0.5 size-4" checked={email} disabled={!quote.recipientEmail} onChange={(event) => setEmail(event.target.checked)} />
              <span>
                {quote.recipientEmail ? t("sales.send.emailTo", { email: quote.recipientEmail }) : t("sales.send.noEmail")}
              </span>
            </label>
            <p className="text-muted-foreground text-[13px]">{t("sales.send.freezeHint")}</p>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                {t("common.cancel")}
              </Button>
              <Button
                disabled={send.isPending}
                onClick={() =>
                  send.mutate(email, {
                    onSuccess: setResult,
                    onError: (failure) => setError(tError(failure instanceof ApiError ? failure.code : "common.network")),
                  })
                }
              >
                <Send /> {send.isPending ? t("common.loading") : t("sales.send.submit")}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

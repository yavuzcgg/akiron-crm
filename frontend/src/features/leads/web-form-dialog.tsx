"use client";

import { Copy, RefreshCw } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { useAssignableMembers } from "@/features/jobs/jobs-api";
import { selectClass } from "@/features/sales/catalog";
import { ApiError } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { embedSnippet, useLeadForm, useLeadFormMutations, type LeadFormSettings } from "./leads-api";

function Settings({ form, onDone }: { form: LeadFormSettings; onDone: () => void }) {
  const { t, tError } = useI18n();
  const { save, rotate } = useLeadFormMutations();
  const members = useAssignableMembers();
  const [enabled, setEnabled] = useState(form.configured ? form.enabled : true);
  const [redirectUrl, setRedirectUrl] = useState(form.redirectUrl ?? "");
  const [ownerId, setOwnerId] = useState(form.ownerUserId ?? "");
  const [origin] = useState(() => window.location.origin);
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  const snippet = form.key
    ? embedSnippet(origin, form.key, {
        name: t("leads.webForm.placeholder.name"),
        email: t("leads.webForm.placeholder.email"),
        phone: t("leads.webForm.placeholder.phone"),
        company: t("leads.webForm.placeholder.company"),
        message: t("leads.webForm.placeholder.message"),
        send: t("leads.webForm.placeholder.send"),
      })
    : null;

  return (
    <div className="grid gap-4">
      <label className="flex cursor-pointer items-center gap-2 text-sm">
        <input type="checkbox" className="accent-primary size-4" checked={enabled} onChange={(event) => setEnabled(event.target.checked)} />
        {t("leads.webForm.enabled")}
      </label>
      <FormField id="form-redirect" label={t("leads.webForm.redirect")} hint={t("leads.webForm.redirectHint")} placeholder="https://" value={redirectUrl} onChange={(event) => setRedirectUrl(event.target.value)} maxLength={500} />
      <div className="grid gap-1.5">
        <Label htmlFor="form-owner" className="text-[13px] font-medium">
          {t("leads.webForm.owner")}
        </Label>
        <select id="form-owner" className={selectClass} value={ownerId} onChange={(event) => setOwnerId(event.target.value)}>
          <option value="">{t("leads.webForm.everyone")}</option>
          {(members.data ?? []).map((member) => (
            <option key={member.userId} value={member.userId}>
              {member.fullName}
            </option>
          ))}
        </select>
      </div>
      {snippet ? (
        <div className="grid gap-1.5">
          <Label htmlFor="form-snippet" className="text-[13px] font-medium">
            {t("leads.webForm.snippet")}
          </Label>
          <Textarea id="form-snippet" readOnly rows={9} className="font-mono text-xs" value={snippet} onFocus={(event) => event.target.select()} />
          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                void navigator.clipboard.writeText(snippet).then(() => toast.success(t("leads.webForm.copied")));
              }}
            >
              <Copy /> {t("leads.webForm.copy")}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                if (window.confirm(t("leads.webForm.rotateConfirm"))) rotate.mutate(undefined, { onError: fail, onSuccess: () => toast.success(t("leads.webForm.rotated")) });
              }}
            >
              <RefreshCw /> {t("leads.webForm.rotate")}
            </Button>
          </div>
          <p className="text-muted-foreground text-xs">{t("leads.webForm.snippetHint")}</p>
        </div>
      ) : (
        <p className="text-muted-foreground text-[13px]">{t("leads.webForm.firstSave")}</p>
      )}
      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={onDone}>
          {t("common.close")}
        </Button>
        <Button
          disabled={save.isPending}
          onClick={() =>
            save.mutate(
              { enabled, redirectUrl: redirectUrl.trim() || null, ownerUserId: ownerId || null },
              { onSuccess: () => toast.success(t("leads.webForm.saved")), onError: fail },
            )
          }
        >
          {save.isPending ? t("common.saving") : t("common.save")}
        </Button>
      </div>
    </div>
  );
}

/** The web form: on/off, where visitors land, who gets the leads, and the HTML to paste into the site. */
export function WebFormDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useI18n();
  const form = useLeadForm(open);
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t("leads.webForm.title")}</DialogTitle>
          <DialogDescription>{t("leads.webForm.description")}</DialogDescription>
        </DialogHeader>
        {form.data ? <Settings key={form.data.key ?? "new"} form={form.data} onDone={() => onOpenChange(false)} /> : <Skeleton className="h-60 w-full" />}
      </DialogContent>
    </Dialog>
  );
}

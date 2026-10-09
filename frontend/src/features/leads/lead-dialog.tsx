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
import { useAssignableMembers } from "@/features/jobs/jobs-api";
import { selectClass } from "@/features/sales/catalog";
import { currencies } from "@/features/sales/labels";
import { PartyPicker, type PickedParty } from "@/features/sales/party-picker";
import { parseDecimal } from "@/features/sales/quote-math";
import { ApiError } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { sourceLabels } from "./labels";
import { leadSources, useLeadMutations, type Lead } from "./leads-api";

interface LeadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  lead?: Lead;
  /** Who owns a new lead by default: the person adding it. */
  defaultOwnerId?: string;
  onSaved?: (lead: Lead) => void;
}

/** Add or edit a lead: who, what they want, what it may be worth, who follows it up and when. */
export function LeadDialog({ open, onOpenChange, lead, defaultOwnerId, onSaved }: LeadDialogProps) {
  const { t, tError } = useI18n();
  const { save } = useLeadMutations();
  const members = useAssignableMembers();
  const [title, setTitle] = useState(lead?.title ?? "");
  const [companyName, setCompanyName] = useState(lead?.companyName ?? "");
  const [contactName, setContactName] = useState(lead?.contactName ?? "");
  const [email, setEmail] = useState(lead?.email ?? "");
  const [phone, setPhone] = useState(lead?.phone ?? "");
  const [source, setSource] = useState(lead?.source ?? "referral");
  const [party, setParty] = useState<PickedParty | null>(lead?.partyId ? { id: lead.partyId, name: lead.partyName ?? "" } : null);
  const [value, setValue] = useState(lead?.estimatedValue ? String(lead.estimatedValue).replace(".", ",") : "");
  const [currency, setCurrency] = useState(lead?.currency ?? "TRY");
  const [expectedClose, setExpectedClose] = useState(lead?.expectedCloseDate ?? "");
  const [ownerId, setOwnerId] = useState(lead ? (lead.ownerUserId ?? "") : (defaultOwnerId ?? ""));
  const [nextStep, setNextStep] = useState(lead?.nextStep ?? "");
  const [nextStepDate, setNextStepDate] = useState(lead?.nextStepDate ?? "");
  const [notes, setNotes] = useState(lead?.notes ?? "");
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    const estimated = value.trim() ? parseDecimal(value) : null;
    if (!title.trim()) {
      setError(t("leads.titleRequired"));
      return;
    }
    if (estimated !== null && (Number.isNaN(estimated) || estimated < 0)) {
      setError(t("finance.amountInvalid"));
      return;
    }
    setError(null);
    try {
      const saved = await save.mutateAsync({
        id: lead?.id,
        title: title.trim(),
        companyName: companyName.trim() || null,
        contactName: contactName.trim() || null,
        email: email.trim() || null,
        phone: phone.trim() || null,
        source,
        partyId: party?.id ?? null,
        estimatedValue: estimated,
        currency,
        expectedCloseDate: expectedClose || null,
        ownerUserId: ownerId || null,
        nextStep: nextStep.trim() || null,
        nextStepDate: nextStepDate || null,
        notes: notes.trim() || null,
      });
      toast.success(t(lead ? "leads.saved" : "leads.created"));
      onSaved?.(saved);
      onOpenChange(false);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t(lead ? "leads.edit" : "leads.new")}</DialogTitle>
          <DialogDescription>{t("leads.dialogHint")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          {error ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}
          <FormField id="lead-title" label={t("leads.field.title")} placeholder={t("leads.field.titlePlaceholder")} value={title} onChange={(event) => setTitle(event.target.value)} maxLength={200} />
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField id="lead-company" label={t("leads.field.company")} value={companyName} onChange={(event) => setCompanyName(event.target.value)} maxLength={200} />
            <FormField id="lead-contact" label={t("leads.field.contact")} value={contactName} onChange={(event) => setContactName(event.target.value)} maxLength={200} />
            <FormField id="lead-email" type="email" label={t("crm.field.email")} value={email} onChange={(event) => setEmail(event.target.value)} maxLength={254} />
            <FormField id="lead-phone" label={t("crm.field.phone")} value={phone} onChange={(event) => setPhone(event.target.value)} maxLength={30} />
          </div>
          <div className="grid gap-1.5">
            <Label className="text-[13px] font-medium">{t("leads.field.party")}</Label>
            <PartyPicker id="lead-party" value={party} onChange={setParty} />
            <p className="text-muted-foreground text-xs">{t("leads.field.partyHint")}</p>
          </div>
          <div className="grid items-start gap-3 sm:grid-cols-[1fr_120px_1fr]">
            <FormField id="lead-value" inputMode="decimal" label={t("leads.field.value")} placeholder="0,00" value={value} onChange={(event) => setValue(event.target.value)} />
            <div className="grid gap-1.5">
              <Label htmlFor="lead-currency" className="text-[13px] font-medium">
                {t("sales.field.currency")}
              </Label>
              <select id="lead-currency" className={selectClass} value={currency} onChange={(event) => setCurrency(event.target.value)}>
                {currencies.map((code) => (
                  <option key={code}>{code}</option>
                ))}
              </select>
            </div>
            <FormField id="lead-close" type="date" label={t("leads.field.expectedClose")} value={expectedClose} onChange={(event) => setExpectedClose(event.target.value)} />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="lead-source" className="text-[13px] font-medium">
                {t("leads.field.source")}
              </Label>
              <select id="lead-source" className={selectClass} value={source} onChange={(event) => setSource(event.target.value)}>
                {leadSources.map((option) => (
                  <option key={option} value={option}>
                    {t(sourceLabels[option]!)}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="lead-owner" className="text-[13px] font-medium">
                {t("leads.field.owner")}
              </Label>
              <select id="lead-owner" className={selectClass} value={ownerId} onChange={(event) => setOwnerId(event.target.value)}>
                <option value="">{t("leads.field.noOwner")}</option>
                {(members.data ?? []).map((member) => (
                  <option key={member.userId} value={member.userId}>
                    {member.fullName}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid gap-3 sm:grid-cols-[1fr_170px]">
            <FormField id="lead-next" label={t("leads.field.nextStep")} placeholder={t("leads.field.nextStepPlaceholder")} value={nextStep} onChange={(event) => setNextStep(event.target.value)} maxLength={300} />
            <FormField id="lead-next-date" type="date" label={t("leads.field.nextStepDate")} value={nextStepDate} onChange={(event) => setNextStepDate(event.target.value)} />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="lead-notes" className="text-[13px] font-medium">
              {t("leads.field.notes")}
            </Label>
            <Textarea id="lead-notes" rows={3} value={notes} onChange={(event) => setNotes(event.target.value)} maxLength={4000} />
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={save.isPending}>
              {save.isPending ? t("common.saving") : t("common.save")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

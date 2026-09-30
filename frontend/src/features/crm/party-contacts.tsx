"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, Mail, Pencil, Phone, Plus, Star, Trash2, UserRound } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { applyApiError } from "@/features/identity/form-errors";
import { initials } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { useRemoveContact, useSaveContact, type PartyContact } from "./parties-api";

const fields = ["fullName", "title", "email", "phone", "isPrimary"] as const;

const nullIfEmpty = (value: string) => (value === "" ? null : value);

function ContactForm({ partyId, contact, onDone }: { partyId: string; contact?: PartyContact; onDone: () => void }) {
  const { t, tError } = useI18n();
  const save = useSaveContact(partyId, contact?.id);
  const [formError, setFormError] = useState<string | null>(null);

  const schema = useMemo(
    () =>
      z.object({
        fullName: z.string().trim().min(1, t("validation.required")).max(200, t("validation.max_length", { maxLength: 200 })),
        title: z.string().trim().max(100, t("validation.max_length", { maxLength: 100 })).transform(nullIfEmpty),
        email: z
          .string()
          .trim()
          .refine((value) => value === "" || z.email().safeParse(value).success, t("validation.email"))
          .transform(nullIfEmpty),
        phone: z.string().trim().max(30, t("validation.max_length", { maxLength: 30 })).transform(nullIfEmpty),
        isPrimary: z.boolean(),
      }),
    [t],
  );

  const form = useForm<z.input<typeof schema>, unknown, z.output<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: {
      fullName: contact?.fullName ?? "",
      title: contact?.title ?? "",
      email: contact?.email ?? "",
      phone: contact?.phone ?? "",
      isPrimary: contact?.isPrimary ?? false,
    },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await save.mutateAsync(values);
      toast.success(t("crm.contact.saved", { name: values.fullName }));
      onDone();
    } catch (error) {
      setFormError(applyApiError(error, fields, form.setError, tError));
    }
  });

  const { errors } = form.formState;

  return (
    <form onSubmit={onSubmit} className="grid gap-4" noValidate>
      {formError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      ) : null}
      <FormField id="contact-name" autoComplete="off" label={t("crm.contact.fullName")} error={errors.fullName?.message} {...form.register("fullName")} />
      <FormField id="contact-title" autoComplete="off" label={t("crm.contact.title")} error={errors.title?.message} {...form.register("title")} />
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField id="contact-email" type="email" autoComplete="off" label={t("crm.field.email")} error={errors.email?.message} {...form.register("email")} />
        <FormField id="contact-phone" type="tel" autoComplete="off" label={t("crm.field.phone")} error={errors.phone?.message} {...form.register("phone")} />
      </div>
      <label className="flex cursor-pointer items-start gap-2 text-sm">
        <input type="checkbox" className="accent-primary mt-0.5 size-4" {...form.register("isPrimary")} />
        <span>
          {t("crm.contact.isPrimary")}
          <span className="text-muted-foreground block text-[13px]">{t("crm.contact.isPrimaryHint")}</span>
        </span>
      </label>
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onDone}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t("common.saving") : t("common.save")}
        </Button>
      </div>
    </form>
  );
}

/** The people at a party. Editing needs the write permission; the list is visible to readers. */
export function PartyContacts({ partyId, contacts, canWrite }: { partyId: string; contacts: PartyContact[]; canWrite: boolean }) {
  const { t, tError } = useI18n();
  const remove = useRemoveContact(partyId);
  // undefined: closed · null: adding · a contact: editing it.
  const [editing, setEditing] = useState<PartyContact | null | undefined>(undefined);

  const onRemove = async (contact: PartyContact) => {
    if (!window.confirm(t("crm.contact.removeConfirm", { name: contact.fullName }))) return;
    try {
      await remove.mutateAsync(contact.id);
      toast.success(t("crm.contact.removed"));
    } catch {
      toast.error(tError("common.unexpected"));
    }
  };

  return (
    <>
      {contacts.length === 0 ? (
        <EmptyState
          icon={UserRound}
          title={t("crm.contact.empty")}
          className="py-6"
          action={
            canWrite ? (
              <Button variant="outline" size="sm" onClick={() => setEditing(null)}>
                <Plus /> {t("crm.contact.add")}
              </Button>
            ) : null
          }
        />
      ) : (
        <div className="grid gap-3">
          <ul className="divide-border divide-y">
            {contacts.map((contact) => (
              <li key={contact.id} className="flex items-start gap-3 py-3 first:pt-0 last:pb-0">
                <span className="bg-primary-soft text-primary-strong flex size-9 shrink-0 items-center justify-center rounded-full text-xs font-semibold">
                  {initials(contact.fullName)}
                </span>
                <div className="grid min-w-0 flex-1 gap-0.5">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{contact.fullName}</span>
                    {contact.isPrimary ? (
                      <Badge variant="info">
                        <Star aria-hidden /> {t("crm.contact.primary")}
                      </Badge>
                    ) : null}
                  </div>
                  {contact.title ? <span className="text-muted-foreground text-[13px]">{contact.title}</span> : null}
                  <div className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-0.5 text-[13px]">
                    {contact.email ? (
                      <a href={`mailto:${contact.email}`} className="hover:text-foreground inline-flex items-center gap-1 underline-offset-4 hover:underline">
                        <Mail className="size-3.5" aria-hidden /> {contact.email}
                      </a>
                    ) : null}
                    {contact.phone ? (
                      <a href={`tel:${contact.phone}`} className="hover:text-foreground tabular inline-flex items-center gap-1 underline-offset-4 hover:underline">
                        <Phone className="size-3.5" aria-hidden /> {contact.phone}
                      </a>
                    ) : null}
                  </div>
                </div>
                {canWrite ? (
                  <div className="flex shrink-0 gap-1">
                    <Button variant="ghost" size="icon-sm" onClick={() => setEditing(contact)} aria-label={t("crm.contact.edit", { name: contact.fullName })}>
                      <Pencil />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      onClick={() => void onRemove(contact)}
                      disabled={remove.isPending}
                      aria-label={t("crm.contact.remove", { name: contact.fullName })}
                    >
                      <Trash2 />
                    </Button>
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
          {canWrite ? (
            <Button variant="outline" size="sm" className="justify-self-start" onClick={() => setEditing(null)}>
              <Plus /> {t("crm.contact.add")}
            </Button>
          ) : null}
        </div>
      )}

      <Dialog open={editing !== undefined} onOpenChange={(open) => (open ? null : setEditing(undefined))}>
        <DialogContent className="sm:max-w-md" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t(editing ? "crm.contact.editTitle" : "crm.contact.addTitle")}</DialogTitle>
            <DialogDescription>{t("crm.contact.description")}</DialogDescription>
          </DialogHeader>
          {editing !== undefined ? (
            <ContactForm key={editing?.id ?? "new"} partyId={partyId} contact={editing ?? undefined} onDone={() => setEditing(undefined)} />
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}

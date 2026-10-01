"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, Building2, UserRound } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { applyApiError } from "@/features/identity/form-errors";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { CustomFieldInputs } from "./custom-fields";
import { useSaveParty, type Party } from "./parties-api";
import { isValidTaxNumber } from "./tax-number";

const fields = [
  "kind",
  "name",
  "isCustomer",
  "code",
  "taxNumber",
  "taxOffice",
  "email",
  "phone",
  "website",
  "city",
  "district",
  "addressLine",
] as const;

const kinds = ["company", "person"] as const;

/** Optional text: empty input becomes null for the API. */
const optional = (max: number, message: string) =>
  z
    .string()
    .trim()
    .max(max, message)
    .transform((value) => (value === "" ? null : value));

interface PartyFormProps {
  /** Editing when given; creating otherwise. */
  party?: Party;
  onSaved: (party: Party) => void;
  onCancel: () => void;
}

export function PartyForm({ party, onSaved, onCancel }: PartyFormProps) {
  const { t, tError } = useI18n();
  const save = useSaveParty(party?.id);
  const [formError, setFormError] = useState<string | null>(null);
  const [customValues, setCustomValues] = useState<Record<string, string>>(() => ({ ...(party?.customFields ?? {}) }));

  const schema = useMemo(() => {
    const max = (maxLength: number) => t("validation.max_length", { maxLength });
    return z
      .object({
        kind: z.enum(kinds),
        name: z.string().trim().min(1, t("validation.required")).max(250, max(250)),
        isCustomer: z.boolean(),
        isSupplier: z.boolean(),
        code: optional(30, max(30)),
        taxNumber: z
          .string()
          .trim()
          .transform((value) => (value === "" ? null : value)),
        taxOffice: optional(100, max(100)),
        email: z
          .string()
          .trim()
          .max(254, max(254))
          .refine((value) => value === "" || z.email().safeParse(value).success, t("validation.email"))
          .transform((value) => (value === "" ? null : value)),
        phone: optional(30, max(30)),
        website: optional(200, max(200)),
        city: optional(100, max(100)),
        district: optional(100, max(100)),
        addressLine: optional(500, max(500)),
      })
      .superRefine((values, context) => {
        if (!values.isCustomer && !values.isSupplier) {
          context.addIssue({ code: "custom", path: ["isCustomer"], message: tError("crm.party.role_required") });
        }
        if (values.taxNumber && !isValidTaxNumber(values.kind, values.taxNumber)) {
          context.addIssue({ code: "custom", path: ["taxNumber"], message: tError("crm.party.tax_number_invalid") });
        }
      });
  }, [t, tError]);

  type FormInput = z.input<typeof schema>;
  type FormOutput = z.output<typeof schema>;

  const form = useForm<FormInput, unknown, FormOutput>({
    resolver: zodResolver(schema),
    defaultValues: {
      kind: (party?.kind as "company" | "person" | undefined) ?? "company",
      name: party?.name ?? "",
      isCustomer: party?.isCustomer ?? true,
      isSupplier: party?.isSupplier ?? false,
      code: "",
      taxNumber: party?.taxNumber ?? "",
      taxOffice: party?.taxOffice ?? "",
      email: party?.email ?? "",
      phone: party?.phone ?? "",
      website: party?.website ?? "",
      city: party?.city ?? "",
      district: party?.district ?? "",
      addressLine: party?.addressLine ?? "",
    },
  });

  const kind = useWatch({ control: form.control, name: "kind" });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      const customFields = Object.fromEntries(Object.entries(customValues).map(([key, value]) => [key, value.trim()]));
      const saved = await save.mutateAsync(party ? { ...values, code: undefined, customFields } : { ...values, customFields });
      toast.success(t(party ? "crm.party.saved" : "crm.party.created", { name: saved.name }));
      onSaved(saved);
    } catch (error) {
      setFormError(applyApiError(error, fields, form.setError, tError));
    }
  });

  const { errors } = form.formState;

  return (
    <form onSubmit={onSubmit} className="grid gap-5" noValidate>
      {formError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      ) : null}

      <fieldset className="grid gap-1.5">
        <legend className="mb-1.5 text-[13px] font-medium">{t("crm.field.kind")}</legend>
        <div role="radiogroup" className="grid grid-cols-2 gap-2">
          {kinds.map((option) => {
            const Icon = option === "company" ? Building2 : UserRound;
            const selected = kind === option;
            return (
              <label
                key={option}
                className={cn(
                  "flex h-10 cursor-pointer items-center gap-2 rounded-lg border px-3 text-sm transition-colors",
                  "has-focus-visible:ring-ring/25 has-focus-visible:ring-3",
                  selected ? "border-primary bg-primary-soft text-primary-strong font-medium" : "border-input hover:bg-muted/60",
                )}
              >
                <input type="radio" value={option} className="sr-only" {...form.register("kind")} />
                <Icon className="size-4" aria-hidden />
                {t(option === "company" ? "crm.kind.company" : "crm.kind.person")}
              </label>
            );
          })}
        </div>
      </fieldset>

      <div className="grid items-start gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormField
            id="party-name"
            autoComplete="off"
            label={t(kind === "company" ? "crm.field.companyName" : "crm.field.personName")}
            error={errors.name?.message}
            {...form.register("name")}
          />
        </div>

        <fieldset className="grid gap-1.5 sm:col-span-2" aria-describedby={errors.isCustomer ? "party-roles-error" : undefined}>
          <legend className="mb-1.5 text-[13px] font-medium">{t("crm.field.roles")}</legend>
          <div className="flex flex-wrap gap-x-6 gap-y-2">
            <label className="flex cursor-pointer items-center gap-2 text-sm">
              <input type="checkbox" className="accent-primary size-4" {...form.register("isCustomer")} />
              {t("crm.role.customer")}
            </label>
            <label className="flex cursor-pointer items-center gap-2 text-sm">
              <input type="checkbox" className="accent-primary size-4" {...form.register("isSupplier")} />
              {t("crm.role.supplier")}
            </label>
          </div>
          {errors.isCustomer ? (
            <p id="party-roles-error" role="alert" className="text-destructive text-[13px]">
              {errors.isCustomer.message}
            </p>
          ) : null}
        </fieldset>

        <FormField
          id="party-tax-number"
          inputMode="numeric"
          autoComplete="off"
          label={t(kind === "company" ? "crm.field.vkn" : "crm.field.tckn")}
          error={errors.taxNumber?.message}
          {...form.register("taxNumber")}
        />
        <FormField id="party-tax-office" autoComplete="off" label={t("crm.field.taxOffice")} error={errors.taxOffice?.message} {...form.register("taxOffice")} />
        <FormField id="party-email" type="email" autoComplete="off" label={t("crm.field.email")} error={errors.email?.message} {...form.register("email")} />
        <FormField id="party-phone" type="tel" autoComplete="off" label={t("crm.field.phone")} error={errors.phone?.message} {...form.register("phone")} />
        <FormField id="party-city" autoComplete="off" label={t("crm.field.city")} error={errors.city?.message} {...form.register("city")} />
        <FormField id="party-district" autoComplete="off" label={t("crm.field.district")} error={errors.district?.message} {...form.register("district")} />

        <div className="grid gap-1.5 sm:col-span-2">
          <Label htmlFor="party-address" className="text-[13px] font-medium">
            {t("crm.field.address")}
          </Label>
          <Textarea
            id="party-address"
            rows={2}
            aria-invalid={!!errors.addressLine}
            aria-describedby={errors.addressLine ? "party-address-error" : undefined}
            {...form.register("addressLine")}
          />
          {errors.addressLine ? (
            <p id="party-address-error" role="alert" className="text-destructive text-[13px]">
              {errors.addressLine.message}
            </p>
          ) : null}
        </div>

        <FormField id="party-website" type="url" autoComplete="off" label={t("crm.field.website")} error={errors.website?.message} {...form.register("website")} />
        {party ? null : (
          <FormField
            id="party-code"
            autoComplete="off"
            label={t("crm.field.code")}
            hint={t("crm.field.codeHint")}
            error={errors.code?.message}
            {...form.register("code")}
          />
        )}
      </div>

      <CustomFieldInputs values={customValues} onChange={setCustomValues} />

      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t("common.saving") : t(party ? "common.save" : "crm.party.create")}
        </Button>
      </div>
    </form>
  );
}

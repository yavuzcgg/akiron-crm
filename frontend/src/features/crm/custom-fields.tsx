"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowDown, ArrowUp, Plus, SlidersHorizontal, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { api, type Schemas } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatCalendarDate, formatNumber } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";

export type CustomField = Schemas["CustomFieldResponse"];

const fieldsKey = ["crm", "custom-fields"] as const;
const types = ["text", "number", "date", "select", "checkbox"] as const;
const typeLabels: Record<string, TranslationKey> = {
  text: "crm.customFields.type.text",
  number: "crm.customFields.type.number",
  date: "crm.customFields.type.date",
  select: "crm.customFields.type.select",
  checkbox: "crm.customFields.type.checkbox",
};

const controlClass =
  "border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 h-10 w-full rounded-lg border px-3 text-sm outline-none focus-visible:ring-3";

export function useCustomFields() {
  return useQuery({
    queryKey: fieldsKey,
    queryFn: async () => unwrap(await api.GET("/api/v1/crm/custom-fields")),
    staleTime: 60_000,
  });
}

/** The tenant's extra fields inside the party form. Values travel as strings, as the API stores them. */
export function CustomFieldInputs({ values, onChange }: { values: Record<string, string>; onChange: (values: Record<string, string>) => void }) {
  const { t } = useI18n();
  const fields = useCustomFields();
  if (!fields.data || fields.data.length === 0) return null;

  const set = (key: string, value: string) => onChange({ ...values, [key]: value });

  return (
    <fieldset className="grid gap-4 border-t pt-4 sm:grid-cols-2">
      <legend className="text-muted-foreground mb-1 text-xs font-semibold tracking-wider uppercase">{t("crm.customFields.section")}</legend>
      {fields.data.map((field) => {
        const id = `cf-${field.key}`;
        const label = `${field.label}${field.isRequired ? " *" : ""}`;
        if (field.type === "checkbox") {
          return (
            <label key={field.key} className="flex cursor-pointer items-center gap-2 self-end pb-2 text-sm">
              <input
                id={id}
                type="checkbox"
                className="accent-primary size-4"
                checked={values[field.key] === "true"}
                onChange={(event) => set(field.key, event.target.checked ? "true" : "")}
              />
              {label}
            </label>
          );
        }

        return (
          <div key={field.key} className="grid gap-1.5">
            <Label htmlFor={id} className="text-[13px] font-medium">
              {label}
            </Label>
            {field.type === "select" ? (
              <select id={id} className={controlClass} value={values[field.key] ?? ""} onChange={(event) => set(field.key, event.target.value)}>
                <option value="">—</option>
                {field.options.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            ) : (
              <Input
                id={id}
                className="h-10"
                type={field.type === "date" ? "date" : "text"}
                inputMode={field.type === "number" ? "decimal" : undefined}
                maxLength={500}
                value={values[field.key] ?? ""}
                onChange={(event) => set(field.key, field.type === "number" ? event.target.value.replace(",", ".") : event.target.value)}
              />
            )}
          </div>
        );
      })}
    </fieldset>
  );
}

/** Stored values shown on the party page, in the fields' order and format. */
export function CustomFieldValues({ values }: { values: Record<string, string> }) {
  const { t } = useI18n();
  const fields = useCustomFields();
  if (!fields.data || fields.data.length === 0) return null;

  const show = (field: CustomField, value: string | undefined) => {
    if (!value) return "—";
    if (field.type === "date") return formatCalendarDate(value);
    if (field.type === "number") return formatNumber(Number(value));
    if (field.type === "checkbox") return t("crm.customFields.yes");
    return value;
  };

  return (
    <dl className="grid gap-4 border-t pt-4 sm:grid-cols-2">
      {fields.data.map((field) => (
        <div key={field.key} className="grid gap-0.5">
          <dt className="text-muted-foreground text-xs font-medium tracking-[0.02em]">{field.label}</dt>
          <dd className="text-sm break-words">{show(field, values[field.key])}</dd>
        </div>
      ))}
    </dl>
  );
}

/** Admins shape the party card: add, reorder, rename, require, remove fields. The type is fixed once created. */
export function CustomFieldManager() {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const fields = useCustomFields();
  const [label, setLabel] = useState("");
  const [type, setType] = useState<string>("text");
  const [options, setOptions] = useState("");
  const [required, setRequired] = useState(false);
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));
  const refresh = () => queryClient.invalidateQueries({ queryKey: fieldsKey });

  const create = useMutation({
    mutationFn: async () =>
      unwrap(
        await api.POST("/api/v1/crm/custom-fields", {
          body: { label: label.trim(), type, isRequired: required, options: options.split("\n").map((option) => option.trim()).filter(Boolean) },
        }),
      ),
    onSuccess: () => {
      setLabel("");
      setOptions("");
      setRequired(false);
      void refresh();
    },
    onError: fail,
  });
  const update = useMutation({
    mutationFn: async (field: CustomField & { isRequired: boolean }) =>
      unwrap(
        await api.PUT("/api/v1/crm/custom-fields/{id}", {
          params: { path: { id: field.id } },
          body: { label: field.label, type: field.type, options: field.options, isRequired: field.isRequired },
        }),
      ),
    onSuccess: refresh,
    onError: fail,
  });
  const remove = useMutation({
    mutationFn: async (id: string) => {
      unwrap(await api.DELETE("/api/v1/crm/custom-fields/{id}", { params: { path: { id } } }));
    },
    onSuccess: refresh,
    onError: fail,
  });
  const reorder = useMutation({
    mutationFn: async (fieldIds: string[]) => unwrap(await api.PUT("/api/v1/crm/custom-fields/order", { body: { fieldIds } })),
    onSuccess: refresh,
    onError: fail,
  });

  if (fields.isPending) return <Skeleton className="h-32 w-full" />;
  const list = fields.data ?? [];

  const move = (from: number, to: number) => {
    const ids = list.map((field) => field.id);
    const [moved] = ids.splice(from, 1);
    ids.splice(to, 0, moved!);
    reorder.mutate(ids);
  };

  return (
    <div className="grid gap-4">
      {list.length === 0 ? (
        <EmptyState icon={SlidersHorizontal} title={t("crm.customFields.empty")} description={t("crm.customFields.emptyHint")} className="py-4" />
      ) : (
        <ul className="divide-border divide-y">
          {list.map((field, index) => (
            <li key={field.id} className="flex items-center gap-2 py-2">
              <div className="flex flex-col">
                <Button variant="ghost" size="icon-xs" disabled={index === 0} onClick={() => move(index, index - 1)} aria-label={t("crm.customFields.up", { field: field.label })}>
                  <ArrowUp />
                </Button>
                <Button variant="ghost" size="icon-xs" disabled={index === list.length - 1} onClick={() => move(index, index + 1)} aria-label={t("crm.customFields.down", { field: field.label })}>
                  <ArrowDown />
                </Button>
              </div>
              <div className="grid min-w-0 flex-1">
                <span className="font-medium">{field.label}</span>
                <span className="text-muted-foreground truncate text-xs">
                  {t(typeLabels[field.type] ?? "crm.customFields.type.text")}
                  {field.options.length > 0 ? ` · ${field.options.join(", ")}` : ""}
                </span>
              </div>
              <label className="flex cursor-pointer items-center gap-1.5 text-xs">
                <input type="checkbox" className="accent-primary size-3.5" checked={field.isRequired} onChange={(event) => update.mutate({ ...field, isRequired: event.target.checked })} />
                {t("crm.customFields.required")}
              </label>
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={() => {
                  if (window.confirm(t("crm.customFields.removeConfirm", { field: field.label }))) remove.mutate(field.id);
                }}
                aria-label={t("crm.customFields.remove", { field: field.label })}
              >
                <Trash2 />
              </Button>
            </li>
          ))}
        </ul>
      )}

      <form
        className="bg-muted/50 grid gap-3 rounded-lg p-3"
        onSubmit={(event) => {
          event.preventDefault();
          if (label.trim()) create.mutate();
        }}
      >
        <div className="grid gap-2 sm:grid-cols-[1fr_160px]">
          <Input className="h-9" value={label} onChange={(event) => setLabel(event.target.value)} maxLength={60} placeholder={t("crm.customFields.labelPlaceholder")} aria-label={t("crm.customFields.label")} />
          <select className={`${controlClass} h-9`} value={type} onChange={(event) => setType(event.target.value)} aria-label={t("crm.customFields.type")}>
            {types.map((option) => (
              <option key={option} value={option}>
                {t(typeLabels[option]!)}
              </option>
            ))}
          </select>
        </div>
        {type === "select" ? (
          <textarea
            className={`${controlClass} h-auto py-2`}
            rows={3}
            value={options}
            onChange={(event) => setOptions(event.target.value)}
            placeholder={t("crm.customFields.optionsHint")}
            aria-label={t("crm.customFields.options")}
          />
        ) : null}
        <div className="flex items-center justify-between gap-2">
          <label className="flex cursor-pointer items-center gap-2 text-sm">
            <input type="checkbox" className="accent-primary size-4" checked={required} onChange={(event) => setRequired(event.target.checked)} />
            {t("crm.customFields.required")}
          </label>
          <Button type="submit" size="sm" disabled={create.isPending || !label.trim()}>
            <Plus /> {t("crm.customFields.add")}
          </Button>
        </div>
      </form>
      <p className="text-muted-foreground text-[13px]">{t("crm.customFields.hint")}</p>
    </div>
  );
}

"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle } from "lucide-react";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { applyApiError } from "@/features/identity/form-errors";
import { useI18n } from "@/lib/i18n";
import { AssigneePicker } from "./assignee-picker";
import { ClientPicker } from "./client-picker";
import { priorities, useSaveWorkOrder, type WorkOrder } from "./jobs-api";
import { priorityLabels } from "./labels";

// A client error (partyId) has no field of that name here; it shows above the form.
const fields = ["title", "description", "priority", "dueDate", "assigneeIds"] as const;

const selectClass =
  "border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 h-10 w-full rounded-lg border px-3 text-sm outline-none focus-visible:ring-3";

interface WorkOrderFormProps {
  workOrder?: WorkOrder;
  /** Where a new card lands; the first open stage when missing. */
  stageId?: string;
  /** Pre-filled client, e.g. when opened from a client's page. */
  client?: { partyId: string; name: string };
  onSaved: (workOrder: WorkOrder) => void;
  onCancel: () => void;
}

export function WorkOrderForm({ workOrder, stageId, client, onSaved, onCancel }: WorkOrderFormProps) {
  const { t, tError } = useI18n();
  const save = useSaveWorkOrder(workOrder?.id);
  const [formError, setFormError] = useState<string | null>(null);

  const schema = useMemo(
    () =>
      z.object({
        title: z.string().trim().min(1, t("validation.required")).max(200, t("validation.max_length", { maxLength: 200 })),
        description: z.string().max(10_000, t("validation.max_length", { maxLength: 10_000 })),
        client: z.object({ partyId: z.string(), name: z.string() }).nullable(),
        priority: z.enum(priorities),
        dueDate: z.string(),
        assigneeIds: z.array(z.string()),
      }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: workOrder?.title ?? "",
      description: workOrder?.description ?? "",
      client: workOrder?.partyId ? { partyId: workOrder.partyId, name: workOrder.partyName ?? "" } : (client ?? null),
      priority: (workOrder?.priority as (typeof priorities)[number] | undefined) ?? "normal",
      dueDate: workOrder?.dueDate ?? "",
      assigneeIds: workOrder?.assignees.map((assignee) => assignee.userId) ?? [],
    },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      const saved = await save.mutateAsync({
        title: values.title,
        description: values.description.trim() || null,
        partyId: values.client?.partyId ?? null,
        priority: values.priority,
        dueDate: values.dueDate || null,
        assigneeIds: values.assigneeIds,
        stageId: workOrder ? undefined : stageId,
      });
      toast.success(t(workOrder ? "jobs.workOrder.saved" : "jobs.workOrder.created", { number: saved.number }));
      onSaved(saved);
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

      <FormField id="wo-title" autoComplete="off" label={t("jobs.field.title")} error={errors.title?.message} {...form.register("title")} />

      <Controller
        control={form.control}
        name="client"
        render={({ field }) => <ClientPicker id="wo-client" value={field.value} onChange={field.onChange} />}
      />

      <div className="grid items-start gap-4 sm:grid-cols-2">
        <div className="grid gap-1.5">
          <Label htmlFor="wo-priority" className="text-[13px] font-medium">
            {t("jobs.field.priority")}
          </Label>
          <select id="wo-priority" className={selectClass} {...form.register("priority")}>
            {priorities.map((priority) => (
              <option key={priority} value={priority}>
                {t(priorityLabels[priority])}
              </option>
            ))}
          </select>
        </div>
        <FormField id="wo-due" type="date" label={t("jobs.field.dueDate")} error={errors.dueDate?.message} {...form.register("dueDate")} />
      </div>

      <Controller
        control={form.control}
        name="assigneeIds"
        render={({ field }) => <AssigneePicker value={field.value} onChange={field.onChange} />}
      />

      <div className="grid gap-1.5">
        <Label htmlFor="wo-description" className="text-[13px] font-medium">
          {t("jobs.field.description")}
        </Label>
        <Textarea id="wo-description" rows={4} placeholder={t("jobs.field.descriptionHint")} {...form.register("description")} />
      </div>

      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t("common.saving") : t(workOrder ? "common.save" : "jobs.workOrder.create")}
        </Button>
      </div>
    </form>
  );
}

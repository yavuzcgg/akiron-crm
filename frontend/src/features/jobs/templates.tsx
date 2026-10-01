"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { LayoutTemplate, Pencil, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { api, type Schemas } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { jobsKey, priorities } from "./jobs-api";
import { priorityLabels } from "./labels";

export type Template = Schemas["TemplateResponse"];

const templatesKey = [...jobsKey, "templates"] as const;

export function useTemplates(enabled = true) {
  return useQuery({
    queryKey: templatesKey,
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/templates")),
    enabled,
  });
}

const selectClass =
  "border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 h-10 w-full rounded-lg border px-3 text-sm outline-none focus-visible:ring-3";

function TemplateForm({ template, onDone }: { template?: Template; onDone: () => void }) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const [name, setName] = useState(template?.name ?? "");
  const [title, setTitle] = useState(template?.title ?? "");
  const [priority, setPriority] = useState(template?.priority ?? "normal");
  const [dueInDays, setDueInDays] = useState(template?.dueInDays?.toString() ?? "");
  const [tasks, setTasks] = useState((template?.tasks ?? []).join("\n"));
  const [description, setDescription] = useState(template?.description ?? "");

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        name: name.trim(),
        title: title.trim() || null,
        description: description.trim() || null,
        priority,
        dueInDays: dueInDays.trim() === "" ? null : Number(dueInDays),
        tasks: tasks.split("\n").map((task) => task.trim()).filter(Boolean),
      };
      return template
        ? unwrap(await api.PUT("/api/v1/jobs/templates/{id}", { params: { path: { id: template.id } }, body }))
        : unwrap(await api.POST("/api/v1/jobs/templates", { body }));
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templatesKey });
      toast.success(t("jobs.templates.saved"));
      onDone();
    },
    onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
  });

  return (
    <form
      className="bg-muted/40 grid gap-3 rounded-lg border p-4"
      onSubmit={(event) => {
        event.preventDefault();
        if (name.trim()) save.mutate();
      }}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <FormField id="tpl-name" label={t("jobs.templates.name")} value={name} onChange={(event) => setName(event.target.value)} maxLength={100} />
        <FormField id="tpl-title" label={t("jobs.templates.workTitle")} hint={t("jobs.templates.workTitleHint")} value={title} onChange={(event) => setTitle(event.target.value)} maxLength={200} />
        <div className="grid gap-1.5">
          <Label htmlFor="tpl-priority" className="text-[13px] font-medium">
            {t("jobs.field.priority")}
          </Label>
          <select id="tpl-priority" className={selectClass} value={priority} onChange={(event) => setPriority(event.target.value)}>
            {priorities.map((option) => (
              <option key={option} value={option}>
                {t(priorityLabels[option]!)}
              </option>
            ))}
          </select>
        </div>
        <FormField id="tpl-due" type="number" min={0} max={365} label={t("jobs.templates.dueInDays")} value={dueInDays} onChange={(event) => setDueInDays(event.target.value)} />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="tpl-tasks" className="text-[13px] font-medium">
          {t("jobs.templates.tasks")}
        </Label>
        <Textarea id="tpl-tasks" rows={4} value={tasks} onChange={(event) => setTasks(event.target.value)} placeholder={t("jobs.templates.tasksHint")} />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="tpl-description" className="text-[13px] font-medium">
          {t("jobs.field.description")}
        </Label>
        <Textarea id="tpl-description" rows={2} value={description} onChange={(event) => setDescription(event.target.value)} />
      </div>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" size="sm" onClick={onDone}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" size="sm" disabled={save.isPending || !name.trim()}>
          {t("common.save")}
        </Button>
      </div>
    </form>
  );
}

/** The template library: recurring jobs with their checklist, lead time and brief. */
export function TemplateManager() {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const templates = useTemplates();
  const [editing, setEditing] = useState<Template | "new" | null>(null);

  const remove = useMutation({
    mutationFn: async (id: string) => {
      unwrap(await api.DELETE("/api/v1/jobs/templates/{id}", { params: { path: { id } } }));
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
    onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
  });

  if (templates.isPending) return <Skeleton className="h-32 w-full" />;
  const list = templates.data ?? [];

  return (
    <div className="grid gap-3">
      {list.length === 0 && editing === null ? <EmptyState icon={LayoutTemplate} title={t("jobs.templates.empty")} description={t("jobs.templates.emptyHint")} className="py-6" /> : null}
      <ul className="divide-border divide-y">
        {list.map((template) =>
          editing !== "new" && editing?.id === template.id ? (
            <li key={template.id} className="py-2">
              <TemplateForm template={template} onDone={() => setEditing(null)} />
            </li>
          ) : (
            <li key={template.id} className="flex items-center gap-3 py-2.5">
              <div className="grid min-w-0 flex-1">
                <span className="font-medium">{template.name}</span>
                <span className="text-muted-foreground text-xs">
                  {t("jobs.templates.summary", { tasks: template.tasks.length, days: template.dueInDays ?? "—" })}
                </span>
              </div>
              <Button variant="ghost" size="icon-sm" onClick={() => setEditing(template)} aria-label={t("jobs.templates.edit", { name: template.name })}>
                <Pencil />
              </Button>
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={() => {
                  if (window.confirm(t("jobs.templates.removeConfirm", { name: template.name }))) remove.mutate(template.id);
                }}
                aria-label={t("jobs.templates.remove", { name: template.name })}
              >
                <Trash2 />
              </Button>
            </li>
          ),
        )}
      </ul>
      {editing === "new" ? (
        <TemplateForm onDone={() => setEditing(null)} />
      ) : (
        <Button variant="outline" size="sm" className="justify-self-start" onClick={() => setEditing("new")}>
          <Plus /> {t("jobs.templates.add")}
        </Button>
      )}
    </div>
  );
}

"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, ArrowRight, Pencil, Plus, Trash2, Zap } from "lucide-react";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { useAssignableMembers } from "@/features/jobs/jobs-api";
import { selectClass } from "@/features/sales/catalog";
import { api, type Schemas } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";

type Rule = Schemas["RuleResponse"];
type RuleInput = Schemas["RuleCommand"];

const rulesKey = ["automation", "rules"] as const;

const triggerLabels: Record<string, TranslationKey> = {
  "quote.accepted": "automation.trigger.quote_accepted",
  "quote.rejected": "automation.trigger.quote_rejected",
  "work_order.completed": "automation.trigger.work_order_completed",
  "receipt.recorded": "automation.trigger.receipt_recorded",
  "lead.created": "automation.trigger.lead_created",
  "ticket.opened": "automation.trigger.ticket_opened",
  "content.approved": "automation.trigger.content_approved",
};

const actionLabels: Record<string, TranslationKey> = {
  email_client: "automation.action.email_client",
  email_address: "automation.action.email_address",
  sms_client: "automation.action.sms_client",
  notify_user: "automation.action.notify_user",
  notify_team: "automation.action.notify_team",
};

const variableLabels: Record<string, TranslationKey> = {
  workspace: "automation.variable.workspace",
  party: "automation.variable.party",
  number: "automation.variable.number",
  title: "automation.variable.title",
  amount: "automation.variable.amount",
  person: "automation.variable.person",
  company: "automation.variable.company",
  source: "automation.variable.source",
};

/** Why a rule did nothing for an event, as the API codes it. */
const skipLabels: Record<string, TranslationKey> = {
  no_client: "automation.skip.no_client",
  no_email: "automation.skip.no_email",
  no_mobile: "automation.skip.no_mobile",
};

const writesToClient = (action: string) => action === "email_client" || action === "sms_client";
const needsSubject = (action: string) => action === "email_client" || action === "email_address";

function useRuleMutations() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["automation"] });
  return {
    save: useMutation({
      mutationFn: async ({ id, ...body }: RuleInput & { id?: string }) =>
        id ? unwrap(await api.PUT("/api/v1/automation/rules/{id}", { params: { path: { id } }, body })) : unwrap(await api.POST("/api/v1/automation/rules", { body })),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        unwrap(await api.DELETE("/api/v1/automation/rules/{id}", { params: { path: { id } } }));
      },
      onSuccess: refresh,
    }),
  };
}

const toInput = (rule: Rule, enabled = rule.enabled): RuleInput & { id: string } => ({
  id: rule.id,
  name: rule.name,
  trigger: rule.trigger,
  action: rule.action,
  target: rule.target,
  subject: rule.subject,
  body: rule.body,
  enabled,
});

/** "When … then …": pick the event, pick what to do, write the message with the event's variables. */
function RuleDialog({ rule, onOpenChange }: { rule?: Rule; onOpenChange: (open: boolean) => void }) {
  const { t, tError } = useI18n();
  const { save } = useRuleMutations();
  const members = useAssignableMembers();
  const catalog = useQuery({ queryKey: ["automation", "catalog"], queryFn: async () => unwrap(await api.GET("/api/v1/automation/catalog")), staleTime: Infinity });
  const bodyRef = useRef<HTMLTextAreaElement>(null);
  const [name, setName] = useState(rule?.name ?? "");
  const [trigger, setTrigger] = useState(rule?.trigger ?? "quote.accepted");
  const [action, setAction] = useState(rule?.action ?? "email_client");
  const [target, setTarget] = useState(rule?.target ?? "");
  const [subject, setSubject] = useState(rule?.subject ?? "");
  const [body, setBody] = useState(rule?.body ?? "");
  const [error, setError] = useState<string | null>(null);

  const info = catalog.data?.triggers.find((item) => item.code === trigger);
  const actions = (catalog.data?.actions ?? []).filter((code) => info?.hasClient !== false || !writesToClient(code));
  const effectiveAction = actions.includes(action) ? action : (actions[0] ?? action);

  const insert = (variable: string) => {
    const token = `{{${variable}}}`;
    const area = bodyRef.current;
    const at = area?.selectionStart ?? body.length;
    setBody(body.slice(0, at) + token + body.slice(area?.selectionEnd ?? at));
    requestAnimationFrame(() => {
      area?.focus();
      area?.setSelectionRange(at + token.length, at + token.length);
    });
  };

  const submit = async () => {
    if (!name.trim() || !body.trim() || (needsSubject(effectiveAction) && !subject.trim()) || ((effectiveAction === "email_address" || effectiveAction === "notify_user") && !target.trim())) {
      setError(t("automation.required"));
      return;
    }
    setError(null);
    try {
      await save.mutateAsync({
        id: rule?.id,
        name: name.trim(),
        trigger,
        action: effectiveAction,
        target: effectiveAction === "email_address" || effectiveAction === "notify_user" ? target.trim() : null,
        subject: needsSubject(effectiveAction) ? subject.trim() : null,
        body: body.trim(),
        enabled: rule?.enabled ?? true,
      });
      toast.success(t("automation.saved"));
      onOpenChange(false);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t(rule ? "automation.edit" : "automation.new")}</DialogTitle>
          <DialogDescription>{t("automation.dialogHint")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          {error ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}
          <FormField id="rule-name" label={t("automation.field.name")} placeholder={t("automation.field.namePlaceholder")} value={name} onChange={(event) => setName(event.target.value)} maxLength={120} />
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="rule-trigger" className="text-[13px] font-medium">
                {t("automation.field.when")}
              </Label>
              <select id="rule-trigger" className={selectClass} value={trigger} onChange={(event) => setTrigger(event.target.value)}>
                {(catalog.data?.triggers ?? []).map((item) => (
                  <option key={item.code} value={item.code}>
                    {t(triggerLabels[item.code] ?? "automation.trigger.unknown")}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="rule-action" className="text-[13px] font-medium">
                {t("automation.field.then")}
              </Label>
              <select id="rule-action" className={selectClass} value={effectiveAction} onChange={(event) => setAction(event.target.value)}>
                {actions.map((code) => (
                  <option key={code} value={code}>
                    {t(actionLabels[code] ?? "automation.action.unknown")}
                  </option>
                ))}
              </select>
            </div>
          </div>
          {effectiveAction === "email_address" ? (
            <FormField id="rule-target" type="email" label={t("automation.field.address")} placeholder="muhasebe@ornek.com" value={target} onChange={(event) => setTarget(event.target.value)} maxLength={254} />
          ) : null}
          {effectiveAction === "notify_user" ? (
            <div className="grid gap-1.5">
              <Label htmlFor="rule-user" className="text-[13px] font-medium">
                {t("automation.field.person")}
              </Label>
              <select id="rule-user" className={selectClass} value={target} onChange={(event) => setTarget(event.target.value)}>
                <option value="">{t("automation.field.pickPerson")}</option>
                {(members.data ?? []).map((member) => (
                  <option key={member.userId} value={member.userId}>
                    {member.fullName}
                  </option>
                ))}
              </select>
            </div>
          ) : null}
          {effectiveAction === "sms_client" ? <p className="bg-muted/60 rounded-lg px-3 py-2 text-[13px]">{t("automation.smsHint")}</p> : null}
          {needsSubject(effectiveAction) ? (
            <FormField id="rule-subject" label={t("automation.field.subject")} value={subject} onChange={(event) => setSubject(event.target.value)} maxLength={200} />
          ) : null}
          <div className="grid gap-1.5">
            <Label htmlFor="rule-body" className="text-[13px] font-medium">
              {t("automation.field.message")}
            </Label>
            <Textarea id="rule-body" ref={bodyRef} rows={5} maxLength={4000} value={body} onChange={(event) => setBody(event.target.value)} />
            <div className="flex flex-wrap items-center gap-1.5">
              <span className="text-muted-foreground text-xs">{t("automation.insert")}</span>
              {(info?.variables ?? []).map((variable) => (
                <button
                  key={variable}
                  type="button"
                  onClick={() => insert(variable)}
                  className="hover:bg-primary-soft focus-visible:ring-ring/40 cursor-pointer rounded-md border px-2 py-0.5 text-xs focus-visible:ring-2 focus-visible:outline-none"
                >
                  {t(variableLabels[variable] ?? "automation.variable.title")}
                </button>
              ))}
            </div>
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

function Runs({ ruleId, sms }: { ruleId: string; sms: boolean }) {
  const { t } = useI18n();
  const runs = useQuery({
    queryKey: ["automation", "runs", ruleId],
    queryFn: async () => unwrap(await api.GET("/api/v1/automation/rules/{id}/runs", { params: { path: { id: ruleId } } })),
  });
  if (runs.isPending) return <Skeleton className="h-8 w-full" />;
  if ((runs.data ?? []).length === 0) return <p className="text-muted-foreground text-sm">{t("automation.noRuns")}</p>;
  return (
    <ul className="grid gap-1 text-sm">
      {runs.data!.map((run, index) => (
        <li key={index} className="flex flex-wrap items-baseline gap-x-2">
          <time className="text-muted-foreground text-xs tabular-nums" dateTime={run.occurredAt}>
            {formatDateTime(run.occurredAt)}
          </time>
          {run.outcome === "done" ? (
            <span>{run.detail ? t(sms ? "automation.run.loggedFor" : "automation.run.sentTo", { to: run.detail }) : t("automation.run.done")}</span>
          ) : (
            <span className="text-muted-foreground">{t(skipLabels[run.detail ?? ""] ?? "automation.skip.other")}</span>
          )}
        </li>
      ))}
    </ul>
  );
}

function RuleCard({ rule, onEdit }: { rule: Rule; onEdit: () => void }) {
  const { t, tError } = useI18n();
  const { save, remove } = useRuleMutations();
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));
  const target = rule.action === "notify_user" ? rule.targetName : rule.action === "email_address" ? rule.target : null;

  return (
    <Card>
      <CardContent className="grid gap-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="grid min-w-0 gap-1.5">
            <p className="font-medium">{rule.name}</p>
            <p className="flex flex-wrap items-center gap-1.5 text-sm">
              <Badge variant="secondary">{t(triggerLabels[rule.trigger] ?? "automation.trigger.unknown")}</Badge>
              <ArrowRight className="text-muted-foreground size-3.5" aria-hidden />
              <Badge variant="info">{t(actionLabels[rule.action] ?? "automation.action.unknown")}</Badge>
              {target ? <span className="text-muted-foreground truncate">{target}</span> : null}
            </p>
          </div>
          <div className="flex shrink-0 items-center gap-1">
            <label className="mr-2 flex cursor-pointer items-center gap-2 text-sm">
              <input
                type="checkbox"
                role="switch"
                className="accent-primary size-4"
                checked={rule.enabled}
                disabled={save.isPending}
                onChange={(event) => save.mutate(toInput(rule, event.target.checked), { onError: fail })}
              />
              {t(rule.enabled ? "automation.on" : "automation.off")}
            </label>
            <Button variant="ghost" size="icon-sm" aria-label={t("automation.edit")} onClick={onEdit}>
              <Pencil />
            </Button>
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={t("automation.delete")}
              onClick={() => {
                if (window.confirm(t("automation.deleteConfirm", { name: rule.name }))) remove.mutate(rule.id, { onError: fail });
              }}
            >
              <Trash2 />
            </Button>
          </div>
        </div>
        <p className="bg-muted/50 text-muted-foreground line-clamp-3 rounded-lg px-3 py-2 text-sm whitespace-pre-wrap">{rule.body}</p>
        {rule.action === "sms_client" ? (
          <p className="text-warning flex items-start gap-1.5 text-[13px]">
            <AlertCircle className="mt-0.5 size-3.5 shrink-0" aria-hidden /> {t("automation.smsNotConnected")}
          </p>
        ) : null}
        <details>
          <summary className="text-muted-foreground cursor-pointer text-xs">
            {rule.lastRunAt ? t("automation.ran", { count: rule.runCount, date: formatDateTime(rule.lastRunAt) }) : t("automation.neverRan")}
          </summary>
          <div className="pt-2">
            <Runs ruleId={rule.id} sms={rule.action === "sms_client"} />
          </div>
        </details>
      </CardContent>
    </Card>
  );
}

/** The organisation's rules: what happens by itself when something happens. */
export function AutomationPage() {
  const { t, tError } = useI18n();
  const rules = useQuery({ queryKey: rulesKey, queryFn: async () => unwrap(await api.GET("/api/v1/automation/rules")) });
  const [editing, setEditing] = useState<Rule | "new" | null>(null);

  const add = (
    <Button onClick={() => setEditing("new")}>
      <Plus /> {t("automation.new")}
    </Button>
  );

  return (
    <div className="grid gap-8">
      <PageHeader title={t("automation.title")} description={t("automation.description")} actions={add} />
      {rules.isPending ? (
        <Skeleton className="h-40 w-full" />
      ) : rules.isError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {rules.error instanceof ApiError && rules.error.status === 403 ? t("automation.noPermission") : tError(rules.error instanceof ApiError ? rules.error.code : "common.network")}
          </AlertDescription>
        </Alert>
      ) : rules.data.length === 0 ? (
        <Card>
          <EmptyState icon={Zap} title={t("automation.empty")} description={t("automation.emptyHint")} action={add} />
        </Card>
      ) : (
        <div className="grid gap-4">
          {rules.data.map((rule) => (
            <RuleCard key={rule.id} rule={rule} onEdit={() => setEditing(rule)} />
          ))}
        </div>
      )}
      {editing ? <RuleDialog rule={editing === "new" ? undefined : editing} onOpenChange={(open) => !open && setEditing(null)} /> : null}
    </div>
  );
}

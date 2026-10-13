"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, ArrowLeft, BriefcaseBusiness, Building2, CircleCheck, Clock3, LifeBuoy, Plus, Search, Trash2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { FileList } from "@/features/files/file-list";
import { useSession } from "@/features/identity/session";
import { priorities, useAssignableMembers } from "@/features/jobs/jobs-api";
import { priorityLabels } from "@/features/jobs/labels";
import { selectClass } from "@/features/sales/catalog";
import { PartyPicker, type PickedParty } from "@/features/sales/party-picker";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { api, type Schemas } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";

type Ticket = Schemas["TicketResponse"];
type TicketInput = Schemas["TicketCommand"];

const ticketsKey = ["tickets"] as const;

export const ticketStatuses = ["open", "in_progress", "waiting_client", "resolved", "closed"] as const;

export const ticketStatusLabels: Record<string, { label: TranslationKey; badge: "info" | "warning" | "secondary" | "success" | "outline" }> = {
  open: { label: "support.status.open", badge: "info" },
  in_progress: { label: "support.status.in_progress", badge: "warning" },
  waiting_client: { label: "support.status.waiting_client", badge: "secondary" },
  resolved: { label: "support.status.resolved", badge: "success" },
  closed: { label: "support.status.closed", badge: "outline" },
};

export function TicketStatusBadge({ status }: { status: string }) {
  const { t } = useI18n();
  const style = ticketStatusLabels[status] ?? ticketStatusLabels.open!;
  return <Badge variant={style.badge}>{t(style.label)}</Badge>;
}

function useTickets(view: "open" | "closed", filter: { search?: string; assigneeId?: string }) {
  return useQuery({
    queryKey: [...ticketsKey, "list", view, filter],
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/tickets", { params: { query: { view, search: filter.search || undefined, assigneeId: filter.assigneeId } } })),
    refetchInterval: 60_000,
  });
}

function useTicketMutations() {
  const queryClient = useQueryClient();
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ticketsKey });
    refreshTimelineSoon(queryClient);
  };
  return {
    save: useMutation({
      mutationFn: async ({ id, ...body }: TicketInput & { id?: string }) =>
        id ? unwrap(await api.PUT("/api/v1/jobs/tickets/{id}", { params: { path: { id } }, body })) : unwrap(await api.POST("/api/v1/jobs/tickets", { body })),
      onSuccess: refresh,
    }),
    move: useMutation({
      mutationFn: async ({ id, status }: { id: string; status: string }) => unwrap(await api.POST("/api/v1/jobs/tickets/{id}/status", { params: { path: { id } }, body: { status } })),
      onSuccess: refresh,
    }),
    convert: useMutation({
      mutationFn: async (id: string) => unwrap(await api.POST("/api/v1/jobs/tickets/{id}/convert", { params: { path: { id } } })),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        unwrap(await api.DELETE("/api/v1/jobs/tickets/{id}", { params: { path: { id } } }));
      },
      onSuccess: refresh,
    }),
  };
}

/** Open a ticket on a client's word: who called, what they need, how urgent, who takes it. */
function TicketDialog({ onOpenChange, onSaved }: { onOpenChange: (open: boolean) => void; onSaved: (ticket: Ticket) => void }) {
  const { t, tError } = useI18n();
  const { save } = useTicketMutations();
  const members = useAssignableMembers();
  const [party, setParty] = useState<PickedParty | null>(null);
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [priority, setPriority] = useState("normal");
  const [assigneeId, setAssigneeId] = useState("");
  const [requesterName, setRequesterName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!party) {
      setError(t("sales.quote.clientRequired"));
      return;
    }
    if (!subject.trim()) {
      setError(t("support.subjectRequired"));
      return;
    }
    setError(null);
    try {
      const ticket = await save.mutateAsync({
        partyId: party.id,
        subject: subject.trim(),
        description: description.trim() || null,
        priority,
        assigneeUserId: assigneeId || null,
        requesterName: requesterName.trim() || null,
      });
      toast.success(t("support.opened", { number: ticket.number }));
      onSaved(ticket);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t("support.new")}</DialogTitle>
          <DialogDescription>{t("support.newHint")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          {error ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}
          <PartyPicker id="ticket-party" value={party} onChange={setParty} />
          <FormField id="ticket-subject" label={t("support.field.subject")} placeholder={t("support.field.subjectPlaceholder")} value={subject} onChange={(event) => setSubject(event.target.value)} maxLength={200} />
          <div className="grid gap-1.5">
            <Label htmlFor="ticket-description" className="text-[13px] font-medium">
              {t("support.field.description")}
            </Label>
            <Textarea id="ticket-description" rows={4} maxLength={5000} value={description} onChange={(event) => setDescription(event.target.value)} />
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="grid gap-1.5">
              <Label htmlFor="ticket-priority" className="text-[13px] font-medium">
                {t("jobs.field.priority")}
              </Label>
              <select id="ticket-priority" className={selectClass} value={priority} onChange={(event) => setPriority(event.target.value)}>
                {priorities.map((option) => (
                  <option key={option} value={option}>
                    {t(priorityLabels[option]!)}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="ticket-assignee" className="text-[13px] font-medium">
                {t("support.field.assignee")}
              </Label>
              <select id="ticket-assignee" className={selectClass} value={assigneeId} onChange={(event) => setAssigneeId(event.target.value)}>
                <option value="">{t("support.unassigned")}</option>
                {(members.data ?? []).map((member) => (
                  <option key={member.userId} value={member.userId}>
                    {member.fullName}
                  </option>
                ))}
              </select>
            </div>
            <FormField id="ticket-requester" label={t("support.field.requester")} value={requesterName} onChange={(event) => setRequesterName(event.target.value)} maxLength={200} />
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={save.isPending}>
              {save.isPending ? t("common.saving") : t("support.open")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

/** The support queue: most urgent first, with who asked, who has it, and whether anyone has answered. */
export function SupportPage() {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const canWrite = hasPermission(session?.permissions, permissions.jobs.ticketsWrite);
  const [view, setView] = useState<"open" | "closed">("open");
  const [mine, setMine] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [adding, setAdding] = useState(false);
  const tickets = useTickets(view, { search, assigneeId: mine ? session?.userId : undefined });

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const add = canWrite ? (
    <Button onClick={() => setAdding(true)}>
      <Plus /> {t("support.new")}
    </Button>
  ) : null;

  return (
    <div className="grid gap-6">
      <PageHeader title={t("support.title")} description={t("support.description")} actions={add} />

      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap items-center gap-3">
          <div role="tablist" aria-label={t("support.title")} className="bg-muted inline-flex rounded-lg p-0.5">
            {(["open", "closed"] as const).map((option) => (
              <button
                key={option}
                type="button"
                role="tab"
                aria-selected={view === option}
                onClick={() => setView(option)}
                className={cn(
                  "focus-visible:ring-ring/40 h-8 cursor-pointer rounded-md px-3 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                  view === option ? "bg-card text-foreground font-medium shadow-xs" : "text-muted-foreground hover:text-foreground",
                )}
              >
                {t(option === "open" ? "support.view.open" : "support.view.closed")}
              </button>
            ))}
          </div>
          <label className="flex cursor-pointer items-center gap-2 text-sm">
            <input type="checkbox" className="accent-primary size-4" checked={mine} onChange={(event) => setMine(event.target.checked)} />
            {t("support.onlyMine")}
          </label>
        </div>
        <div className="relative w-full lg:max-w-xs">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
          <Input type="search" className="h-9 pl-9" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={t("support.search")} aria-label={t("support.search")} />
        </div>
      </div>

      {tickets.isPending ? (
        <Skeleton className="h-60 w-full" />
      ) : tickets.isError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {tickets.error instanceof ApiError && tickets.error.status === 403 ? t("support.noPermission") : tError(tickets.error instanceof ApiError ? tickets.error.code : "common.network")}
          </AlertDescription>
        </Alert>
      ) : tickets.data.length === 0 ? (
        <Card>
          <EmptyState icon={LifeBuoy} title={t(view === "open" ? "support.empty" : "support.emptyClosed")} description={view === "open" && !search && !mine ? t("support.emptyHint") : undefined} action={view === "open" && !search && !mine ? add : undefined} />
        </Card>
      ) : (
        <Card className="py-0">
          <CardContent className="px-0">
            <ul className="divide-border divide-y">
              {tickets.data.map((ticket) => (
                <li key={ticket.id}>
                  <Link href={`/support/${ticket.id}`} className="hover:bg-muted/40 flex items-center gap-4 px-5 py-3.5 focus-visible:outline-none">
                    <div className="grid min-w-0 flex-1 gap-1">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="truncate font-medium">{ticket.subject}</span>
                        <TicketStatusBadge status={ticket.status} />
                        {ticket.priority === "urgent" || ticket.priority === "high" ? <Badge variant="destructive">{t(priorityLabels[ticket.priority]!)}</Badge> : null}
                        {ticket.responseOverdue ? (
                          <span className="text-destructive flex items-center gap-1 text-xs font-medium">
                            <Clock3 className="size-3.5" aria-hidden /> {t("support.responseOverdue")}
                          </span>
                        ) : null}
                      </span>
                      <span className="text-muted-foreground truncate text-xs">
                        <span className="font-mono">{ticket.number}</span> · {ticket.partyName} · {ticket.requesterName} · {formatDateTime(ticket.createdAt)}
                      </span>
                    </div>
                    <span className="text-muted-foreground hidden shrink-0 text-sm sm:block">{ticket.assigneeName ?? t("support.unassigned")}</span>
                  </Link>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      {adding ? (
        <TicketDialog
          onOpenChange={setAdding}
          onSaved={(ticket) => {
            setAdding(false);
            router.push(`/support/${ticket.id}`);
          }}
        />
      ) : null}
    </div>
  );
}

/** One ticket: what was asked, the conversation with the client, and the controls to move it along. */
export function TicketDetail({ id }: { id: string }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const members = useAssignableMembers();
  const ticket = useQuery({
    queryKey: [...ticketsKey, "one", id],
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/tickets/{id}", { params: { path: { id } } })),
    // The conversation moves the status a moment after a message is sent.
    refetchInterval: 15_000,
  });
  const { save, move, convert, remove } = useTicketMutations();
  const granted = session?.permissions;
  const canWrite = hasPermission(granted, permissions.jobs.ticketsWrite);
  const canConvert = canWrite && hasPermission(granted, permissions.jobs.workOrdersWrite);
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  const back = (
    <Link href="/support" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("support.title")}
    </Link>
  );

  if (ticket.isPending) return <Skeleton className="h-96 w-full" />;
  if (ticket.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{tError(ticket.error instanceof ApiError ? ticket.error.code : "common.network")}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const data = ticket.data;
  const change = (patch: Partial<Pick<TicketInput, "priority" | "assigneeUserId">>) =>
    save.mutate(
      { id, partyId: data.partyId, subject: data.subject, description: data.description, priority: data.priority, assigneeUserId: data.assigneeUserId, requesterName: data.requesterName, ...patch },
      { onError: fail },
    );

  return (
    <div className="grid gap-6">
      {back}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="grid gap-1.5">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-muted-foreground font-mono text-xs">{data.number}</span>
            <TicketStatusBadge status={data.status} />
            {data.responseOverdue ? (
              <span className="text-destructive flex items-center gap-1 text-xs font-medium">
                <Clock3 className="size-3.5" aria-hidden /> {t("support.responseOverdue")}
              </span>
            ) : null}
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{data.subject}</h1>
          <Link href={`/crm/parties/${data.partyId}`} className="text-primary flex items-center gap-1.5 text-sm underline-offset-4 hover:underline">
            <Building2 className="size-4" aria-hidden /> {data.partyName}
          </Link>
        </div>
        {canWrite ? (
          <div className="flex flex-wrap gap-2">
            {data.workOrderId ? (
              <Link href={`/jobs/${data.workOrderId}`} className={buttonVariants({ variant: "outline" })}>
                <BriefcaseBusiness /> {t("support.openWorkOrder")}
              </Link>
            ) : canConvert ? (
              <Button
                variant="outline"
                disabled={convert.isPending}
                onClick={() => convert.mutate(id, { onSuccess: (result) => result.workOrderId && router.push(`/jobs/${result.workOrderId}`), onError: fail })}
              >
                <BriefcaseBusiness /> {t("support.convert")}
              </Button>
            ) : null}
            {data.status !== "resolved" && data.status !== "closed" ? (
              <Button disabled={move.isPending} onClick={() => move.mutate({ id, status: "resolved" }, { onSuccess: () => toast.success(t("support.resolvedToast")), onError: fail })}>
                <CircleCheck /> {t("support.resolve")}
              </Button>
            ) : null}
          </div>
        ) : null}
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="grid content-start gap-6">
          {data.description ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("support.request")}</CardTitle>
                <CardDescription>{t(data.fromClient ? "support.fromPortal" : "support.byStaff", { name: data.requesterName, date: formatDateTime(data.createdAt) })}</CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm whitespace-pre-wrap">{data.description}</p>
              </CardContent>
            </Card>
          ) : null}
          <Card>
            <CardHeader>
              <CardTitle>{t("support.thread")}</CardTitle>
              <CardDescription>{t("support.threadHint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed subjectType="ticket" subjectId={id} canWriteNotes={hasPermission(granted, permissions.timeline.notesWrite)} clientThread />
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card>
            <CardContent className="grid gap-4">
              <div className="grid gap-1.5">
                <Label htmlFor="ticket-status" className="text-[13px] font-medium">
                  {t("sales.column.status")}
                </Label>
                <select id="ticket-status" className={selectClass} disabled={!canWrite} value={data.status} onChange={(event) => move.mutate({ id, status: event.target.value }, { onError: fail })}>
                  {ticketStatuses.map((status) => (
                    <option key={status} value={status}>
                      {t(ticketStatusLabels[status]!.label)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="grid gap-1.5">
                <Label htmlFor="ticket-owner" className="text-[13px] font-medium">
                  {t("support.field.assignee")}
                </Label>
                <select id="ticket-owner" className={selectClass} disabled={!canWrite} value={data.assigneeUserId ?? ""} onChange={(event) => change({ assigneeUserId: event.target.value || null })}>
                  <option value="">{t("support.unassigned")}</option>
                  {(members.data ?? []).map((member) => (
                    <option key={member.userId} value={member.userId}>
                      {member.fullName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="grid gap-1.5">
                <Label htmlFor="ticket-prio" className="text-[13px] font-medium">
                  {t("jobs.field.priority")}
                </Label>
                <select id="ticket-prio" className={selectClass} disabled={!canWrite} value={data.priority} onChange={(event) => change({ priority: event.target.value })}>
                  {priorities.map((option) => (
                    <option key={option} value={option}>
                      {t(priorityLabels[option]!)}
                    </option>
                  ))}
                </select>
              </div>
              <dl className="text-muted-foreground grid gap-1 text-xs">
                <div>
                  {t("support.firstResponse")}:{" "}
                  <span className={cn("text-foreground", data.responseOverdue && "text-destructive font-medium")}>
                    {data.firstResponseAt ? formatDateTime(data.firstResponseAt) : t("support.responseDue", { date: formatDateTime(data.responseDueAt) })}
                  </span>
                </div>
                {data.resolvedAt ? (
                  <div>
                    {t("support.resolvedAt")}: <span className="text-foreground">{formatDateTime(data.resolvedAt)}</span>
                  </div>
                ) : null}
              </dl>
            </CardContent>
          </Card>

          {hasPermission(granted, permissions.files.read) ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("files.title")}</CardTitle>
                <CardDescription>{t("support.filesHint")}</CardDescription>
              </CardHeader>
              <CardContent>
                <FileList subjectType="ticket" subjectId={id} canWrite={hasPermission(granted, permissions.files.write)} shareable />
              </CardContent>
            </Card>
          ) : null}

          {canWrite ? (
            <Button
              variant="ghost"
              className="text-destructive justify-self-start"
              onClick={() => {
                if (!window.confirm(t("support.deleteConfirm", { number: data.number }))) return;
                remove.mutate(id, { onSuccess: () => router.push("/support"), onError: fail });
              }}
            >
              <Trash2 /> {t("support.delete")}
            </Button>
          ) : null}
        </div>
      </div>
    </div>
  );
}

"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, ArrowLeft, ChevronRight, CircleCheck, LifeBuoy, Plus } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { TicketStatusBadge } from "@/features/support/support";
import { ApiError, unwrap } from "@/lib/api/errors";
import { portalApi } from "@/lib/api/portal-client";
import { formatDateTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { FileBox, PortalError, Thread } from "./portal-parts";

const ticketsKey = ["portal", "tickets"] as const;

/** The form is the page's first thing when there is nothing yet, and one click away otherwise. */
function NewTicket({ onDone }: { onDone: (id: string) => void }) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [urgent, setUrgent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const open = useMutation({
    mutationFn: async () => unwrap(await portalApi.POST("/api/v1/jobs/portal/tickets", { body: { subject: subject.trim(), description: description.trim() || null, urgent } })),
    onSuccess: (ticket) => {
      void queryClient.invalidateQueries({ queryKey: ticketsKey });
      toast.success(t("portal.support.sent", { number: ticket.number }));
      onDone(ticket.id);
    },
    onError: (failure) => setError(tError(failure instanceof ApiError ? failure.code : "common.network")),
  });

  return (
    <form
      className="grid gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        setError(null);
        if (subject.trim()) open.mutate();
      }}
    >
      {error ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
      <FormField id="support-subject" required label={t("portal.support.subject")} placeholder={t("portal.support.subjectPlaceholder")} value={subject} onChange={(event) => setSubject(event.target.value)} maxLength={200} className="text-base md:text-sm" />
      <div className="grid gap-1.5">
        <Label htmlFor="support-description" className="text-[13px] font-medium">
          {t("portal.support.details")}
        </Label>
        <Textarea id="support-description" rows={4} maxLength={5000} value={description} onChange={(event) => setDescription(event.target.value)} className="text-base md:text-sm" placeholder={t("portal.support.detailsPlaceholder")} />
      </div>
      <label className="flex cursor-pointer items-center gap-2 text-sm">
        <input type="checkbox" className="accent-primary size-4" checked={urgent} onChange={(event) => setUrgent(event.target.checked)} />
        {t("portal.support.urgent")}
      </label>
      <div className="flex justify-end">
        <Button type="submit" disabled={open.isPending || !subject.trim()}>
          {open.isPending ? t("portal.thread.sending") : t("portal.support.send")}
        </Button>
      </div>
    </form>
  );
}

/** The client's requests for help: ask, then follow the answer. */
export function PortalSupport() {
  const { t } = useI18n();
  const router = useRouter();
  const tickets = useQuery({ queryKey: ticketsKey, queryFn: async () => unwrap(await portalApi.GET("/api/v1/jobs/portal/tickets")) });
  const [asking, setAsking] = useState(false);

  const none = (tickets.data ?? []).length === 0;
  return (
    <div className="grid gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="grid gap-1">
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{t("portal.support.title")}</h1>
          <p className="text-muted-foreground">{t("portal.support.description")}</p>
        </div>
        {!none && !asking ? (
          <Button onClick={() => setAsking(true)}>
            <Plus /> {t("portal.support.new")}
          </Button>
        ) : null}
      </div>

      {tickets.isPending ? (
        <Skeleton className="h-40 w-full" />
      ) : tickets.isError ? (
        <PortalError error={tickets.error} />
      ) : (
        <>
          {asking || none ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("portal.support.new")}</CardTitle>
                <CardDescription>{t("portal.support.newHint")}</CardDescription>
              </CardHeader>
              <CardContent>
                <NewTicket onDone={(id) => router.push(`/portal/support/${id}`)} />
              </CardContent>
            </Card>
          ) : null}
          {none ? (
            <EmptyState icon={LifeBuoy} title={t("portal.support.empty")} className="py-4" />
          ) : (
            <Card className="py-0">
              <CardContent className="px-0">
                <ul className="divide-border divide-y">
                  {tickets.data.map((ticket) => (
                    <li key={ticket.id}>
                      <Link href={`/portal/support/${ticket.id}`} className="hover:bg-muted/40 flex items-center gap-3 px-4 py-3.5 focus-visible:outline-none">
                        <div className="grid min-w-0 flex-1 gap-1">
                          <span className="flex flex-wrap items-center gap-2">
                            <span className="font-medium">{ticket.subject}</span>
                            <TicketStatusBadge status={ticket.status} />
                            {ticket.urgent ? <Badge variant="destructive">{t("portal.support.urgentBadge")}</Badge> : null}
                          </span>
                          <span className="text-muted-foreground text-xs">
                            {ticket.number} · {formatDateTime(ticket.createdAt)}
                          </span>
                        </div>
                        <ChevronRight className="text-muted-foreground size-4 shrink-0" aria-hidden />
                      </Link>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

export function PortalTicket({ id }: { id: string }) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const ticket = useQuery({
    queryKey: [...ticketsKey, id],
    queryFn: async () => unwrap(await portalApi.GET("/api/v1/jobs/portal/tickets/{id}", { params: { path: { id } } })),
    refetchInterval: 30_000,
  });
  const close = useMutation({
    mutationFn: async () => unwrap(await portalApi.POST("/api/v1/jobs/portal/tickets/{id}/close", { params: { path: { id } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ticketsKey });
      toast.success(t("portal.support.closed"));
    },
    onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
  });
  const back = (
    <Link href="/portal/support" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("portal.support.title")}
    </Link>
  );

  if (ticket.isPending) return <Skeleton className="h-80 w-full" />;
  if (ticket.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <PortalError error={ticket.error} />
      </div>
    );
  }

  const data = ticket.data;
  return (
    <div className="grid gap-6">
      {back}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="grid gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-muted-foreground font-mono text-xs">{data.number}</span>
            <TicketStatusBadge status={data.status} />
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{data.subject}</h1>
          <p className="text-muted-foreground text-sm">{t("portal.support.openedBy", { name: data.requesterName, date: formatDateTime(data.createdAt) })}</p>
        </div>
        {data.status !== "closed" ? (
          <Button variant="outline" disabled={close.isPending} onClick={() => close.mutate()}>
            <CircleCheck /> {t("portal.support.close")}
          </Button>
        ) : null}
      </div>

      {data.status === "resolved" ? (
        <Alert>
          <CircleCheck />
          <AlertDescription>{t("portal.support.resolvedHint")}</AlertDescription>
        </Alert>
      ) : null}

      {data.description ? (
        <Card>
          <CardContent>
            <p className="text-sm whitespace-pre-wrap">{data.description}</p>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("portal.thread.title")}</CardTitle>
          <CardDescription>{t("portal.support.threadHint")}</CardDescription>
        </CardHeader>
        <CardContent>
          <Thread subjectType="ticket" subjectId={id} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("portal.files.title")}</CardTitle>
          <CardDescription>{t("portal.support.filesHint")}</CardDescription>
        </CardHeader>
        <CardContent>
          <FileBox subjectType="ticket" subjectId={id} uploadLabel={t("portal.files.send")} />
        </CardContent>
      </Card>
    </div>
  );
}

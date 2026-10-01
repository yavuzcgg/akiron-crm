"use client";

import { AlertCircle, CalendarCheck2, CalendarPlus, Check, Palmtree, X } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { useSession } from "@/features/identity/session";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatCalendarDate, formatNumber, initials } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { leaveTypes, useLeaveCalendar, useLeaveMutations, useMyLeave, usePendingLeave, type Leave } from "./people-api";

const typeLabels: Record<string, TranslationKey> = {
  annual: "people.leave.type.annual",
  sick: "people.leave.type.sick",
  excuse: "people.leave.type.excuse",
  unpaid: "people.leave.type.unpaid",
  other: "people.leave.type.other",
};

const statusBadge: Record<string, { variant: "warning" | "success" | "destructive" | "secondary"; label: TranslationKey }> = {
  pending: { variant: "warning", label: "people.leave.status.pending" },
  approved: { variant: "success", label: "people.leave.status.approved" },
  rejected: { variant: "destructive", label: "people.leave.status.rejected" },
  cancelled: { variant: "secondary", label: "people.leave.status.cancelled" },
};

const selectClass =
  "border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 h-10 w-full rounded-lg border px-3 text-sm outline-none focus-visible:ring-3";

function Dates({ leave }: { leave: Leave }) {
  const { t } = useI18n();
  const range = leave.startDate === leave.endDate ? formatCalendarDate(leave.startDate) : `${formatCalendarDate(leave.startDate)} – ${formatCalendarDate(leave.endDate)}`;
  return (
    <span className="tabular-nums">
      {range} · {t(leave.halfDay ? "people.leave.halfDay" : "people.leave.days", { days: formatNumber(leave.days) })}
    </span>
  );
}

function RequestForm({ onDone }: { onDone: () => void }) {
  const { t, tError } = useI18n();
  const { submit } = useLeaveMutations();
  const [type, setType] = useState<string>("annual");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [halfDay, setHalfDay] = useState(false);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  return (
    <form
      className="grid gap-4"
      noValidate
      onSubmit={async (event) => {
        event.preventDefault();
        if (!startDate || (!halfDay && !endDate)) {
          setError(t("people.leave.datesRequired"));
          return;
        }
        setError(null);
        try {
          await submit.mutateAsync({ type, startDate, endDate: halfDay ? startDate : endDate, halfDay, note: note.trim() || null });
          toast.success(t("people.leave.submitted"));
          onDone();
        } catch (failure) {
          setError(tError(failure instanceof ApiError ? failure.code : "common.network", failure instanceof ApiError ? failure.params : undefined));
        }
      }}
    >
      {error ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
      <div className="grid gap-1.5">
        <Label htmlFor="leave-type" className="text-[13px] font-medium">
          {t("people.leave.type")}
        </Label>
        <select id="leave-type" className={selectClass} value={type} onChange={(event) => setType(event.target.value)}>
          {leaveTypes.map((option) => (
            <option key={option} value={option}>
              {t(typeLabels[option]!)}
            </option>
          ))}
        </select>
      </div>
      <label className="flex cursor-pointer items-center gap-2 text-sm">
        <input type="checkbox" className="accent-primary size-4" checked={halfDay} onChange={(event) => setHalfDay(event.target.checked)} />
        {t("people.leave.halfDayOption")}
      </label>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField id="leave-start" type="date" label={t(halfDay ? "people.leave.date" : "people.leave.start")} value={startDate} onChange={(event) => setStartDate(event.target.value)} />
        {halfDay ? null : (
          <FormField id="leave-end" type="date" label={t("people.leave.end")} min={startDate || undefined} value={endDate} onChange={(event) => setEndDate(event.target.value)} />
        )}
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="leave-note" className="text-[13px] font-medium">
          {t("people.leave.note")}
        </Label>
        <Textarea id="leave-note" rows={2} maxLength={500} value={note} onChange={(event) => setNote(event.target.value)} placeholder={t("people.leave.notePlaceholder")} />
      </div>
      <p className="text-muted-foreground text-[13px]">{t("people.leave.workingDaysHint")}</p>
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onDone}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" disabled={submit.isPending}>
          {submit.isPending ? t("common.saving") : t("people.leave.submit")}
        </Button>
      </div>
    </form>
  );
}

function Approvals() {
  const { t, tError } = useI18n();
  const pending = usePendingLeave(true);
  const { decide } = useLeaveMutations();
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  if (pending.isPending) return <Skeleton className="h-16 w-full" />;
  const items = pending.data ?? [];
  if (items.length === 0) return <EmptyState icon={CalendarCheck2} title={t("people.leave.noPending")} className="py-6" />;

  return (
    <ul className="divide-border divide-y">
      {items.map((leave) => (
        <li key={leave.id} className="flex flex-wrap items-center gap-3 py-3 first:pt-0 last:pb-0">
          <span className="bg-primary-soft text-primary-strong flex size-9 shrink-0 items-center justify-center rounded-full text-xs font-semibold">
            {initials(leave.userName || "?")}
          </span>
          <div className="grid min-w-0 flex-1 gap-0.5 text-sm">
            <span className="font-medium">
              {leave.userName} · {t(typeLabels[leave.type ?? "other"]!)}
            </span>
            <span className="text-muted-foreground text-xs">
              <Dates leave={leave} />
              {leave.note ? ` · ${leave.note}` : ""}
            </span>
          </div>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                const note = window.prompt(t("people.leave.rejectReason")) ?? undefined;
                decide.mutate({ id: leave.id, approve: false, note }, { onSuccess: () => toast.success(t("people.leave.rejectedToast")), onError: fail });
              }}
              disabled={decide.isPending}
            >
              <X /> {t("people.leave.reject")}
            </Button>
            <Button
              size="sm"
              onClick={() => decide.mutate({ id: leave.id, approve: true }, { onSuccess: () => toast.success(t("people.leave.approvedToast")), onError: fail })}
              disabled={decide.isPending}
            >
              <Check /> {t("people.leave.approve")}
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}

/** Who is off in the coming weeks; the reason shows only to approvers and to the person. */
function TeamCalendar() {
  const { t } = useI18n();
  const [range] = useState(() => ({ from: dayKey(new Date()), to: dayKey(new Date(Date.now() + 60 * 86_400_000)) }));
  const calendar = useLeaveCalendar(range.from, range.to);

  if (calendar.isPending) return <Skeleton className="h-16 w-full" />;
  const items = (calendar.data ?? []).filter((leave) => leave.status === "approved");
  if (items.length === 0) return <EmptyState icon={Palmtree} title={t("people.leave.nobodyOff")} className="py-6" />;

  return (
    <ul className="divide-border divide-y">
      {items.map((leave) => (
        <li key={leave.id} className="flex items-center gap-3 py-2.5 text-sm first:pt-0 last:pb-0">
          <span className="bg-muted text-muted-foreground flex size-8 shrink-0 items-center justify-center rounded-full text-[11px] font-semibold">
            {initials(leave.userName || "?")}
          </span>
          <div className="grid min-w-0 flex-1">
            <span className="truncate font-medium">
              {leave.userName}
              {leave.type ? <span className="text-muted-foreground font-normal"> · {t(typeLabels[leave.type] ?? "people.leave.type.other")}</span> : null}
            </span>
            <span className="text-muted-foreground text-xs">
              <Dates leave={leave} />
            </span>
          </div>
        </li>
      ))}
    </ul>
  );
}

export function LeavePage() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const [year] = useState(() => new Date().getFullYear());
  const mine = useMyLeave(year);
  const { cancel } = useLeaveMutations();
  const [open, setOpen] = useState(false);
  const canApprove = hasPermission(session?.permissions, permissions.people.leaveApprove);
  const balance = mine.data?.balance;
  const usedShare = useMemo(() => (balance && balance.allowance > 0 ? Math.min(100, (balance.used / balance.allowance) * 100) : 0), [balance]);

  return (
    <div className="grid gap-8">
      <PageHeader
        title={t("people.leave.title")}
        description={t("people.leave.description")}
        actions={
          <Button onClick={() => setOpen(true)}>
            <CalendarPlus /> {t("people.leave.request")}
          </Button>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="grid content-start gap-6">
          {canApprove ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("people.leave.approvals")}</CardTitle>
                <CardDescription>{t("people.leave.approvalsHint")}</CardDescription>
              </CardHeader>
              <CardContent>
                <Approvals />
              </CardContent>
            </Card>
          ) : null}

          <Card>
            <CardHeader>
              <CardTitle>{t("people.leave.mine")}</CardTitle>
            </CardHeader>
            <CardContent>
              {mine.isPending ? (
                <Skeleton className="h-16 w-full" />
              ) : (mine.data?.requests.length ?? 0) === 0 ? (
                <EmptyState icon={Palmtree} title={t("people.leave.none")} className="py-6" />
              ) : (
                <ul className="divide-border divide-y">
                  {mine.data!.requests.map((leave) => {
                    const badge = statusBadge[leave.status] ?? statusBadge.pending!;
                    const cancellable = leave.status === "pending" || (leave.status === "approved" && leave.startDate > dayKey(new Date()));
                    return (
                      <li key={leave.id} className="flex flex-wrap items-center gap-3 py-3 text-sm first:pt-0 last:pb-0">
                        <div className="grid min-w-0 flex-1 gap-0.5">
                          <span className="font-medium">{t(typeLabels[leave.type ?? "other"]!)}</span>
                          <span className="text-muted-foreground text-xs">
                            <Dates leave={leave} />
                            {leave.decisionNote ? ` · ${leave.decidedByName ?? ""}: ${leave.decisionNote}` : ""}
                          </span>
                        </div>
                        <Badge variant={badge.variant}>{t(badge.label)}</Badge>
                        {cancellable ? (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() =>
                              cancel.mutate(leave.id, {
                                onSuccess: () => toast.success(t("people.leave.cancelled")),
                                onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
                              })
                            }
                            disabled={cancel.isPending}
                          >
                            {t("people.leave.withdraw")}
                          </Button>
                        ) : null}
                      </li>
                    );
                  })}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle>{t("people.leave.balance", { year })}</CardTitle>
            </CardHeader>
            <CardContent>
              {balance ? (
                <div className="grid gap-3">
                  <p className="text-3xl font-semibold tabular-nums">
                    {formatNumber(balance.remaining)} <span className="text-muted-foreground text-base font-normal">/ {balance.allowance}</span>
                  </p>
                  <p className="text-muted-foreground text-sm">{t("people.leave.remaining")}</p>
                  <div className="bg-muted h-2 overflow-hidden rounded-full" role="presentation">
                    <div className="bg-primary h-full rounded-full" style={{ width: `${usedShare}%` }} />
                  </div>
                  <dl className="text-muted-foreground grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <dt>{t("people.leave.used")}</dt>
                      <dd className="text-foreground text-sm font-medium tabular-nums">{formatNumber(balance.used)}</dd>
                    </div>
                    <div>
                      <dt>{t("people.leave.pendingDays")}</dt>
                      <dd className="text-foreground text-sm font-medium tabular-nums">{formatNumber(balance.pending)}</dd>
                    </div>
                  </dl>
                </div>
              ) : (
                <Skeleton className="h-24 w-full" />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t("people.leave.team")}</CardTitle>
              <CardDescription>{t("people.leave.teamHint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TeamCalendar />
            </CardContent>
          </Card>
        </div>
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-md" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("people.leave.request")}</DialogTitle>
            <DialogDescription>{t("people.leave.requestHint")}</DialogDescription>
          </DialogHeader>
          {open ? <RequestForm onDone={() => setOpen(false)} /> : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

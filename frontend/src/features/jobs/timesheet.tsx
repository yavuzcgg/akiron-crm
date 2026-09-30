"use client";

import { AlertCircle, ChevronLeft, ChevronRight, Clock3 } from "lucide-react";
import Link from "next/link";
import { useMemo, useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useSession } from "@/features/identity/session";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatCalendarDate, formatDuration, formatWeekday } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { useAssignableMembers, useTimeEntries, type TimeEntry } from "./jobs-api";

/** Monday of the week <paramref name="offset"/> weeks from this one. */
function weekStart(offset: number): Date {
  const date = new Date();
  date.setHours(0, 0, 0, 0);
  const sinceMonday = (date.getDay() + 6) % 7;
  date.setDate(date.getDate() - sinceMonday + offset * 7);
  return date;
}

function addDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

/** A week of logged time: totals per day, then each day's entries. */
export function Timesheet() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const canReadAll = hasPermission(session?.permissions, permissions.jobs.timeReadAll);
  const members = useAssignableMembers();
  const [offset, setOffset] = useState(0);
  const [userId, setUserId] = useState<string>("");

  const monday = weekStart(offset);
  const days = Array.from({ length: 7 }, (_, index) => dayKey(addDays(monday, index)));
  const entries = useTimeEntries(days[0]!, days[6]!, userId || undefined);

  const byDay = useMemo(() => {
    const groups = new Map<string, TimeEntry[]>(days.map((day) => [day, []]));
    for (const entry of entries.data ?? []) groups.get(dayKey(entry.startedAt))?.push(entry);
    return groups;
    // days is derived from offset
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entries.data, offset]);

  const finished = (entries.data ?? []).filter((entry) => entry.endedAt);
  const total = finished.reduce((sum, entry) => sum + entry.minutes, 0);
  const billable = finished.filter((entry) => entry.isBillable).reduce((sum, entry) => sum + entry.minutes, 0);
  const todayKey = dayKey(new Date());

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-1">
          <Button variant="outline" size="icon-sm" onClick={() => setOffset(offset - 1)} aria-label={t("jobs.timesheet.previous")}>
            <ChevronLeft />
          </Button>
          <Button variant="outline" size="icon-sm" onClick={() => setOffset(offset + 1)} disabled={offset >= 0} aria-label={t("jobs.timesheet.next")}>
            <ChevronRight />
          </Button>
        </div>
        <p className="text-sm font-medium tabular-nums">
          {formatCalendarDate(days[0]!)} – {formatCalendarDate(days[6]!)}
        </p>
        {offset !== 0 ? (
          <Button variant="link" size="sm" onClick={() => setOffset(0)}>
            {t("jobs.timesheet.thisWeek")}
          </Button>
        ) : null}
        {canReadAll ? (
          <select
            className="border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 ml-auto h-9 rounded-lg border px-3 text-sm outline-none focus-visible:ring-3"
            value={userId}
            onChange={(event) => setUserId(event.target.value)}
            aria-label={t("jobs.timesheet.person")}
          >
            <option value="">{t("jobs.timesheet.me")}</option>
            {(members.data ?? [])
              .filter((member) => member.userId !== session?.userId)
              .map((member) => (
                <option key={member.userId} value={member.userId}>
                  {member.fullName}
                </option>
              ))}
          </select>
        ) : null}
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-8">
        {days.map((day) => {
          const minutes = (byDay.get(day) ?? []).filter((entry) => entry.endedAt).reduce((sum, entry) => sum + entry.minutes, 0);
          return (
            <div key={day} className={cn("bg-card rounded-lg border p-3", day === todayKey && "border-primary/50")}>
              <p className="text-muted-foreground text-xs">{formatWeekday(day)}</p>
              <p className={cn("text-base font-semibold tabular-nums", minutes === 0 && "text-muted-foreground font-normal")}>
                {minutes === 0 ? "—" : formatDuration(minutes)}
              </p>
            </div>
          );
        })}
        <div className="bg-primary-soft text-primary-strong col-span-2 rounded-lg p-3 sm:col-span-4 lg:col-span-1">
          <p className="text-xs">{t("jobs.timesheet.week")}</p>
          <p className="text-base font-semibold tabular-nums">{formatDuration(total)}</p>
          <p className="text-xs tabular-nums">{t("jobs.timesheet.billable", { duration: formatDuration(billable) })}</p>
        </div>
      </div>

      {entries.isPending ? (
        <Skeleton className="h-48 w-full" />
      ) : entries.isError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{tError(entries.error instanceof ApiError ? entries.error.code : "common.network")}</AlertDescription>
        </Alert>
      ) : finished.length === 0 ? (
        <EmptyState icon={Clock3} title={t("jobs.timesheet.empty")} description={t("jobs.timesheet.emptyHint")} />
      ) : (
        <div className="grid gap-4">
          {days
            .filter((day) => (byDay.get(day) ?? []).some((entry) => entry.endedAt))
            .reverse()
            .map((day) => (
              <section key={day} className="bg-card rounded-xl border">
                <h2 className="text-muted-foreground border-b px-4 py-2.5 text-xs font-semibold tracking-[0.02em] uppercase">{formatWeekday(day)}</h2>
                <ul className="divide-border divide-y">
                  {(byDay.get(day) ?? [])
                    .filter((entry) => entry.endedAt)
                    .map((entry) => (
                      <li key={entry.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                        <span className="text-muted-foreground w-24 shrink-0 font-mono text-xs">{entry.workOrderNumber}</span>
                        <div className="grid min-w-0 flex-1">
                          <Link href={`/jobs/${entry.workOrderId}`} className="truncate font-medium underline-offset-4 hover:underline">
                            {entry.workOrderTitle}
                          </Link>
                          <span className="text-muted-foreground truncate text-xs">
                            {[entry.partyName, entry.note].filter(Boolean).join(" · ") || " "}
                          </span>
                        </div>
                        {!entry.isBillable ? <span className="text-muted-foreground text-xs">{t("jobs.time.notBillable")}</span> : null}
                        <span className="font-semibold tabular-nums">{formatDuration(entry.minutes)}</span>
                      </li>
                    ))}
                </ul>
              </section>
            ))}
        </div>
      )}
    </div>
  );
}

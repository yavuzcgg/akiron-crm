"use client";

import { Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatCalendarDate, formatDuration } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { useTimeEntries, useTimeEntryMutations, type WorkOrder } from "./jobs-api";
import { parseDuration } from "./duration";
import { TimerButton } from "./timer";

const daysShown = 60;

/** Time on one work order: totals, the timer, a quick "log time" form and the latest entries. */
export function TimePanel({ workOrder, canTrack }: { workOrder: WorkOrder; canTrack: boolean }) {
  const { t, tError } = useI18n();
  // Fixed for the life of the panel: the range must not move between renders.
  const [{ today, from }] = useState(() => ({
    today: dayKey(new Date()),
    from: dayKey(new Date(Date.now() - (daysShown - 1) * 86_400_000)),
  }));
  const entries = useTimeEntries(from, today);
  const { log, remove } = useTimeEntryMutations();
  const [date, setDate] = useState(today);
  const [duration, setDuration] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  const mine = (entries.data ?? []).filter((entry) => entry.workOrderId === workOrder.id && entry.endedAt).reverse();

  return (
    <div className="grid gap-4">
      <dl className="grid grid-cols-2 gap-3">
        <div className="bg-muted/50 rounded-lg p-3">
          <dt className="text-muted-foreground text-xs">{t("jobs.time.total")}</dt>
          <dd className="text-lg font-semibold tabular-nums">{formatDuration(workOrder.minutesLogged)}</dd>
        </div>
        <div className="bg-muted/50 rounded-lg p-3">
          <dt className="text-muted-foreground text-xs">{t("jobs.time.billable")}</dt>
          <dd className="text-lg font-semibold tabular-nums">{formatDuration(workOrder.billableMinutes)}</dd>
        </div>
      </dl>

      {canTrack ? (
        <>
          <TimerButton workOrderId={workOrder.id} />
          <form
            className="grid gap-2 border-t pt-4"
            onSubmit={(event) => {
              event.preventDefault();
              const minutes = parseDuration(duration);
              if (!minutes || minutes < 1 || minutes > 1440) {
                setError(t("jobs.time.durationInvalid"));
                return;
              }
              setError(null);
              log.mutate(
                { workOrderId: workOrder.id, date, minutes, note: note.trim() || null, isBillable: true },
                {
                  onSuccess: () => {
                    setDuration("");
                    setNote("");
                    toast.success(t("jobs.time.logged", { duration: formatDuration(minutes) }));
                  },
                  onError: fail,
                },
              );
            }}
          >
            <p className="text-[13px] font-medium">{t("jobs.time.logTitle")}</p>
            <div className="grid grid-cols-[1fr_96px] gap-2">
              <div className="grid gap-1">
                <Label htmlFor="log-date" className="sr-only">
                  {t("jobs.time.date")}
                </Label>
                <Input id="log-date" type="date" value={date} max={today} onChange={(event) => setDate(event.target.value)} className="h-9" />
              </div>
              <div className="grid gap-1">
                <Label htmlFor="log-duration" className="sr-only">
                  {t("jobs.time.duration")}
                </Label>
                <Input
                  id="log-duration"
                  inputMode="decimal"
                  placeholder="1:30"
                  value={duration}
                  onChange={(event) => setDuration(event.target.value)}
                  aria-invalid={!!error}
                  aria-describedby={error ? "log-error" : "log-hint"}
                  className="h-9 tabular-nums"
                />
              </div>
            </div>
            <Input value={note} onChange={(event) => setNote(event.target.value)} placeholder={t("jobs.time.notePlaceholder")} maxLength={500} className="h-9" aria-label={t("jobs.time.note")} />
            {error ? (
              <p id="log-error" role="alert" className="text-destructive text-[13px]">
                {error}
              </p>
            ) : (
              <p id="log-hint" className="text-muted-foreground text-[13px]">
                {t("jobs.time.durationHint")}
              </p>
            )}
            <Button type="submit" variant="secondary" size="sm" className="justify-self-start" disabled={log.isPending || !duration.trim()}>
              {t("jobs.time.log")}
            </Button>
          </form>
        </>
      ) : null}

      {mine.length > 0 ? (
        <div className="grid gap-1 border-t pt-4">
          <p className="text-muted-foreground text-xs font-medium">{t("jobs.time.recent")}</p>
          <ul className="divide-border divide-y">
            {mine.slice(0, 8).map((entry) => (
              <li key={entry.id} className="group flex items-center gap-2 py-2 text-sm">
                <span className="text-muted-foreground w-24 shrink-0 text-xs tabular-nums">{formatCalendarDate(dayKey(entry.startedAt))}</span>
                <span className="min-w-0 flex-1 truncate">{entry.note ?? entry.userName}</span>
                <span className="font-medium tabular-nums">{formatDuration(entry.minutes)}</span>
                <Button
                  variant="ghost"
                  size="icon-xs"
                  className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100"
                  onClick={() => remove.mutate(entry.id, { onError: fail })}
                  aria-label={t("jobs.time.remove")}
                >
                  <Trash2 />
                </Button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

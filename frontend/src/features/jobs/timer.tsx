"use client";

import { Pause, Play, Timer } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useSession } from "@/features/identity/session";
import { ApiError } from "@/lib/api/errors";
import { formatElapsed } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { useRunningTimer, useTimerMutations } from "./jobs-api";

/** Seconds since <paramref name="startedAt"/>, ticking once a second while mounted. */
function useElapsed(startedAt: string | undefined) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!startedAt) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [startedAt]);
  return startedAt ? (now - new Date(startedAt).getTime()) / 1000 : 0;
}

/** The running timer in the top bar, visible on every page, with a stop button. */
export function TimerChip() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const canTrack = hasPermission(session?.permissions, permissions.jobs.timeWrite);
  const timer = useRunningTimer(canTrack);
  const { stop } = useTimerMutations();
  const running = timer.data ?? null;
  const elapsed = useElapsed(running?.startedAt);

  if (!running) return null;

  return (
    <div className="bg-success-soft text-success flex h-8 items-center gap-1 rounded-full pr-1 pl-3 text-sm">
      <Timer className="size-4 motion-safe:animate-pulse" aria-hidden />
      <Link href={`/jobs/${running.workOrderId}`} className="max-w-40 truncate font-medium underline-offset-4 hover:underline" title={running.workOrderTitle}>
        {running.workOrderNumber}
      </Link>
      <span className="tabular-nums" aria-live="off">
        {formatElapsed(elapsed)}
      </span>
      <Button
        variant="ghost"
        size="icon-xs"
        className="hover:bg-success/15 text-success rounded-full"
        onClick={() =>
          stop.mutate(undefined, {
            onSuccess: (entry) => toast.success(t("jobs.timer.stopped", { number: entry.workOrderNumber })),
            onError: (error) => toast.error(tError(error instanceof ApiError ? error.code : "common.network")),
          })
        }
        disabled={stop.isPending}
        aria-label={t("jobs.timer.stop")}
      >
        <Pause />
      </Button>
    </div>
  );
}

/** Start/stop for one work order, on its page. */
export function TimerButton({ workOrderId }: { workOrderId: string }) {
  const { t, tError } = useI18n();
  const timer = useRunningTimer();
  const { start, stop } = useTimerMutations();
  const runningHere = timer.data?.workOrderId === workOrderId;
  const elapsed = useElapsed(runningHere ? timer.data?.startedAt : undefined);
  const fail = (error: unknown) => toast.error(tError(error instanceof ApiError ? error.code : "common.network"));

  return runningHere ? (
    <Button variant="outline" className={cn("border-success/40 text-success")} onClick={() => stop.mutate(undefined, { onError: fail })} disabled={stop.isPending}>
      <Pause /> {t("jobs.timer.stop")} <span className="tabular-nums">{formatElapsed(elapsed)}</span>
    </Button>
  ) : (
    <Button variant="outline" onClick={() => start.mutate(workOrderId, { onError: fail })} disabled={start.isPending}>
      <Play /> {t(timer.data ? "jobs.timer.switch" : "jobs.timer.start")}
    </Button>
  );
}

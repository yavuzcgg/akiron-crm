"use client";

import { AlarmClock, CalendarClock, CheckSquare, Clock3, Flag } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { formatCalendarDate, formatDuration, initials } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import type { WorkOrderCard } from "./jobs-api";
import { priorityLabels } from "./labels";

/** Today as YYYY-MM-DD in local time, to compare with calendar due dates. */
function today(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}

export function PriorityBadge({ priority }: { priority: string }) {
  const { t } = useI18n();
  // Normal is the default and stays quiet; the others earn a label (never colour alone).
  if (priority === "normal") return null;
  const variant = priority === "urgent" ? "destructive" : priority === "high" ? "warning" : "secondary";
  return (
    <Badge variant={variant}>
      <Flag aria-hidden /> {t(priorityLabels[priority] ?? "jobs.priority.normal")}
    </Badge>
  );
}

export function DueDate({ dueDate, done }: { dueDate: string; done: boolean }) {
  const { t } = useI18n();
  const overdue = !done && dueDate < today();
  const Icon = overdue ? AlarmClock : CalendarClock;
  return (
    <span className={cn("inline-flex items-center gap-1 text-xs tabular-nums", overdue ? "text-destructive font-medium" : "text-muted-foreground")}>
      <Icon className="size-3.5" aria-hidden />
      {overdue ? t("jobs.card.overdue", { date: formatCalendarDate(dueDate) }) : formatCalendarDate(dueDate)}
    </span>
  );
}

export function AssigneeStack({ assignees }: { assignees: WorkOrderCard["assignees"] }) {
  const { t } = useI18n();
  if (assignees.length === 0) return null;
  const shown = assignees.slice(0, 3);
  const names = assignees.map((assignee) => assignee.fullName || t("jobs.card.formerMember")).join(", ");
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <span className="flex -space-x-1.5" aria-label={t("jobs.card.assignees", { names })}>
            {shown.map((assignee) => (
              <span
                key={assignee.userId}
                className="bg-primary-soft text-primary-strong ring-card flex size-6 items-center justify-center rounded-full text-[10px] font-semibold ring-2"
              >
                {assignee.fullName ? initials(assignee.fullName) : "?"}
              </span>
            ))}
            {assignees.length > shown.length ? (
              <span className="bg-muted text-muted-foreground ring-card flex size-6 items-center justify-center rounded-full text-[10px] font-semibold ring-2">
                +{assignees.length - shown.length}
              </span>
            ) : null}
          </span>
        }
      />
      <TooltipContent>{names}</TooltipContent>
    </Tooltip>
  );
}

/** The face of a work order on the board and in lists. */
export function WorkOrderCardBody({ card }: { card: WorkOrderCard }) {
  const { t } = useI18n();
  const done = card.completedAt !== null;
  return (
    <div className="grid gap-2">
      <div className="flex items-start justify-between gap-2">
        <span className="text-muted-foreground font-mono text-[11px]">{card.number}</span>
        <PriorityBadge priority={card.priority} />
      </div>
      <p className={cn("text-sm leading-5 font-medium", done && "text-muted-foreground line-through decoration-1")}>{card.title}</p>
      {card.partyName ? <p className="text-muted-foreground -mt-1 truncate text-xs">{card.partyName}</p> : null}
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        {card.dueDate ? <DueDate dueDate={card.dueDate} done={done} /> : null}
        {card.tasksTotal > 0 ? (
          <span className="text-muted-foreground inline-flex items-center gap-1 text-xs tabular-nums" aria-label={t("jobs.card.tasks", { done: card.tasksDone, total: card.tasksTotal })}>
            <CheckSquare className="size-3.5" aria-hidden /> {card.tasksDone}/{card.tasksTotal}
          </span>
        ) : null}
        {card.minutesLogged > 0 ? (
          <span className="text-muted-foreground inline-flex items-center gap-1 text-xs tabular-nums">
            <Clock3 className="size-3.5" aria-hidden /> {formatDuration(card.minutesLogged)}
          </span>
        ) : null}
        <span className="ml-auto">
          <AssigneeStack assignees={card.assignees} />
        </span>
      </div>
    </div>
  );
}

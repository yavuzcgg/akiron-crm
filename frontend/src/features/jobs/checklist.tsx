"use client";

import { ListChecks, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useTaskMutations, type WorkOrderTask } from "./jobs-api";

/** The work order's checklist: tick, add at the bottom, remove. */
export function Checklist({ workOrderId, tasks, canWrite }: { workOrderId: string; tasks: WorkOrderTask[]; canWrite: boolean }) {
  const { t, tError } = useI18n();
  const { add, update, remove } = useTaskMutations(workOrderId);
  const [title, setTitle] = useState("");
  const fail = (error: unknown) => toast.error(tError(error instanceof ApiError ? error.code : "common.network"));
  const done = tasks.filter((task) => task.isDone).length;

  return (
    <div className="grid gap-3">
      {tasks.length > 0 ? (
        <>
          <div className="flex items-center gap-3">
            <div
              className="bg-muted h-1.5 flex-1 overflow-hidden rounded-full"
              role="progressbar"
              aria-valuemin={0}
              aria-valuemax={tasks.length}
              aria-valuenow={done}
              aria-label={t("jobs.card.tasks", { done, total: tasks.length })}
            >
              <div className="bg-success h-full rounded-full transition-[width]" style={{ width: `${(done / tasks.length) * 100}%` }} />
            </div>
            <span className="text-muted-foreground text-xs tabular-nums">
              {done}/{tasks.length}
            </span>
          </div>
          <ul className="grid gap-0.5">
            {tasks.map((task) => (
              <li key={task.id} className="group hover:bg-muted/50 flex items-center gap-2.5 rounded-md px-1.5 py-1.5">
                <input
                  id={`task-${task.id}`}
                  type="checkbox"
                  className="accent-primary size-4"
                  checked={task.isDone}
                  disabled={!canWrite}
                  onChange={(event) => update.mutate({ taskId: task.id, title: task.title, isDone: event.target.checked }, { onError: fail })}
                />
                <label htmlFor={`task-${task.id}`} className={cn("flex-1 cursor-pointer text-sm", task.isDone && "text-muted-foreground line-through")}>
                  {task.title}
                </label>
                {canWrite ? (
                  <Button
                    variant="ghost"
                    size="icon-xs"
                    className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100"
                    onClick={() => remove.mutate(task.id, { onError: fail })}
                    aria-label={t("jobs.tasks.remove", { title: task.title })}
                  >
                    <Trash2 />
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        </>
      ) : (
        <EmptyState icon={ListChecks} title={t("jobs.tasks.empty")} className="py-4" />
      )}
      {canWrite ? (
        <form
          className="flex gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (!title.trim()) return;
            add.mutate(title.trim(), { onSuccess: () => setTitle(""), onError: fail });
          }}
        >
          <Input value={title} onChange={(event) => setTitle(event.target.value)} placeholder={t("jobs.tasks.placeholder")} maxLength={300} className="h-9" aria-label={t("jobs.tasks.new")} />
          <Button type="submit" variant="outline" size="sm" className="h-9" disabled={add.isPending || !title.trim()}>
            <Plus /> {t("jobs.tasks.add")}
          </Button>
        </form>
      ) : null}
    </div>
  );
}

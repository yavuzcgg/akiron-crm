"use client";

import { AlertCircle, Download, FileText, Paperclip, Send, Upload } from "lucide-react";
import { useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { stageLabel } from "@/features/jobs/labels";
import { ApiError } from "@/lib/api/errors";
import { formatDateTime, formatFileSize } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  progressOf,
  usePortalFiles,
  usePortalSendMessage,
  usePortalThread,
  usePortalUpload,
  type ClientWorkOrder,
  type ThreadItem,
  type ThreadSubject,
} from "./portal-api";

export function PortalError({ error }: { error: unknown }) {
  const { tError } = useI18n();
  return (
    <Alert variant="destructive">
      <AlertCircle />
      <AlertDescription>{tError(error instanceof ApiError ? error.code : "common.network")}</AlertDescription>
    </Alert>
  );
}

/** Where a job stands, in the agency's own stage name; colour always comes with the label. */
export function StageBadge({ workOrder }: { workOrder: Pick<ClientWorkOrder, "stageKey" | "stageName" | "stageCategory"> }) {
  const { t } = useI18n();
  const label = stageLabel({ key: workOrder.stageKey, name: workOrder.stageName }, t);
  return <Badge variant={workOrder.stageCategory === "done" ? "default" : workOrder.stageCategory === "active" ? "secondary" : "outline"}>{label}</Badge>;
}

export function ProgressBar({ workOrder }: { workOrder: Pick<ClientWorkOrder, "tasksDone" | "tasksTotal" | "stageCategory"> }) {
  const { t } = useI18n();
  const percent = progressOf(workOrder);
  if (percent === null) return null;
  return (
    <div className="grid gap-1">
      <div
        role="progressbar"
        aria-valuenow={percent}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={t("portal.jobs.progress")}
        className="bg-muted h-1.5 overflow-hidden rounded-full"
      >
        <div className={cn("h-full rounded-full", percent === 100 ? "bg-success" : "bg-primary")} style={{ width: `${percent}%` }} />
      </div>
      <span className="text-muted-foreground text-xs tabular-nums">{t("portal.jobs.progressValue", { percent })}</span>
    </div>
  );
}

const text = (value: unknown) => (typeof value === "string" ? value : "");

function Message({ item }: { item: ThreadItem }) {
  const { t } = useI18n();
  const mine = item.actor.kind === "portal_contact";
  const payload = (item.payload ?? {}) as Record<string, unknown>;
  const who = item.actor.name ?? t("portal.thread.team");

  if (item.type.startsWith("social.content.")) {
    const line =
      item.type === "social.content.sent"
        ? t("portal.thread.contentSent")
        : item.type === "social.content.published"
          ? t("portal.thread.contentPublished")
          : payload.approved
            ? t("portal.thread.contentApproved", { name: who })
            : t("portal.thread.contentChanges", { name: who });
    return (
      <li className="text-muted-foreground grid justify-items-center gap-1 text-center text-xs">
        <span>
          {line} · <time dateTime={item.occurredAt}>{formatDateTime(item.occurredAt)}</time>
        </span>
        {text(payload.note) ? <span className="bg-muted text-foreground max-w-[85%] rounded-lg px-3 py-1.5 text-sm">{text(payload.note)}</span> : null}
      </li>
    );
  }

  if (item.type !== "timeline.note") {
    // A file crossed the line: one quiet row, not a bubble.
    const fileName = text(payload.fileName);
    const fileId = text(payload.fileId);
    return (
      <li className="text-muted-foreground flex flex-wrap items-center justify-center gap-x-1.5 gap-y-1 text-xs">
        <Paperclip className="size-3.5" aria-hidden />
        <span>{t(mine ? "portal.thread.youSentFile" : "portal.thread.teamSentFile", { name: who })}</span>
        {fileId ? (
          <a className="text-primary underline-offset-4 hover:underline" href={`/api/v1/files/portal/${fileId}/content`}>
            {fileName}
          </a>
        ) : (
          <span>{fileName}</span>
        )}
        <time dateTime={item.occurredAt}>· {formatDateTime(item.occurredAt)}</time>
      </li>
    );
  }

  return (
    <li className={cn("grid max-w-[85%] gap-1", mine ? "justify-self-end" : "justify-self-start")}>
      <p className={cn("rounded-2xl px-3.5 py-2 text-sm whitespace-pre-wrap", mine ? "bg-primary-soft text-foreground rounded-br-md" : "bg-muted rounded-bl-md")}>
        {text(payload.text)}
      </p>
      <span className={cn("text-muted-foreground text-xs", mine && "text-right")}>
        {who} · <time dateTime={item.occurredAt}>{formatDateTime(item.occurredAt)}</time>
      </span>
    </li>
  );
}

/** The conversation between the client and the team on one record: oldest at the top, the box at the bottom. */
export function Thread({ subjectType, subjectId }: { subjectType: ThreadSubject; subjectId: string }) {
  const { t, tError } = useI18n();
  const thread = usePortalThread(subjectType, subjectId);
  const send = usePortalSendMessage(subjectType, subjectId);
  const [message, setMessage] = useState("");
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!message.trim()) return;
    setError(null);
    try {
      await send.mutateAsync(message.trim());
      setMessage("");
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  const items = [...(thread.data?.items ?? [])].reverse();

  return (
    <div className="grid gap-4">
      {thread.isPending ? (
        <Skeleton className="h-24 w-full" />
      ) : thread.isError ? (
        <PortalError error={thread.error} />
      ) : items.length === 0 ? (
        <p className="text-muted-foreground py-4 text-center text-sm">{t("portal.thread.empty")}</p>
      ) : (
        <ol className="grid gap-3" aria-live="polite">
          {items.map((item) => (
            <Message key={item.id} item={item} />
          ))}
        </ol>
      )}
      <form onSubmit={submit} className="bg-muted/40 focus-within:border-ring focus-within:ring-ring/20 grid gap-2 rounded-xl border p-3 focus-within:ring-3">
        <Textarea
          className="min-h-14 resize-none border-0 bg-transparent p-1 text-base shadow-none focus-visible:ring-0 md:text-sm dark:bg-transparent"
          aria-label={t("portal.thread.placeholder")}
          placeholder={t("portal.thread.placeholder")}
          value={message}
          onChange={(event) => setMessage(event.target.value)}
          maxLength={4000}
          rows={2}
        />
        {error ? <p className="text-destructive text-sm">{error}</p> : null}
        <div className="flex justify-end">
          <Button type="submit" size="sm" disabled={send.isPending || !message.trim()}>
            <Send /> {send.isPending ? t("portal.thread.sending") : t("portal.thread.send")}
          </Button>
        </div>
      </form>
    </div>
  );
}

/** Files the team shared and files the client sent, with an upload button. */
export function FileBox({ subjectType, subjectId, uploadLabel }: { subjectType: ThreadSubject; subjectId: string; uploadLabel: string }) {
  const { t, tError } = useI18n();
  const files = usePortalFiles(subjectType, subjectId);
  const upload = usePortalUpload(subjectType, subjectId);
  const input = useRef<HTMLInputElement>(null);

  const onPick = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    upload.mutate(file, {
      onSuccess: (saved) => toast.success(t("portal.files.sent", { name: saved.fileName })),
      onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network", failure instanceof ApiError ? failure.params : undefined)),
    });
  };

  return (
    <div className="grid gap-3">
      {files.isPending ? (
        <Skeleton className="h-10 w-full" />
      ) : (files.data ?? []).length === 0 ? (
        <p className="text-muted-foreground text-sm">{t("portal.files.empty")}</p>
      ) : (
        <ul className="divide-border divide-y">
          {files.data!.map((file) => (
            <li key={file.id} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0">
              <span className="bg-primary-soft text-primary flex size-9 shrink-0 items-center justify-center rounded-lg">
                <FileText className="size-4" aria-hidden />
              </span>
              <div className="grid min-w-0 flex-1">
                <span className="truncate text-sm font-medium">{file.fileName}</span>
                <span className="text-muted-foreground text-xs">
                  {formatFileSize(file.sizeBytes)} · {formatDateTime(file.uploadedAt)}
                  {file.uploadedByClient ? ` · ${t("portal.files.yours")}` : ""}
                </span>
              </div>
              <a href={`/api/v1/files/portal/${file.id}/content`} className={buttonVariants({ variant: "ghost", size: "icon" })} aria-label={t("portal.files.download", { name: file.fileName })}>
                <Download />
              </a>
            </li>
          ))}
        </ul>
      )}
      <input ref={input} type="file" className="hidden" onChange={onPick} />
      <Button variant="outline" className="justify-self-start" onClick={() => input.current?.click()} disabled={upload.isPending}>
        <Upload /> {upload.isPending ? t("files.uploading") : uploadLabel}
      </Button>
    </div>
  );
}

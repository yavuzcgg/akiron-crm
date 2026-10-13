"use client";

import { AlertCircle, ArrowLeft, Building2, CircleCheck, ExternalLink, MessageSquareWarning, Pencil, RotateCcw, Rocket, Send, Trash2, Undo2, UserRoundCheck } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { FileList } from "@/features/files/file-list";
import { useSession } from "@/features/identity/session";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatDateTime } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { formatLabels, shortTime, useContentMutations, useContentPost, type ContentAction } from "./content-api";
import { ContentStatusBadge, PlatformChips, PostDialog, PostMedia, PostText } from "./post-parts";

function Fact({ label, children }: { label: TranslationKey; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="grid gap-1">
      <dt className="text-muted-foreground text-xs font-medium">{t(label)}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

/** One post: how it will look, where its approval stands, what can happen next, and its conversation with the client. */
export function PostDetail({ id }: { id: string }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const post = useContentPost(id);
  const { act, sendBack, publish, remove } = useContentMutations();
  const [editing, setEditing] = useState(false);
  const granted = session?.permissions;
  const canWrite = hasPermission(granted, permissions.social.contentWrite);
  const canApprove = hasPermission(granted, permissions.social.contentApprove);
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  const back = (
    <Link href="/content" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("content.title")}
    </Link>
  );

  if (post.isPending) return <Skeleton className="h-96 w-full" />;
  if (post.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{tError(post.error instanceof ApiError ? post.error.code : "common.network")}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const data = post.data;
  const status = data.status;
  const editable = status === "draft" || status === "internal_review" || status === "changes_requested";
  const run = (action: ContentAction, done: TranslationKey) => act.mutate({ id, action }, { onSuccess: () => toast.success(t(done)), onError: fail });

  return (
    <div className="grid gap-6">
      {back}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="grid gap-1.5">
          <div className="flex flex-wrap items-center gap-2">
            <ContentStatusBadge status={status} />
            {data.revision > 1 ? <span className="text-muted-foreground text-xs">{t("content.revision", { number: data.revision })}</span> : null}
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{data.title}</h1>
          <Link href={`/crm/parties/${data.partyId}`} className="text-primary flex items-center gap-1.5 text-sm underline-offset-4 hover:underline">
            <Building2 className="size-4" aria-hidden /> {data.partyName}
          </Link>
        </div>
        <div className="flex flex-wrap gap-2">
          {canWrite && (status === "draft" || status === "changes_requested") ? (
            <Button variant="outline" disabled={act.isPending} onClick={() => run("submit", "content.done.submitted")}>
              <UserRoundCheck /> {t("content.action.submit")}
            </Button>
          ) : null}
          {canApprove && status === "internal_review" ? (
            <Button
              variant="outline"
              onClick={() => {
                const note = window.prompt(t("content.returnPrompt"));
                if (note?.trim()) sendBack.mutate({ id, note: note.trim() }, { onSuccess: () => toast.success(t("content.done.returned")), onError: fail });
              }}
            >
              <Undo2 /> {t("content.action.return")}
            </Button>
          ) : null}
          {canApprove && status !== "approved" && status !== "published" ? (
            <Button variant="outline" disabled={act.isPending} onClick={() => run("approve", "content.done.approved")}>
              <CircleCheck /> {t("content.action.approve")}
            </Button>
          ) : null}
          {canWrite && (status === "client_review" || status === "approved") ? (
            <Button
              variant="outline"
              disabled={act.isPending}
              onClick={() => {
                if (window.confirm(t("content.reviseConfirm"))) run("revise", "content.done.revised");
              }}
            >
              <RotateCcw /> {t("content.action.revise")}
            </Button>
          ) : null}
          {canWrite && editable ? (
            <Button variant="outline" onClick={() => setEditing(true)}>
              <Pencil /> {t("content.edit")}
            </Button>
          ) : null}
          {canApprove && editable ? (
            <Button disabled={act.isPending} onClick={() => run("send", "content.done.sent")}>
              <Send /> {t("content.action.send")}
            </Button>
          ) : null}
          {canWrite && status === "approved" ? (
            <Button
              disabled={publish.isPending}
              onClick={() => {
                const url = window.prompt(t("content.publishPrompt"), "");
                if (url === null) return;
                publish.mutate({ id, url: url.trim() || null }, { onSuccess: () => toast.success(t("content.done.published")), onError: fail });
              }}
            >
              <Rocket /> {t("content.action.publish")}
            </Button>
          ) : null}
        </div>
      </div>

      {status === "changes_requested" && data.clientNote ? (
        <Alert variant="destructive">
          <MessageSquareWarning />
          <AlertDescription>{t("content.clientAsked", { name: data.decidedByName ?? "", note: data.clientNote })}</AlertDescription>
        </Alert>
      ) : null}
      {status === "draft" && data.reviewNote ? (
        <Alert>
          <Undo2 />
          <AlertDescription>{t("content.reviewerSaid", { note: data.reviewNote })}</AlertDescription>
        </Alert>
      ) : null}
      {status === "approved" && data.decidedByName ? (
        <Alert>
          <CircleCheck />
          <AlertDescription>{t("content.approvedBy", { name: data.decidedByName, date: data.decidedAt ? formatDateTime(data.decidedAt) : "" })}</AlertDescription>
        </Alert>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle>{t("content.preview")}</CardTitle>
              <CardDescription>{t("content.previewHint")}</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-[minmax(0,280px)_1fr]">
              <PostMedia postId={id} audience="staff" />
              <PostText caption={data.caption} hashtags={data.hashtags} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t("content.thread")}</CardTitle>
              <CardDescription>{t("content.threadHint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed subjectType="content_post" subjectId={id} canWriteNotes={hasPermission(granted, permissions.timeline.notesWrite)} clientThread={status !== "draft" && status !== "internal_review"} />
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle>{t("content.plan")}</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-4">
                <Fact label="content.field.date">
                  {formatCalendarDate(data.plannedDate)}
                  {data.plannedTime ? <span className="text-muted-foreground tabular-nums"> · {shortTime(data.plannedTime)}</span> : null}
                </Fact>
                <Fact label="content.field.platforms">{data.platforms.length > 0 ? <PlatformChips codes={data.platforms} /> : "—"}</Fact>
                <Fact label="content.field.format">{t(formatLabels[data.format] ?? "content.format.post")}</Fact>
                <Fact label="content.owner">{data.ownerName ?? "—"}</Fact>
                {data.sentToClientAt ? <Fact label="content.sentAt">{formatDateTime(data.sentToClientAt)}</Fact> : null}
                {data.publishedAt ? (
                  <Fact label="content.publishedAt">
                    {formatDateTime(data.publishedAt)}
                    {data.publishedUrl ? (
                      <a href={data.publishedUrl} target="_blank" rel="noopener noreferrer" className="text-primary mt-1 flex items-center gap-1 underline-offset-4 hover:underline">
                        <ExternalLink className="size-3.5" aria-hidden /> {t("content.openPublished")}
                      </a>
                    ) : null}
                  </Fact>
                ) : null}
              </dl>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t("content.media.title")}</CardTitle>
              <CardDescription>{t("content.media.hint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <FileList subjectType="content_post" subjectId={id} canWrite={canWrite && editable && hasPermission(granted, permissions.files.write)} />
            </CardContent>
          </Card>

          {canWrite && status !== "published" ? (
            <Button
              variant="ghost"
              className="text-destructive justify-self-start"
              onClick={() => {
                if (!window.confirm(t("content.deleteConfirm", { title: data.title }))) return;
                remove.mutate(id, { onSuccess: () => router.push("/content"), onError: fail });
              }}
            >
              <Trash2 /> {t("content.delete")}
            </Button>
          ) : null}
        </div>
      </div>

      {editing ? <PostDialog open onOpenChange={setEditing} post={data} /> : null}
    </div>
  );
}

"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CalendarDays, CircleCheck, ExternalLink, MessageSquareWarning } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { formatLabels, shortTime } from "@/features/content/content-api";
import { ContentStatusBadge, PlatformChips, PostMedia, PostText } from "@/features/content/post-parts";
import type { Schemas } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { portalApi } from "@/lib/api/portal-client";
import { formatCalendarDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { PortalError, Thread } from "./portal-parts";

type ClientPost = Schemas["ClientContentPost"];

const postsKey = ["portal", "content"] as const;

function usePosts() {
  return useQuery({ queryKey: postsKey, queryFn: async () => unwrap(await portalApi.GET("/api/v1/social/portal/posts")) });
}

function useDecide() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, note }: { id: string; note?: string }) =>
      note === undefined
        ? unwrap(await portalApi.POST("/api/v1/social/portal/posts/{id}/approve", { params: { path: { id } } }))
        : unwrap(await portalApi.POST("/api/v1/social/portal/posts/{id}/request-changes", { params: { path: { id } }, body: { note } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: postsKey });
      window.setTimeout(() => void queryClient.invalidateQueries({ queryKey: ["portal", "thread"] }), 1500);
    },
  });
}

function When({ post }: { post: ClientPost }) {
  const { t } = useI18n();
  return (
    <span className="text-muted-foreground flex flex-wrap items-center gap-2 text-xs">
      <span className="tabular-nums">
        {formatCalendarDate(post.plannedDate)}
        {post.plannedTime ? ` · ${shortTime(post.plannedTime)}` : ""}
      </span>
      <span>{t(formatLabels[post.format] ?? "content.format.post")}</span>
      <PlatformChips codes={post.platforms} />
    </span>
  );
}

/** Approve, or say what should change. The note is required for a change request: "no" alone helps nobody. */
function Decision({ post, compact = false }: { post: ClientPost; compact?: boolean }) {
  const { t, tError } = useI18n();
  const decide = useDecide();
  const [asking, setAsking] = useState(false);
  const [note, setNote] = useState("");
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  if (asking) {
    return (
      <div className="grid gap-2">
        <Label htmlFor={`changes-${post.id}`} className="text-[13px] font-medium">
          {t("portal.content.whatToChange")}
        </Label>
        <Textarea id={`changes-${post.id}`} rows={3} maxLength={2000} value={note} onChange={(event) => setNote(event.target.value)} className="text-base md:text-sm" />
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => setAsking(false)}>
            {t("common.cancel")}
          </Button>
          <Button
            disabled={decide.isPending || !note.trim()}
            onClick={() => decide.mutate({ id: post.id, note: note.trim() }, { onSuccess: () => toast.success(t("portal.content.changesSent")), onError: fail })}
          >
            {t("portal.content.sendChanges")}
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className={compact ? "flex flex-wrap gap-2" : "flex flex-col gap-2 sm:flex-row sm:justify-end"}>
      <Button variant="outline" size={compact ? "default" : "lg"} onClick={() => setAsking(true)}>
        <MessageSquareWarning /> {t("portal.content.requestChanges")}
      </Button>
      <Button size={compact ? "default" : "lg"} disabled={decide.isPending} onClick={() => decide.mutate({ id: post.id }, { onSuccess: () => toast.success(t("portal.content.approved")), onError: fail })}>
        <CircleCheck /> {t("portal.content.approve")}
      </Button>
    </div>
  );
}

function PendingCard({ post }: { post: ClientPost }) {
  const { t } = useI18n();
  return (
    <Card>
      <CardContent className="grid gap-4 sm:grid-cols-[200px_1fr]">
        <PostMedia postId={post.id} audience="client" />
        <div className="grid content-start gap-3">
          <div className="grid gap-1">
            <Link href={`/portal/content/${post.id}`} className="font-medium underline-offset-4 hover:underline">
              {post.title}
            </Link>
            <When post={post} />
            {post.revision > 1 ? <span className="text-muted-foreground text-xs">{t("portal.content.revised", { number: post.revision })}</span> : null}
          </div>
          <PostText caption={post.caption} hashtags={post.hashtags} />
          <Decision post={post} compact />
        </div>
      </CardContent>
    </Card>
  );
}

function PostRow({ post }: { post: ClientPost }) {
  return (
    <li>
      <Link href={`/portal/content/${post.id}`} className="hover:bg-muted/40 grid gap-1 px-4 py-3.5 focus-visible:outline-none">
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{post.title}</span>
          <ContentStatusBadge status={post.status} audience="client" />
        </span>
        <When post={post} />
      </Link>
    </li>
  );
}

/** The client's content calendar: what waits for their answer first, then what is planned and what went out. */
export function PortalContent() {
  const { t, tError } = useI18n();
  const posts = usePosts();
  const decide = useDecide();
  const [approvingAll, setApprovingAll] = useState(false);

  const title = (
    <div className="grid gap-1">
      <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{t("portal.content.title")}</h1>
      <p className="text-muted-foreground">{t("portal.content.description")}</p>
    </div>
  );

  if (posts.isPending) {
    return (
      <div className="grid gap-6">
        {title}
        <Skeleton className="h-60 w-full" />
      </div>
    );
  }
  if (posts.isError) return <PortalError error={posts.error} />;

  const waiting = posts.data.filter((post) => post.status === "client_review");
  const rest = posts.data.filter((post) => post.status !== "client_review");
  const upcoming = rest.filter((post) => post.status !== "published");
  const published = rest.filter((post) => post.status === "published").reverse();

  const approveAll = async () => {
    if (!window.confirm(t("portal.content.approveAllConfirm", { count: waiting.length }))) return;
    setApprovingAll(true);
    try {
      for (const post of waiting) await decide.mutateAsync({ id: post.id });
      toast.success(t("portal.content.allApproved"));
    } catch (failure) {
      toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));
    } finally {
      setApprovingAll(false);
    }
  };

  return (
    <div className="grid gap-6">
      {title}

      {posts.data.length === 0 ? (
        <Card>
          <EmptyState icon={CalendarDays} title={t("portal.content.empty")} description={t("portal.content.emptyHint")} />
        </Card>
      ) : null}

      {waiting.length > 0 ? (
        <section className="grid gap-3" aria-labelledby="content-waiting">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="content-waiting" className="text-sm font-semibold">
              {t("portal.content.waiting", { count: waiting.length })}
            </h2>
            {waiting.length > 1 ? (
              <Button variant="outline" size="sm" disabled={approvingAll} onClick={() => void approveAll()}>
                <CircleCheck /> {t("portal.content.approveAll")}
              </Button>
            ) : null}
          </div>
          {waiting.map((post) => (
            <PendingCard key={post.id} post={post} />
          ))}
        </section>
      ) : null}

      {upcoming.length > 0 ? (
        <section className="grid gap-2" aria-labelledby="content-upcoming">
          <h2 id="content-upcoming" className="text-sm font-semibold">
            {t("portal.content.upcoming")}
          </h2>
          <Card className="py-0">
            <CardContent className="px-0">
              <ul className="divide-border divide-y">
                {upcoming.map((post) => (
                  <PostRow key={post.id} post={post} />
                ))}
              </ul>
            </CardContent>
          </Card>
        </section>
      ) : null}

      {published.length > 0 ? (
        <section className="grid gap-2" aria-labelledby="content-published">
          <h2 id="content-published" className="text-sm font-semibold">
            {t("portal.content.published")}
          </h2>
          <Card className="py-0">
            <CardContent className="px-0">
              <ul className="divide-border divide-y">
                {published.map((post) => (
                  <PostRow key={post.id} post={post} />
                ))}
              </ul>
            </CardContent>
          </Card>
        </section>
      ) : null}
    </div>
  );
}

export function PortalContentPost({ id }: { id: string }) {
  const { t } = useI18n();
  const post = useQuery({
    queryKey: [...postsKey, id],
    queryFn: async () => unwrap(await portalApi.GET("/api/v1/social/portal/posts/{id}", { params: { path: { id } } })),
  });
  const back = (
    <Link href="/portal/content" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("portal.content.title")}
    </Link>
  );

  if (post.isPending) return <Skeleton className="h-96 w-full" />;
  if (post.isError) {
    return (
      <div className="grid gap-6">
        {back}
        <PortalError error={post.error} />
      </div>
    );
  }

  const data = post.data;
  return (
    <div className="grid gap-6">
      {back}
      <div className="grid gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <ContentStatusBadge status={data.status} audience="client" />
          {data.revision > 1 ? <span className="text-muted-foreground text-xs">{t("portal.content.revised", { number: data.revision })}</span> : null}
        </div>
        <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{data.title}</h1>
        <When post={data} />
      </div>

      {data.status === "changes_requested" && data.clientNote ? (
        <Alert>
          <MessageSquareWarning />
          <AlertDescription>{t("portal.content.youAsked", { note: data.clientNote })}</AlertDescription>
        </Alert>
      ) : null}

      <Card>
        <CardContent className="grid gap-4 sm:grid-cols-[minmax(0,320px)_1fr]">
          <PostMedia postId={id} audience="client" />
          <div className="grid content-start gap-4">
            <PostText caption={data.caption} hashtags={data.hashtags} />
            {data.publishedUrl ? (
              <a href={data.publishedUrl} target="_blank" rel="noopener noreferrer" className={buttonVariants({ variant: "outline", className: "justify-self-start" })}>
                <ExternalLink /> {t("content.openPublished")}
              </a>
            ) : null}
          </div>
        </CardContent>
      </Card>

      {data.status === "client_review" ? <Decision post={data} /> : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("portal.thread.title")}</CardTitle>
          <CardDescription>{t("portal.content.threadHint")}</CardDescription>
        </CardHeader>
        <CardContent>
          <Thread subjectType="content_post" subjectId={id} />
        </CardContent>
      </Card>
    </div>
  );
}

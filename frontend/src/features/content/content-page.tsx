"use client";

import { AlertCircle, CalendarDays, ChevronLeft, ChevronRight, Plus, Send } from "lucide-react";
import Link from "next/link";
import { useState, type DragEvent } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useSession } from "@/features/identity/session";
import { PartyPicker, type PickedParty } from "@/features/sales/party-picker";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatMonth, weekdayNames } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { isoDay, monthGrid, shortTime, statusStyles, useContentMutations, useContentPosts, type ContentPost } from "./content-api";
import { ContentStatusBadge, PlatformChips, PostDialog } from "./post-parts";

const statusOrder = ["draft", "internal_review", "client_review", "changes_requested", "approved", "published"] as const;

function Chip({ post, showParty, draggable }: { post: ContentPost; showParty: boolean; draggable: boolean }) {
  const { t } = useI18n();
  const style = statusStyles[post.status] ?? statusStyles.draft!;
  return (
    <Link
      href={`/content/${post.id}`}
      draggable={draggable}
      onDragStart={(event: DragEvent) => event.dataTransfer.setData("text/post-id", post.id)}
      title={`${post.title} · ${t(style.label)}`}
      className={cn(
        "bg-card hover:border-primary/50 focus-visible:ring-ring/40 block rounded-md border border-l-[3px] px-1.5 py-1 text-xs leading-tight focus-visible:ring-2 focus-visible:outline-none",
        style.bar,
      )}
    >
      <span className="block truncate font-medium">
        {post.plannedTime ? <span className="text-muted-foreground tabular-nums">{shortTime(post.plannedTime)} </span> : null}
        {post.title}
      </span>
      {showParty ? <span className="text-muted-foreground block truncate">{post.partyName}</span> : null}
      <span className="sr-only">{t(style.label)}</span>
    </Link>
  );
}

function MonthGrid({
  days,
  monthIndex,
  byDay,
  showParty,
  canWrite,
  onAdd,
  onMove,
}: {
  days: string[];
  monthIndex: number;
  byDay: Map<string, ContentPost[]>;
  showParty: boolean;
  canWrite: boolean;
  onAdd: (day: string) => void;
  onMove: (postId: string, day: string) => void;
}) {
  const { t } = useI18n();
  const [today] = useState(() => isoDay(new Date()));
  const [over, setOver] = useState<string | null>(null);
  const names = weekdayNames();

  return (
    <div className="hidden overflow-hidden rounded-xl border md:block">
      <div className="bg-muted/60 text-muted-foreground grid grid-cols-7 border-b text-xs font-medium">
        {names.map((name) => (
          <div key={name} className="px-2 py-2">
            {name}
          </div>
        ))}
      </div>
      <div className="bg-border grid grid-cols-7 gap-px">
        {days.map((day) => {
          const inMonth = Number(day.slice(5, 7)) - 1 === monthIndex;
          const posts = byDay.get(day) ?? [];
          return (
            <div
              key={day}
              onDragOver={(event) => {
                if (!canWrite) return;
                event.preventDefault();
                setOver(day);
              }}
              onDragLeave={() => setOver((current) => (current === day ? null : current))}
              onDrop={(event) => {
                setOver(null);
                const id = event.dataTransfer.getData("text/post-id");
                if (id) onMove(id, day);
              }}
              className={cn("group bg-card flex min-h-28 flex-col gap-1 p-1.5", !inMonth && "bg-muted/30", over === day && "bg-primary-soft")}
            >
              <div className="flex items-center justify-between">
                <span
                  className={cn(
                    "flex size-6 items-center justify-center rounded-full text-xs tabular-nums",
                    day === today ? "bg-primary text-primary-foreground font-semibold" : inMonth ? "text-foreground" : "text-muted-foreground",
                  )}
                >
                  {Number(day.slice(8))}
                </span>
                {canWrite ? (
                  <Button
                    variant="ghost"
                    size="icon-xs"
                    className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100"
                    aria-label={t("content.addOn", { date: formatCalendarDate(day) })}
                    onClick={() => onAdd(day)}
                  >
                    <Plus />
                  </Button>
                ) : null}
              </div>
              {posts.map((post) => (
                <Chip key={post.id} post={post} showParty={showParty} draggable={canWrite && post.status !== "published"} />
              ))}
            </div>
          );
        })}
      </div>
    </div>
  );
}

/** The same month as a list of days: what phones show, and what the "list" view shows everywhere. */
function Agenda({ posts, showParty, className }: { posts: ContentPost[]; showParty: boolean; className?: string }) {
  const days = [...new Set(posts.map((post) => post.plannedDate))];
  return (
    <div className={cn("grid gap-4", className)}>
      {days.map((day) => (
        <section key={day} className="grid gap-2">
          <h2 className="text-muted-foreground text-xs font-semibold tracking-wider uppercase">{formatCalendarDate(day)}</h2>
          <Card className="py-0">
            <CardContent className="px-0">
              <ul className="divide-border divide-y">
                {posts
                  .filter((post) => post.plannedDate === day)
                  .map((post) => (
                    <li key={post.id}>
                      <Link href={`/content/${post.id}`} className="hover:bg-muted/40 flex items-center gap-3 px-4 py-3 focus-visible:outline-none">
                        <div className="grid min-w-0 flex-1 gap-1">
                          <span className="flex flex-wrap items-center gap-2">
                            <span className="truncate font-medium">{post.title}</span>
                            <ContentStatusBadge status={post.status} />
                          </span>
                          <span className="text-muted-foreground flex flex-wrap items-center gap-2 text-xs">
                            {post.plannedTime ? <span className="tabular-nums">{shortTime(post.plannedTime)}</span> : null}
                            {showParty ? <span>{post.partyName}</span> : null}
                            <PlatformChips codes={post.platforms} />
                          </span>
                        </div>
                      </Link>
                    </li>
                  ))}
              </ul>
            </CardContent>
          </Card>
        </section>
      ))}
    </div>
  );
}

/** The content calendar: a month of planned posts across clients, or one client's month to send for approval. */
export function ContentPage() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const canWrite = hasPermission(session?.permissions, permissions.social.contentWrite);
  const canApprove = hasPermission(session?.permissions, permissions.social.contentApprove);
  const [month, setMonth] = useState(() => {
    const now = new Date();
    return { year: now.getFullYear(), index: now.getMonth() };
  });
  const [party, setParty] = useState<PickedParty | null>(null);
  const [view, setView] = useState<"calendar" | "list">("calendar");
  const [adding, setAdding] = useState<string | null>(null);
  const { reschedule, sendBatch } = useContentMutations();

  const days = monthGrid(month.year, month.index);
  const posts = useContentPosts({ from: days[0], to: days.at(-1), partyId: party?.id });
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  const shift = (delta: number) => {
    const next = new Date(month.year, month.index + delta, 1);
    setMonth({ year: next.getFullYear(), index: next.getMonth() });
  };

  const items = posts.data ?? [];
  const byDay = new Map<string, ContentPost[]>();
  for (const post of items) byDay.set(post.plannedDate, [...(byDay.get(post.plannedDate) ?? []), post]);
  const inMonth = items.filter((post) => Number(post.plannedDate.slice(5, 7)) - 1 === month.index);
  const ready = inMonth.filter((post) => post.status === "draft" || post.status === "internal_review").length;
  const counts = statusOrder.map((status) => ({ status, count: inMonth.filter((post) => post.status === status).length })).filter((row) => row.count > 0);

  const first = isoDay(new Date(month.year, month.index, 1));
  const last = isoDay(new Date(month.year, month.index + 1, 0));
  const defaultDay = (() => {
    const today = isoDay(new Date());
    return today >= first && today <= last ? today : first;
  })();

  const actions = (
    <>
      {canApprove && party && ready > 0 ? (
        <Button
          variant="outline"
          disabled={sendBatch.isPending}
          onClick={() => {
            if (!window.confirm(t("content.sendBatchConfirm", { count: ready, party: party.name }))) return;
            sendBatch.mutate({ partyId: party.id, from: first, to: last }, { onSuccess: (result) => toast.success(t("content.sentBatch", { count: result.sent })), onError: fail });
          }}
        >
          <Send /> {t("content.sendBatch", { count: ready })}
        </Button>
      ) : null}
      {canWrite ? (
        <Button onClick={() => setAdding(defaultDay)}>
          <Plus /> {t("content.new")}
        </Button>
      ) : null}
    </>
  );

  return (
    <div className="grid gap-6">
      <PageHeader title={t("content.title")} description={t("content.description")} actions={actions} />

      <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="outline" size="icon" onClick={() => shift(-1)} aria-label={t("content.previousMonth")}>
            <ChevronLeft />
          </Button>
          <h2 className="min-w-36 text-center text-base font-semibold capitalize" aria-live="polite">
            {formatMonth(month.year, month.index)}
          </h2>
          <Button variant="outline" size="icon" onClick={() => shift(1)} aria-label={t("content.nextMonth")}>
            <ChevronRight />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              const now = new Date();
              setMonth({ year: now.getFullYear(), index: now.getMonth() });
            }}
          >
            {t("common.today")}
          </Button>
          <div role="tablist" aria-label={t("content.view")} className="bg-muted ml-2 hidden rounded-lg p-0.5 md:inline-flex">
            {(["calendar", "list"] as const).map((option) => (
              <button
                key={option}
                type="button"
                role="tab"
                aria-selected={view === option}
                onClick={() => setView(option)}
                className={cn(
                  "focus-visible:ring-ring/40 h-8 cursor-pointer rounded-md px-3 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                  view === option ? "bg-card text-foreground font-medium shadow-xs" : "text-muted-foreground hover:text-foreground",
                )}
              >
                {t(option === "calendar" ? "content.view.calendar" : "content.view.list")}
              </button>
            ))}
          </div>
        </div>
        <div className="w-full lg:w-72">
          <PartyPicker id="content-party" value={party} onChange={setParty} />
        </div>
      </div>

      {counts.length > 0 ? (
        <ul className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-xs">
          {counts.map((row) => (
            <li key={row.status} className="flex items-center gap-1.5">
              <span className={cn("h-3 border-l-[3px]", statusStyles[row.status]!.bar)} aria-hidden />
              {t(statusStyles[row.status]!.label)} <span className="text-foreground font-medium tabular-nums">{row.count}</span>
            </li>
          ))}
        </ul>
      ) : null}

      {posts.isPending ? (
        <Skeleton className="h-96 w-full rounded-xl" />
      ) : posts.isError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {posts.error instanceof ApiError && posts.error.status === 403 ? t("content.noPermission") : tError(posts.error instanceof ApiError ? posts.error.code : "common.network")}
          </AlertDescription>
        </Alert>
      ) : (
        <>
          {view === "calendar" ? (
            <MonthGrid
              days={days}
              monthIndex={month.index}
              byDay={byDay}
              showParty={!party}
              canWrite={canWrite}
              onAdd={setAdding}
              onMove={(id, day) => {
                const post = items.find((item) => item.id === id);
                if (post && post.plannedDate !== day) reschedule.mutate({ id, plannedDate: day, plannedTime: post.plannedTime }, { onError: fail });
              }}
            />
          ) : null}
          {inMonth.length === 0 ? (
            <Card className={cn(view === "calendar" && "md:hidden")}>
              <EmptyState
                icon={CalendarDays}
                title={t("content.empty")}
                description={t("content.emptyHint")}
                action={
                  canWrite ? (
                    <Button onClick={() => setAdding(defaultDay)}>
                      <Plus /> {t("content.new")}
                    </Button>
                  ) : undefined
                }
              />
            </Card>
          ) : (
            <Agenda posts={inMonth} showParty={!party} className={view === "calendar" ? "md:hidden" : undefined} />
          )}
        </>
      )}

      {adding ? <PostDialog open onOpenChange={(open) => !open && setAdding(null)} date={adding} party={party} /> : null}
    </div>
  );
}

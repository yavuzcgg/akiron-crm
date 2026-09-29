"use client";

import { AlertCircle } from "lucide-react";
import { useState, type FormEvent } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatDate, formatTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { describeEntry } from "./entry-types";
import { useAddNote, useTimeline, type TimelineItem, type TimelineSubjectType } from "./timeline-api";

interface TimelineFeedProps {
  subjectType: TimelineSubjectType;
  subjectId: string;
  canWriteNotes: boolean;
}

/** A record's history grouped by day, newest first, with an optional note composer (ADR-0009). */
export function TimelineFeed({ subjectType, subjectId, canWriteNotes }: TimelineFeedProps) {
  const { t, tError } = useI18n();
  const timeline = useTimeline(subjectType, subjectId);
  // Read once per mount: "today" must not change between renders of the same list.
  const [now] = useState(() => Date.now());

  if (timeline.isPending) {
    return (
      <div className="grid gap-3">
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
      </div>
    );
  }

  if (timeline.isError) {
    const error = timeline.error;
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>
          {tError(error instanceof ApiError ? error.code : "common.network", error instanceof ApiError ? error.params : undefined)}
        </AlertDescription>
      </Alert>
    );
  }

  const items = timeline.data.pages.flatMap((page) => page.items);

  return (
    <div className="grid gap-6">
      {canWriteNotes ? <NoteComposer subjectType={subjectType} subjectId={subjectId} /> : null}

      {items.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t("timeline.empty")}</p>
      ) : (
        <ol className="grid gap-6">
          {groupByDay(items).map(([day, entries]) => (
            <li key={day} className="grid gap-3">
              <DayHeading day={day} sample={entries[0]!.occurredAt} now={now} />
              <ol className="border-border ml-4 grid gap-4 border-l pl-6">
                {entries.map((item) => (
                  <TimelineRow key={item.id} item={item} />
                ))}
              </ol>
            </li>
          ))}
        </ol>
      )}

      {timeline.hasNextPage ? (
        <Button variant="outline" onClick={() => timeline.fetchNextPage()} disabled={timeline.isFetchingNextPage}>
          {timeline.isFetchingNextPage ? t("common.loading") : t("common.loadMore")}
        </Button>
      ) : null}
    </div>
  );
}

function TimelineRow({ item }: { item: TimelineItem }) {
  const { t } = useI18n();
  const entry = describeEntry(item, t);

  return (
    <li className="relative">
      <span
        className={cn(
          "ring-background absolute top-0 -left-[2.4rem] flex size-7 items-center justify-center rounded-full ring-4",
          entry.tone,
        )}
      >
        <entry.icon className="size-3.5" />
      </span>
      <div className="flex items-baseline justify-between gap-4">
        <p className="text-sm">{entry.headline}</p>
        <time className="text-muted-foreground shrink-0 text-xs tabular-nums" dateTime={item.occurredAt}>
          {formatTime(item.occurredAt)}
        </time>
      </div>
      {entry.body ? (
        <p className="bg-muted/50 mt-2 rounded-md px-3 py-2 text-sm whitespace-pre-wrap">{entry.body}</p>
      ) : null}
    </li>
  );
}

function DayHeading({ day, sample, now }: { day: string; sample: string; now: number }) {
  const { t } = useI18n();
  const today = dayKey(new Date(now));
  const yesterday = dayKey(new Date(now - 86_400_000));
  const label = day === today ? t("common.today") : day === yesterday ? t("common.yesterday") : formatDate(sample);

  return <h3 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{label}</h3>;
}

function NoteComposer({ subjectType, subjectId }: { subjectType: TimelineSubjectType; subjectId: string }) {
  const { t, tError } = useI18n();
  const addNote = useAddNote(subjectType, subjectId);
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!text.trim()) return;

    setError(null);
    try {
      await addNote.mutateAsync(text);
      setText("");
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <form onSubmit={submit} className="grid gap-2">
      <Textarea
        value={text}
        onChange={(event) => setText(event.target.value)}
        placeholder={t("timeline.note.placeholder")}
        maxLength={4000}
        rows={2}
      />
      {error ? <p className="text-destructive text-sm">{error}</p> : null}
      <div className="flex justify-end">
        <Button type="submit" size="sm" disabled={addNote.isPending || !text.trim()}>
          {t("timeline.note.submit")}
        </Button>
      </div>
    </form>
  );
}

function groupByDay(items: TimelineItem[]): [string, TimelineItem[]][] {
  const groups = new Map<string, TimelineItem[]>();
  for (const item of items) {
    const key = dayKey(item.occurredAt);
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }
  return [...groups.entries()];
}

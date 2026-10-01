"use client";

import { Activity, AlertCircle } from "lucide-react";
import Link from "next/link";
import { EmptyState } from "@/components/empty-state";
import { useRef, useState, type FormEvent } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatDate, formatTime, initials } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { describeEntry } from "./entry-types";
import { fold, highlightMentions, mentionQuery, mentionsIn, useMentionable, type Mention } from "./mentions";
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
        <EmptyState icon={Activity} title={t("timeline.empty")} className="py-6" />
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
        <p className="text-sm">
          {entry.href ? (
            <Link href={entry.href} className="underline-offset-4 hover:underline">
              {entry.headline}
            </Link>
          ) : (
            entry.headline
          )}
        </p>
        <time className="text-muted-foreground shrink-0 text-xs tabular-nums" dateTime={item.occurredAt}>
          {formatTime(item.occurredAt)}
        </time>
      </div>
      {entry.body ? (
        <p className="bg-muted/50 mt-2 rounded-md px-3 py-2 text-sm whitespace-pre-wrap">{highlightMentions(entry.body, entry.mentions)}</p>
      ) : null}
    </li>
  );
}

function DayHeading({ day, sample, now }: { day: string; sample: string; now: number }) {
  const { t } = useI18n();
  const today = dayKey(new Date(now));
  const yesterday = dayKey(new Date(now - 86_400_000));
  const label = day === today ? t("common.today") : day === yesterday ? t("common.yesterday") : formatDate(sample);

  return <h3 className="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">{label}</h3>;
}

/**
 * The note box. Typing "@" opens a list of teammates (arrows and Enter pick one); the people whose
 * "@Name" is still in the text when it is sent get notified.
 */
function NoteComposer({ subjectType, subjectId }: { subjectType: TimelineSubjectType; subjectId: string }) {
  const { t, tError } = useI18n();
  const addNote = useAddNote(subjectType, subjectId);
  const textarea = useRef<HTMLTextAreaElement>(null);
  const [text, setText] = useState("");
  const [picked, setPicked] = useState<Mention[]>([]);
  const [query, setQuery] = useState<{ query: string; start: number } | null>(null);
  const [active, setActive] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const people = useMentionable(true);

  const options = query
    ? (people.data ?? []).filter((person) => fold(person.fullName).includes(fold(query.query))).slice(0, 6)
    : [];

  const pick = (person: { userId: string; fullName: string }) => {
    if (!query) return;
    const caret = query.start + query.query.length + 1;
    const next = `${text.slice(0, query.start)}@${person.fullName} ${text.slice(caret)}`;
    const position = query.start + person.fullName.length + 2;
    setText(next);
    setPicked([...picked, { userId: person.userId, name: person.fullName }]);
    setQuery(null);
    requestAnimationFrame(() => {
      textarea.current?.focus();
      textarea.current?.setSelectionRange(position, position);
    });
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!text.trim()) return;

    setError(null);
    try {
      await addNote.mutateAsync({ text, mentionedUserIds: mentionsIn(text, picked).map((mention) => mention.userId) });
      setText("");
      setPicked([]);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <form onSubmit={submit} className="bg-muted/40 focus-within:border-ring focus-within:ring-ring/20 relative grid gap-2 rounded-xl border p-3 transition-shadow focus-within:ring-3">
      <Textarea
        ref={textarea}
        className="min-h-14 resize-none border-0 bg-transparent p-1 shadow-none focus-visible:ring-0 dark:bg-transparent"
        aria-label={t("timeline.note.placeholder")}
        aria-expanded={options.length > 0}
        aria-controls="mention-list"
        value={text}
        onChange={(event) => {
          setText(event.target.value);
          setQuery(mentionQuery(event.target.value, event.target.selectionStart));
          setActive(0);
        }}
        onKeyDown={(event) => {
          if (options.length === 0) return;
          if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            setActive((current) => (current + (event.key === "ArrowDown" ? 1 : options.length - 1)) % options.length);
          } else if (event.key === "Enter" || event.key === "Tab") {
            event.preventDefault();
            pick(options[active]!);
          } else if (event.key === "Escape") {
            setQuery(null);
          }
        }}
        placeholder={t("timeline.note.placeholderMentions")}
        maxLength={4000}
        rows={2}
      />
      {options.length > 0 ? (
        <ul id="mention-list" role="listbox" className="bg-popover absolute top-full left-3 z-50 mt-1 w-64 rounded-lg border p-1 shadow-md">
          {options.map((person, index) => (
            <li
              key={person.userId}
              role="option"
              aria-selected={index === active}
              onMouseDown={(event) => {
                event.preventDefault();
                pick(person);
              }}
              className={cn("flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm", index === active && "bg-muted")}
            >
              <span className="bg-primary-soft text-primary-strong flex size-6 items-center justify-center rounded-full text-[10px] font-semibold">
                {initials(person.fullName)}
              </span>
              {person.fullName}
            </li>
          ))}
        </ul>
      ) : null}
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

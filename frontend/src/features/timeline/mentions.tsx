"use client";

import { useQuery } from "@tanstack/react-query";
import { Fragment, type ReactNode } from "react";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export interface Mention {
  userId: string;
  name: string;
}

export function useMentionable(enabled: boolean) {
  return useQuery({
    queryKey: ["timeline", "mentionable"],
    queryFn: async () => unwrap(await api.GET("/api/v1/timeline/mentionable")),
    enabled,
    staleTime: 5 * 60_000,
  });
}

/** Folds Turkish letters so "ay" finds "Ayşe" and "IS" finds "Işık". */
export function fold(text: string): string {
  return text
    .toLocaleLowerCase("tr-TR")
    .replace(/ı/g, "i")
    .normalize("NFD")
    .replace(/\p{M}/gu, "");
}

/** The "@query" being typed right before the caret, or null. */
export function mentionQuery(text: string, caret: number): { query: string; start: number } | null {
  const match = /(^|\s)@([\p{L}\p{N}]{0,30})$/u.exec(text.slice(0, caret));
  return match ? { query: match[2]!, start: caret - match[2]!.length - 1 } : null;
}

/** The mentions whose "@Name" is still in the text (a deleted name is no longer a mention). */
export function mentionsIn(text: string, picked: Mention[]): Mention[] {
  const unique = new Map(picked.map((mention) => [mention.userId, mention]));
  return [...unique.values()].filter((mention) => text.includes(`@${mention.name}`));
}

/** The note text with each "@Name" of <paramref name="mentions"/> emphasised. */
export function highlightMentions(text: string, mentions: Mention[]): ReactNode {
  if (mentions.length === 0) return text;
  const names = [...new Set(mentions.map((mention) => mention.name))].sort((left, right) => right.length - left.length);
  const pattern = new RegExp(`(@(?:${names.map((name) => name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|")}))`, "g");
  return text.split(pattern).map((part, index) =>
    index % 2 === 1 ? (
      <span key={index} className="text-primary bg-primary/10 rounded px-0.5 font-medium">
        {part}
      </span>
    ) : (
      <Fragment key={index}>{part}</Fragment>
    ),
  );
}

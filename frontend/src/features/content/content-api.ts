"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import type { TranslationKey } from "@/lib/i18n";

export type ContentPost = Schemas["ContentPostResponse"];
export type ContentInput = Schemas["ContentPostCommand"];

export const platforms = ["instagram", "facebook", "linkedin", "x", "tiktok", "youtube"] as const;
export const formats = ["post", "story", "reel", "carousel", "video"] as const;

export const platformNames: Record<string, string> = {
  instagram: "Instagram",
  facebook: "Facebook",
  linkedin: "LinkedIn",
  x: "X",
  tiktok: "TikTok",
  youtube: "YouTube",
};

export const formatLabels: Record<string, TranslationKey> = {
  post: "content.format.post",
  story: "content.format.story",
  reel: "content.format.reel",
  carousel: "content.format.carousel",
  video: "content.format.video",
};

type BadgeVariant = "secondary" | "warning" | "info" | "destructive" | "success" | "outline";

/** How each status reads and which colour family carries it (always with its label). */
export const statusStyles: Record<string, { label: TranslationKey; badge: BadgeVariant; bar: string }> = {
  draft: { label: "content.status.draft", badge: "secondary", bar: "border-l-muted-foreground/50" },
  internal_review: { label: "content.status.internal_review", badge: "warning", bar: "border-l-warning" },
  client_review: { label: "content.status.client_review", badge: "info", bar: "border-l-info" },
  changes_requested: { label: "content.status.changes_requested", badge: "destructive", bar: "border-l-destructive" },
  approved: { label: "content.status.approved", badge: "success", bar: "border-l-success" },
  published: { label: "content.status.published", badge: "outline", bar: "border-l-primary" },
};

const contentKey = ["content"] as const;

export function useContentPosts(filter: { from?: string; to?: string; partyId?: string; status?: string }) {
  return useQuery({
    queryKey: [...contentKey, "list", filter],
    queryFn: async () => unwrap(await api.GET("/api/v1/social/posts", { params: { query: filter } })),
  });
}

export function useContentPost(id: string) {
  return useQuery({
    queryKey: [...contentKey, "one", id],
    queryFn: async () => unwrap(await api.GET("/api/v1/social/posts/{id}", { params: { path: { id } } })),
  });
}

export type ContentAction = "submit" | "send" | "approve" | "revise";

export function useContentMutations() {
  const queryClient = useQueryClient();
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: contentKey });
    refreshTimelineSoon(queryClient);
  };
  const path = (id: string) => ({ params: { path: { id } } });
  return {
    save: useMutation({
      mutationFn: async ({ id, ...body }: ContentInput & { id?: string }) =>
        id ? unwrap(await api.PUT("/api/v1/social/posts/{id}", { ...path(id), body })) : unwrap(await api.POST("/api/v1/social/posts", { body })),
      onSuccess: refresh,
    }),
    reschedule: useMutation({
      mutationFn: async ({ id, plannedDate, plannedTime }: { id: string; plannedDate: string; plannedTime?: string | null }) =>
        unwrap(await api.POST("/api/v1/social/posts/{id}/reschedule", { ...path(id), body: { plannedDate, plannedTime: plannedTime ?? null } })),
      onSuccess: refresh,
    }),
    act: useMutation({
      mutationFn: async ({ id, action }: { id: string; action: ContentAction }) => {
        if (action === "submit") return unwrap(await api.POST("/api/v1/social/posts/{id}/submit", path(id)));
        if (action === "send") return unwrap(await api.POST("/api/v1/social/posts/{id}/send", path(id)));
        if (action === "approve") return unwrap(await api.POST("/api/v1/social/posts/{id}/approve", path(id)));
        return unwrap(await api.POST("/api/v1/social/posts/{id}/revise", path(id)));
      },
      onSuccess: refresh,
    }),
    sendBack: useMutation({
      mutationFn: async ({ id, note }: { id: string; note: string }) => unwrap(await api.POST("/api/v1/social/posts/{id}/return", { ...path(id), body: { note } })),
      onSuccess: refresh,
    }),
    publish: useMutation({
      mutationFn: async ({ id, url }: { id: string; url: string | null }) => unwrap(await api.POST("/api/v1/social/posts/{id}/publish", { ...path(id), body: { url } })),
      onSuccess: refresh,
    }),
    sendBatch: useMutation({
      mutationFn: async (body: Schemas["SendBatchCommand"]) => unwrap(await api.POST("/api/v1/social/posts/send-batch", { body })),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        unwrap(await api.DELETE("/api/v1/social/posts/{id}", path(id)));
      },
      onSuccess: refresh,
    }),
  };
}

const pad = (value: number) => String(value).padStart(2, "0");

export const isoDay = (date: Date) => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;

/**
 * The days a month's calendar shows: whole weeks from the Monday on or before the 1st to the Sunday
 * on or after the last day (35 or 42 cells), as YYYY-MM-DD.
 */
export function monthGrid(year: number, monthIndex: number): string[] {
  const first = new Date(year, monthIndex, 1);
  const lead = (first.getDay() + 6) % 7;
  const daysInMonth = new Date(year, monthIndex + 1, 0).getDate();
  const cells = Math.ceil((lead + daysInMonth) / 7) * 7;
  return Array.from({ length: cells }, (_, index) => isoDay(new Date(year, monthIndex, 1 - lead + index)));
}

/** "14:30" from the API's "14:30:00"; empty when the post has no time. */
export const shortTime = (time?: string | null) => (time ? time.slice(0, 5) : "");

/** Media a browser can show inline as a picture. */
export const isImage = (contentType: string) => contentType.startsWith("image/");

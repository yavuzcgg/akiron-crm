"use client";

import { useInfiniteQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type TimelineItem = Schemas["TimelineItemResponse"];

/** Streams the API knows (ADR-0009); modules add theirs as they arrive. */
export type TimelineSubjectType = "workspace" | "user";

export const timelineQueryKey = (subjectType: TimelineSubjectType, subjectId: string) =>
  ["timeline", subjectType, subjectId] as const;

export function useTimeline(subjectType: TimelineSubjectType, subjectId: string, enabled = true) {
  return useInfiniteQuery({
    queryKey: timelineQueryKey(subjectType, subjectId),
    enabled,
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam }) =>
      unwrap(
        await api.GET("/api/v1/timeline/{subjectType}/{subjectId}", {
          params: { path: { subjectType, subjectId }, query: { before: pageParam } },
        }),
      ),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    // Other people's actions and projected events arrive without this tab doing anything.
    refetchInterval: 30_000,
  });
}

export function useAddNote(subjectType: TimelineSubjectType, subjectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (text: string) =>
      unwrap(
        await api.POST("/api/v1/timeline/{subjectType}/{subjectId}/notes", {
          params: { path: { subjectType, subjectId } },
          body: { text },
        }),
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: timelineQueryKey(subjectType, subjectId) }),
  });
}

/**
 * Entries for things that just happened elsewhere (an invitation sent, say) are projected from
 * the outbox a moment later; refresh the stream shortly after instead of right away.
 */
export function refreshTimelineSoon(queryClient: ReturnType<typeof useQueryClient>) {
  window.setTimeout(() => queryClient.invalidateQueries({ queryKey: ["timeline"] }), 1500);
}

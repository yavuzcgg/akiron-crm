"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { portalApi } from "@/lib/api/portal-client";

export type PortalMe = Schemas["PortalMeResponse"];
export type ClientWorkOrder = Schemas["ClientWorkOrder"];
export type ClientQuoteRow = Schemas["ClientQuoteRow"];
export type ClientContract = Schemas["ClientContract"];
export type ClientAccount = Schemas["ClientAccountResponse"];
export type ClientFile = Schemas["ClientFileResponse"];
export type ThreadItem = Schemas["TimelineItemResponse"];

export type ThreadSubject = "work_order" | "party";

const portalKey = ["portal"] as const;

export function usePortalMe() {
  return useQuery({
    queryKey: [...portalKey, "me"],
    queryFn: async () => unwrap(await portalApi.GET("/api/v1/portal/me")),
    staleTime: 5 * 60_000,
    retry: false,
  });
}

export function useRequestLink() {
  return useMutation({
    mutationFn: async (email: string) => {
      unwrap(await portalApi.POST("/api/v1/portal/auth/request-link", { body: { email } }));
    },
  });
}

export function useEnterPortal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (token: string) => {
      unwrap(await portalApi.POST("/api/v1/portal/auth/enter", { body: { token } }));
    },
    onSuccess: () => queryClient.removeQueries({ queryKey: portalKey }),
  });
}

export function useLeavePortal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      unwrap(await portalApi.POST("/api/v1/portal/auth/logout"));
    },
    onSettled: () => queryClient.removeQueries({ queryKey: portalKey }),
  });
}

export function usePortalWorkOrders() {
  return useQuery({ queryKey: [...portalKey, "work-orders"], queryFn: async () => unwrap(await portalApi.GET("/api/v1/jobs/portal/work-orders")) });
}

export function usePortalWorkOrder(id: string) {
  return useQuery({
    queryKey: [...portalKey, "work-orders", id],
    queryFn: async () => unwrap(await portalApi.GET("/api/v1/jobs/portal/work-orders/{id}", { params: { path: { id } } })),
  });
}

export function usePortalQuotes() {
  return useQuery({ queryKey: [...portalKey, "quotes"], queryFn: async () => unwrap(await portalApi.GET("/api/v1/sales/portal/quotes")) });
}

export function usePortalQuote(id: string) {
  return useQuery({
    queryKey: [...portalKey, "quotes", id],
    queryFn: async () => unwrap(await portalApi.GET("/api/v1/sales/portal/quotes/{id}", { params: { path: { id } } })),
  });
}

export function usePortalDecide(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ accept, note }: { accept: boolean; note: string }) =>
      unwrap(
        await portalApi.POST(accept ? "/api/v1/sales/portal/quotes/{id}/accept" : "/api/v1/sales/portal/quotes/{id}/reject", {
          params: { path: { id } },
          body: { note: note || null },
        }),
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...portalKey, "quotes"] }),
  });
}

export function usePortalContracts() {
  return useQuery({ queryKey: [...portalKey, "contracts"], queryFn: async () => unwrap(await portalApi.GET("/api/v1/sales/portal/contracts")) });
}

export function usePortalAccount() {
  return useQuery({ queryKey: [...portalKey, "account"], queryFn: async () => unwrap(await portalApi.GET("/api/v1/finance/portal/account")) });
}

const threadKey = (subjectType: ThreadSubject, subjectId: string) => [...portalKey, "thread", subjectType, subjectId] as const;

export function usePortalThread(subjectType: ThreadSubject, subjectId: string) {
  return useQuery({
    queryKey: threadKey(subjectType, subjectId),
    queryFn: async () =>
      unwrap(await portalApi.GET("/api/v1/timeline/portal/{subjectType}/{subjectId}", { params: { path: { subjectType, subjectId }, query: { limit: 50 } } })),
    // The team may answer while the page is open.
    refetchInterval: 30_000,
  });
}

export function usePortalSendMessage(subjectType: ThreadSubject, subjectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (text: string) =>
      unwrap(await portalApi.POST("/api/v1/timeline/portal/{subjectType}/{subjectId}/notes", { params: { path: { subjectType, subjectId } }, body: { text } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: threadKey(subjectType, subjectId) }),
  });
}

const filesKey = (subjectType: ThreadSubject, subjectId: string) => [...portalKey, "files", subjectType, subjectId] as const;

export function usePortalFiles(subjectType: ThreadSubject, subjectId: string) {
  return useQuery({
    queryKey: filesKey(subjectType, subjectId),
    queryFn: async () => unwrap(await portalApi.GET("/api/v1/files/portal", { params: { query: { subjectType, subjectId } } })),
  });
}

export function usePortalUpload(subjectType: ThreadSubject, subjectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (file: File) => {
      const form = new FormData();
      form.append("file", file);
      form.append("subjectType", subjectType);
      form.append("subjectId", subjectId);
      return unwrap(await portalApi.POST("/api/v1/files/portal", { body: form as never, bodySerializer: (body) => body as unknown as FormData }));
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: filesKey(subjectType, subjectId) });
      window.setTimeout(() => void queryClient.invalidateQueries({ queryKey: threadKey(subjectType, subjectId) }), 1500);
    },
  });
}

/** How far a job is, 0–100, from its checklist; null when it has none (then only the stage speaks). */
export function progressOf(workOrder: Pick<ClientWorkOrder, "tasksDone" | "tasksTotal" | "stageCategory">): number | null {
  if (workOrder.stageCategory === "done") return 100;
  if (workOrder.tasksTotal === 0) return null;
  return Math.round((workOrder.tasksDone / workOrder.tasksTotal) * 100);
}

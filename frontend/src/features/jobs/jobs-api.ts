"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type Board = Schemas["BoardResponse"];
export type Stage = Schemas["StageResponse"];
export type WorkOrderCard = Schemas["WorkOrderCard"];
export type WorkOrder = Schemas["WorkOrderResponse"];
export type WorkOrderTask = Schemas["WorkOrderTaskResponse"];
export type WorkOrderInput = Schemas["UpdateWorkOrderCommand"];
export type TimeEntry = Schemas["TimeEntryResponse"];
export type Member = Schemas["MemberSummary"];
export type Client = Schemas["PartySummary"];

export const priorities = ["low", "normal", "high", "urgent"] as const;
export type Priority = (typeof priorities)[number];

export const jobsKey = ["jobs"] as const;
const boardKey = (filter: BoardFilter) => [...jobsKey, "board", filter] as const;
const workOrderKey = (id: string) => [...jobsKey, "work-order", id] as const;
const timerKey = [...jobsKey, "timer"] as const;

export interface BoardFilter {
  search: string;
  mine: boolean;
}

export function useBoard(filter: BoardFilter) {
  return useQuery({
    queryKey: boardKey(filter),
    queryFn: async () =>
      unwrap(
        await api.GET("/api/v1/jobs/work-orders/board", {
          params: { query: { search: filter.search || undefined, mine: filter.mine || undefined } },
        }),
      ),
    placeholderData: keepPreviousData,
    // Teammates move cards too; the board catches up without a reload.
    refetchInterval: 30_000,
  });
}

export function useWorkOrder(id: string) {
  return useQuery({
    queryKey: workOrderKey(id),
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/work-orders/{id}", { params: { path: { id } } })),
    retry: false,
  });
}

export function usePartyWorkOrders(partyId: string) {
  return useQuery({
    queryKey: [...jobsKey, "party", partyId],
    queryFn: async () =>
      unwrap(
        await api.GET("/api/v1/jobs/work-orders", {
          params: { query: { partyId, includeDone: true, page: 1, pageSize: 20 } },
        }),
      ),
  });
}

/** Everything a work order change can affect: the board, lists, its detail and the timeline. */
function useRefreshJobs() {
  const queryClient = useQueryClient();
  return (workOrder?: WorkOrder) => {
    if (workOrder) queryClient.setQueryData(workOrderKey(workOrder.id), workOrder);
    void queryClient.invalidateQueries({ queryKey: jobsKey });
    refreshTimelineSoon(queryClient);
  };
}

export function useSaveWorkOrder(id?: string) {
  const refresh = useRefreshJobs();
  return useMutation({
    mutationFn: async (input: WorkOrderInput & { stageId?: string | null }) =>
      id
        ? unwrap(await api.PUT("/api/v1/jobs/work-orders/{id}", { params: { path: { id } }, body: input }))
        : unwrap(await api.POST("/api/v1/jobs/work-orders", { body: input })),
    onSuccess: (workOrder) => refresh(workOrder),
  });
}

export function useMoveWorkOrder() {
  const refresh = useRefreshJobs();
  return useMutation({
    mutationFn: async ({ id, stageId, index }: { id: string; stageId: string; index?: number }) =>
      unwrap(await api.POST("/api/v1/jobs/work-orders/{id}/move", { params: { path: { id } }, body: { stageId, index } })),
    onSettled: () => refresh(),
  });
}

export function useArchiveWorkOrder(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      unwrap(await api.DELETE("/api/v1/jobs/work-orders/{id}", { params: { path: { id } } }));
    },
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: workOrderKey(id) });
      void queryClient.invalidateQueries({ queryKey: jobsKey });
    },
  });
}

export function useTaskMutations(workOrderId: string) {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: jobsKey });
  const path = { id: workOrderId };

  return {
    add: useMutation({
      mutationFn: async (title: string) =>
        unwrap(await api.POST("/api/v1/jobs/work-orders/{id}/tasks", { params: { path }, body: { title } })),
      onSuccess: refresh,
    }),
    update: useMutation({
      mutationFn: async ({ taskId, title, isDone }: { taskId: string; title: string; isDone: boolean }) =>
        unwrap(
          await api.PUT("/api/v1/jobs/work-orders/{id}/tasks/{taskId}", {
            params: { path: { ...path, taskId } },
            body: { title, isDone },
          }),
        ),
      // A tick shows at once; the server's answer (or a rollback on failure) follows.
      onMutate: async ({ taskId, title, isDone }) => {
        const key = workOrderKey(workOrderId);
        await queryClient.cancelQueries({ queryKey: key });
        const previous = queryClient.getQueryData<WorkOrder>(key);
        if (previous) {
          queryClient.setQueryData<WorkOrder>(key, {
            ...previous,
            tasks: previous.tasks.map((task) => (task.id === taskId ? { ...task, title, isDone } : task)),
          });
        }
        return { previous };
      },
      onError: (_error, _input, context) => {
        if (context?.previous) queryClient.setQueryData(workOrderKey(workOrderId), context.previous);
      },
      onSettled: refresh,
    }),
    remove: useMutation({
      mutationFn: async (taskId: string) => {
        unwrap(await api.DELETE("/api/v1/jobs/work-orders/{id}/tasks/{taskId}", { params: { path: { ...path, taskId } } }));
      },
      onSuccess: refresh,
    }),
  };
}

export function useStages() {
  return useQuery({
    queryKey: [...jobsKey, "stages"],
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/stages")),
  });
}

export function useStageMutations() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: jobsKey });
  return {
    create: useMutation({
      mutationFn: async (body: { name: string; category: string }) => unwrap(await api.POST("/api/v1/jobs/stages", { body })),
      onSuccess: refresh,
    }),
    update: useMutation({
      mutationFn: async ({ id, ...body }: { id: string; name: string; category: string }) =>
        unwrap(await api.PUT("/api/v1/jobs/stages/{id}", { params: { path: { id } }, body })),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        unwrap(await api.DELETE("/api/v1/jobs/stages/{id}", { params: { path: { id } } }));
      },
      onSuccess: refresh,
    }),
    reorder: useMutation({
      mutationFn: async (stageIds: string[]) => unwrap(await api.PUT("/api/v1/jobs/stages/order", { body: { stageIds } })),
      onSuccess: refresh,
    }),
  };
}

export function useAssignableMembers() {
  return useQuery({
    queryKey: [...jobsKey, "members"],
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/assignable-members")),
    staleTime: 5 * 60_000,
  });
}

export function useClientSearch(search: string, enabled: boolean) {
  return useQuery({
    queryKey: [...jobsKey, "clients", search],
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/clients", { params: { query: { search: search || undefined } } })),
    enabled,
    placeholderData: keepPreviousData,
  });
}

/** The caller's running timer, or null. Shared by the header chip and the work order page. */
export function useRunningTimer(enabled = true) {
  return useQuery({
    queryKey: timerKey,
    enabled,
    queryFn: async () => {
      const result = await api.GET("/api/v1/jobs/time/timer");
      return result.response.status === 204 ? null : unwrap(result);
    },
    refetchInterval: 60_000,
  });
}

export function useTimerMutations() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: jobsKey });
  return {
    start: useMutation({
      mutationFn: async (workOrderId: string) =>
        unwrap(await api.POST("/api/v1/jobs/time/timer/start", { body: { workOrderId, isBillable: true } })),
      onSuccess: (entry) => {
        queryClient.setQueryData(timerKey, entry);
        void refresh();
      },
    }),
    stop: useMutation({
      mutationFn: async () => unwrap(await api.POST("/api/v1/jobs/time/timer/stop")),
      onSuccess: () => {
        queryClient.setQueryData(timerKey, null);
        void refresh();
      },
    }),
  };
}

export function useTimeEntries(from: string, to: string, userId?: string) {
  return useQuery({
    queryKey: [...jobsKey, "time", from, to, userId ?? "me"],
    queryFn: async () => unwrap(await api.GET("/api/v1/jobs/time/entries", { params: { query: { from, to, userId } } })),
    placeholderData: keepPreviousData,
  });
}

export function useTimeEntryMutations() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: jobsKey });
  return {
    log: useMutation({
      mutationFn: async (body: Schemas["LogTimeCommand"]) => unwrap(await api.POST("/api/v1/jobs/time/entries", { body })),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        unwrap(await api.DELETE("/api/v1/jobs/time/entries/{id}", { params: { path: { id } } }));
      },
      onSuccess: refresh,
    }),
  };
}

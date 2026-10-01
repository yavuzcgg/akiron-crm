"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type Employee = Schemas["EmployeeResponse"];
export type EmployeeInput = Schemas["UpdateEmployeeCommand"];
export type Leave = Schemas["LeaveResponse"];
export type MyLeave = Schemas["MyLeaveResponse"];
export type LeaveInput = Schemas["SubmitLeaveCommand"];

export const leaveTypes = ["annual", "sick", "excuse", "unpaid", "other"] as const;
export type LeaveType = (typeof leaveTypes)[number];

const peopleKey = ["people"] as const;

export function useEmployees() {
  return useQuery({
    queryKey: [...peopleKey, "employees"],
    queryFn: async () => unwrap(await api.GET("/api/v1/people/employees")),
  });
}

export function useUpdateEmployee(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: EmployeeInput) =>
      unwrap(await api.PUT("/api/v1/people/employees/{userId}", { params: { path: { userId } }, body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: peopleKey }),
  });
}

export function useMyLeave(year: number) {
  return useQuery({
    queryKey: [...peopleKey, "leave", "mine", year],
    queryFn: async () => unwrap(await api.GET("/api/v1/people/leave/mine", { params: { query: { year } } })),
  });
}

export function usePendingLeave(enabled: boolean) {
  return useQuery({
    queryKey: [...peopleKey, "leave", "pending"],
    queryFn: async () => unwrap(await api.GET("/api/v1/people/leave/pending")),
    enabled,
  });
}

export function useLeaveCalendar(from: string, to: string) {
  return useQuery({
    queryKey: [...peopleKey, "leave", "calendar", from, to],
    queryFn: async () => unwrap(await api.GET("/api/v1/people/leave/calendar", { params: { query: { from, to } } })),
  });
}

export function useLeaveMutations() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: peopleKey });
  return {
    submit: useMutation({
      mutationFn: async (body: LeaveInput) => unwrap(await api.POST("/api/v1/people/leave", { body })),
      onSuccess: refresh,
    }),
    cancel: useMutation({
      mutationFn: async (id: string) => unwrap(await api.POST("/api/v1/people/leave/{id}/cancel", { params: { path: { id } } })),
      onSuccess: refresh,
    }),
    decide: useMutation({
      mutationFn: async ({ id, approve, note }: { id: string; approve: boolean; note?: string }) =>
        unwrap(await api.POST("/api/v1/people/leave/{id}/decide", { params: { path: { id } }, body: { approve, note: note || null } })),
      onSuccess: refresh,
    }),
  };
}

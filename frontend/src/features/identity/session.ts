"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { api, type Schemas } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";

export type Session = Schemas["SessionResponse"];
export type LoginInput = Schemas["LoginCommand"];
export type RegisterInput = Schemas["RegisterCommand"];

export const sessionQueryKey = ["identity", "session"] as const;

/** The signed-in user, or null when there is no valid session. */
export function useSession() {
  return useQuery({
    queryKey: sessionQueryKey,
    queryFn: async (): Promise<Session | null> => {
      try {
        return unwrap(await api.GET("/api/v1/identity/auth/session"));
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },
    staleTime: 5 * 60_000,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: LoginInput) => unwrap(await api.POST("/api/v1/identity/auth/login", { body: input })),
    onSuccess: (session) => queryClient.setQueryData(sessionQueryKey, session),
  });
}

export function useRegister() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: RegisterInput) =>
      unwrap(await api.POST("/api/v1/identity/auth/register", { body: input })),
    onSuccess: (session) => queryClient.setQueryData(sessionQueryKey, session),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  const router = useRouter();
  return useMutation({
    mutationFn: async () => {
      await api.POST("/api/v1/identity/auth/logout");
    },
    onSettled: () => {
      // Another user may sign in on this browser next; nothing cached may leak to them.
      queryClient.clear();
      router.replace("/login");
    },
  });
}

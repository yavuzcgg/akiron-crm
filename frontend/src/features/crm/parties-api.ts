"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type Party = Schemas["PartyResponse"];
export type PartyListItem = Schemas["PartyListItem"];
export type PartyContact = Schemas["PartyContactResponse"];
export type PartyInput = Schemas["UpdatePartyCommand"];
export type ContactInput = Schemas["ContactCommand"];
export type PartyRoleFilter = "all" | "customer" | "supplier";

export const partiesKey = ["crm", "parties"] as const;
const partyKey = (id: string) => [...partiesKey, "detail", id] as const;

export const partiesPageSize = 25;

export function useParties(search: string, role: PartyRoleFilter, page: number) {
  return useQuery({
    queryKey: [...partiesKey, "list", { search, role, page }],
    queryFn: async () =>
      unwrap(
        await api.GET("/api/v1/crm/parties", {
          params: {
            query: {
              page,
              pageSize: partiesPageSize,
              search: search || undefined,
              role: role === "all" ? undefined : role,
            },
          },
        }),
      ),
    // Typing in the search box keeps the last results on screen instead of flashing a skeleton.
    placeholderData: keepPreviousData,
  });
}

export function useParty(id: string) {
  return useQuery({
    queryKey: partyKey(id),
    queryFn: async () => unwrap(await api.GET("/api/v1/crm/parties/{id}", { params: { path: { id } } })),
    retry: false,
  });
}

export function useSaveParty(id?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: PartyInput & { code?: string | null }) =>
      id
        ? unwrap(await api.PUT("/api/v1/crm/parties/{id}", { params: { path: { id } }, body: input }))
        : unwrap(await api.POST("/api/v1/crm/parties", { body: input })),
    onSuccess: (party) => {
      queryClient.setQueryData(partyKey(party.id), party);
      void queryClient.invalidateQueries({ queryKey: [...partiesKey, "list"] });
      refreshTimelineSoon(queryClient);
    },
  });
}

export function useArchiveParty(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      unwrap(await api.DELETE("/api/v1/crm/parties/{id}", { params: { path: { id } } }));
    },
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: partyKey(id) });
      void queryClient.invalidateQueries({ queryKey: [...partiesKey, "list"] });
    },
  });
}

export function useSaveContact(partyId: string, contactId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: ContactInput) =>
      contactId
        ? unwrap(
            await api.PUT("/api/v1/crm/parties/{id}/contacts/{contactId}", {
              params: { path: { id: partyId, contactId } },
              body: input,
            }),
          )
        : unwrap(await api.POST("/api/v1/crm/parties/{id}/contacts", { params: { path: { id: partyId } }, body: input })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: partyKey(partyId) });
      refreshTimelineSoon(queryClient);
    },
  });
}

export function useRemoveContact(partyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (contactId: string) => {
      unwrap(
        await api.DELETE("/api/v1/crm/parties/{id}/contacts/{contactId}", {
          params: { path: { id: partyId, contactId } },
        }),
      );
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partyKey(partyId) }),
  });
}

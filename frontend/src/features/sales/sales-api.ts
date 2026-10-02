"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type CatalogItem = Schemas["CatalogItemResponse"];
export type CatalogInput = Schemas["CatalogItemCommand"];
export type Quote = Schemas["QuoteResponse"];
export type QuoteListItem = Schemas["QuoteListItem"];
export type QuoteInput = Schemas["QuoteCommand"];
export type QuoteLineInput = Schemas["QuoteLineCommand"];
export type PublicQuote = Schemas["PublicQuoteResponse"];
export type SendResult = Schemas["SendQuoteResponse"];

export const quoteStatuses = ["draft", "sent", "accepted", "rejected", "expired"] as const;
export type QuoteStatus = (typeof quoteStatuses)[number];

const salesKey = ["sales"] as const;
const quoteKey = (id: string) => [...salesKey, "quote", id] as const;

export function useCatalog(search = "") {
  return useQuery({
    queryKey: [...salesKey, "catalog", search],
    queryFn: async () => unwrap(await api.GET("/api/v1/sales/catalog", { params: { query: { search: search || undefined } } })),
    placeholderData: keepPreviousData,
  });
}

export function useCatalogMutations() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: [...salesKey, "catalog"] });
  return {
    save: useMutation({
      mutationFn: async ({ id, ...body }: CatalogInput & { id?: string }) =>
        id
          ? unwrap(await api.PUT("/api/v1/sales/catalog/{id}", { params: { path: { id } }, body }))
          : unwrap(await api.POST("/api/v1/sales/catalog", { body })),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        unwrap(await api.DELETE("/api/v1/sales/catalog/{id}", { params: { path: { id } } }));
      },
      onSuccess: refresh,
    }),
  };
}

export function useQuotes(filter: { search: string; status: string; partyId?: string }, page: number, pageSize = 25) {
  return useQuery({
    queryKey: [...salesKey, "quotes", filter, page],
    queryFn: async () =>
      unwrap(
        await api.GET("/api/v1/sales/quotes", {
          params: {
            query: {
              page,
              pageSize,
              search: filter.search || undefined,
              status: filter.status === "all" ? undefined : filter.status,
              partyId: filter.partyId,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useQuote(id: string | undefined) {
  return useQuery({
    queryKey: quoteKey(id ?? "new"),
    queryFn: async () => unwrap(await api.GET("/api/v1/sales/quotes/{id}", { params: { path: { id: id! } } })),
    enabled: !!id,
    retry: false,
  });
}

export function useQuoteMutations(id?: string) {
  const queryClient = useQueryClient();
  const settle = (quote?: Quote) => {
    if (quote) queryClient.setQueryData(quoteKey(quote.id), quote);
    void queryClient.invalidateQueries({ queryKey: [...salesKey, "quotes"] });
    refreshTimelineSoon(queryClient);
  };

  return {
    save: useMutation({
      mutationFn: async (body: QuoteInput) =>
        id
          ? unwrap(await api.PUT("/api/v1/sales/quotes/{id}", { params: { path: { id } }, body }))
          : unwrap(await api.POST("/api/v1/sales/quotes", { body })),
      onSuccess: settle,
    }),
    send: useMutation({
      mutationFn: async (email: boolean) =>
        unwrap(await api.POST("/api/v1/sales/quotes/{id}/send", { params: { path: { id: id! } }, body: { email } })),
      onSuccess: (result) => settle(result.quote),
    }),
    revise: useMutation({
      mutationFn: async () => unwrap(await api.POST("/api/v1/sales/quotes/{id}/revise", { params: { path: { id: id! } } })),
      onSuccess: settle,
    }),
    remove: useMutation({
      mutationFn: async () => {
        unwrap(await api.DELETE("/api/v1/sales/quotes/{id}", { params: { path: { id: id! } } }));
      },
      onSuccess: () => settle(),
    }),
  };
}

/** The client's side, through the link secret; no session needed. */
export function usePublicQuote(token: string) {
  return useQuery({
    queryKey: ["public-quote", token],
    queryFn: async () => unwrap(await api.GET("/api/v1/sales/public/quotes/{token}", { params: { path: { token } } })),
    retry: false,
  });
}

export function useDecideQuote(token: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ accept, name, note }: { accept: boolean; name: string; note?: string }) => {
      const options = { params: { path: { token } }, body: { name, note: note || null } };
      return unwrap(
        accept
          ? await api.POST("/api/v1/sales/public/quotes/{token}/accept", options)
          : await api.POST("/api/v1/sales/public/quotes/{token}/reject", options),
      );
    },
    onSuccess: (quote) => queryClient.setQueryData(["public-quote", token], quote),
  });
}

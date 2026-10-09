"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type Contract = Schemas["ContractResponse"];
export type ContractInput = Schemas["CreateContractCommand"];
export type ContractTerms = Schemas["UpdateContractCommand"];

const contractsKey = ["contracts"] as const;

export function useContracts(filter: { status?: string; partyId?: string; search?: string }, page = 1, pageSize = 50) {
  return useQuery({
    queryKey: [...contractsKey, "list", filter, page, pageSize],
    queryFn: async () =>
      unwrap(
        await api.GET("/api/v1/sales/contracts", {
          params: { query: { page, pageSize, status: filter.status || undefined, partyId: filter.partyId, search: filter.search || undefined } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useContract(id: string) {
  return useQuery({
    queryKey: [...contractsKey, "one", id],
    queryFn: async () => unwrap(await api.GET("/api/v1/sales/contracts/{id}", { params: { path: { id } } })),
  });
}

/** What a contract has billed so far: its finance charges, newest first. */
export function useContractCharges(contractId: string, enabled: boolean) {
  return useQuery({
    queryKey: ["finance", "charges", "contract", contractId],
    queryFn: async () => unwrap(await api.GET("/api/v1/finance/charges", { params: { query: { contractId } } })),
    enabled,
  });
}

export function useContractMutations() {
  const queryClient = useQueryClient();
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: contractsKey });
    // A start or a resume can bill a month at once; Finance records it moments later.
    window.setTimeout(() => void queryClient.invalidateQueries({ queryKey: ["finance"] }), 1500);
    refreshTimelineSoon(queryClient);
  };
  return {
    create: useMutation({
      mutationFn: async (body: ContractInput) => unwrap(await api.POST("/api/v1/sales/contracts", { body })),
      onSuccess: refresh,
    }),
    update: useMutation({
      mutationFn: async ({ id, ...body }: ContractTerms & { id: string }) => unwrap(await api.PUT("/api/v1/sales/contracts/{id}", { params: { path: { id } }, body })),
      onSuccess: refresh,
    }),
    pause: useMutation({
      mutationFn: async (id: string) => unwrap(await api.POST("/api/v1/sales/contracts/{id}/pause", { params: { path: { id } } })),
      onSuccess: refresh,
    }),
    resume: useMutation({
      mutationFn: async (id: string) => unwrap(await api.POST("/api/v1/sales/contracts/{id}/resume", { params: { path: { id } } })),
      onSuccess: refresh,
    }),
    end: useMutation({
      mutationFn: async ({ id, reason }: { id: string; reason: string }) => unwrap(await api.POST("/api/v1/sales/contracts/{id}/end", { params: { path: { id } }, body: { reason } })),
      onSuccess: refresh,
    }),
  };
}

/** Monthly recurring revenue of the active contracts, per currency. */
export function monthlyTotals(contracts: Contract[]): { currency: string; amount: number }[] {
  const totals = new Map<string, number>();
  for (const contract of contracts) {
    if (contract.status === "active") totals.set(contract.currency, (totals.get(contract.currency) ?? 0) + contract.amount);
  }
  return [...totals].map(([currency, amount]) => ({ currency, amount }));
}

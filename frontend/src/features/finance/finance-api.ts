"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type Charge = Schemas["ChargeResponse"];
export type ChargeInput = Schemas["RecordChargeCommand"];
export type Receipt = Schemas["ReceiptResponse"];
export type ReceiptInput = Schemas["RecordReceiptCommand"];
export type Statement = Schemas["StatementResponse"];
export type OpenItemRow = Schemas["OpenItemRow"];
export type PartyBalance = Schemas["PartyBalanceRow"];
export type FinanceSummary = Schemas["FinanceSummary"];

export const paymentMethods = ["transfer", "cash", "card", "check", "note", "other"] as const;

const financeKey = ["finance"] as const;

/** After any money move, every view of money is stale: balances, statements, lists. */
function useRefreshFinance() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: financeKey });
    refreshTimelineSoon(queryClient);
  };
}

export function useFinanceSummary() {
  return useQuery({ queryKey: [...financeKey, "summary"], queryFn: async () => unwrap(await api.GET("/api/v1/finance/summary")) });
}

export function useBalances() {
  return useQuery({ queryKey: [...financeKey, "balances"], queryFn: async () => unwrap(await api.GET("/api/v1/finance/balances")) });
}

export function useOpenItems(filter: { partyId?: string; overdue?: boolean } = {}) {
  return useQuery({
    queryKey: [...financeKey, "open-items", filter],
    queryFn: async () => unwrap(await api.GET("/api/v1/finance/open-items", { params: { query: { partyId: filter.partyId, overdue: filter.overdue || undefined } } })),
  });
}

export function useStatement(partyId: string) {
  return useQuery({
    queryKey: [...financeKey, "statement", partyId],
    queryFn: async () => unwrap(await api.GET("/api/v1/finance/parties/{partyId}/statement", { params: { path: { partyId } } })),
  });
}

export function useReceipts(page: number) {
  return useQuery({
    queryKey: [...financeKey, "receipts", page],
    queryFn: async () => unwrap(await api.GET("/api/v1/finance/receipts", { params: { query: { page, pageSize: 25 } } })),
    placeholderData: keepPreviousData,
  });
}

export function useToInvoice() {
  return useQuery({ queryKey: [...financeKey, "to-invoice"], queryFn: async () => unwrap(await api.GET("/api/v1/finance/charges/to-invoice")) });
}

export function useFinanceMutations() {
  const refresh = useRefreshFinance();
  return {
    charge: useMutation({
      mutationFn: async (body: ChargeInput) => unwrap(await api.POST("/api/v1/finance/charges", { body })),
      onSuccess: refresh,
    }),
    receive: useMutation({
      mutationFn: async (body: ReceiptInput) => unwrap(await api.POST("/api/v1/finance/receipts", { body })),
      onSuccess: refresh,
    }),
    allocate: useMutation({
      mutationFn: async (id: string) =>
        unwrap(await api.POST("/api/v1/finance/receipts/{id}/allocate", { params: { path: { id } }, body: { autoAllocate: true } })),
      onSuccess: refresh,
    }),
    reverse: useMutation({
      mutationFn: async ({ id, reason }: { id: string; reason: string }) =>
        unwrap(await api.POST("/api/v1/finance/receipts/{id}/reverse", { params: { path: { id } }, body: { reason } })),
      onSuccess: refresh,
    }),
    cancelCharge: useMutation({
      mutationFn: async ({ id, reason }: { id: string; reason: string }) =>
        unwrap(await api.POST("/api/v1/finance/charges/{id}/cancel", { params: { path: { id } }, body: { reason } })),
      onSuccess: refresh,
    }),
    remind: useMutation({
      mutationFn: async ({ id, email }: { id: string; email?: string }) =>
        unwrap(await api.POST("/api/v1/finance/open-items/{id}/remind", { params: { path: { id } }, body: { email: email || null } })),
      onSuccess: refresh,
    }),
    invoiced: useMutation({
      mutationFn: async ({ id, invoiceNumber, invoiceDate }: { id: string; invoiceNumber: string; invoiceDate: string }) =>
        unwrap(await api.POST("/api/v1/finance/charges/{id}/invoiced", { params: { path: { id } }, body: { invoiceNumber, invoiceDate } })),
      onSuccess: refresh,
    }),
  };
}

/** Equal monthly instalments; the last one takes the kuruş left over so the plan adds up exactly. */
export function splitMonthly(total: number, count: number, firstDue: string): { dueDate: string; amount: number }[] {
  const cents = Math.round(total * 100);
  const part = Math.floor(cents / count);
  const start = new Date(`${firstDue}T00:00:00`);
  return Array.from({ length: count }, (_, index) => {
    const lastDay = new Date(start.getFullYear(), start.getMonth() + index + 1, 0).getDate();
    const date = new Date(start.getFullYear(), start.getMonth() + index, Math.min(start.getDate(), lastDay));
    const dueDate = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
    const amount = (index === count - 1 ? cents - part * (count - 1) : part) / 100;
    return { dueDate, amount };
  });
}

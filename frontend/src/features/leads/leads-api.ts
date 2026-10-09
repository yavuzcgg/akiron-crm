"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";

export type Lead = Schemas["LeadResponse"];
export type LeadInput = Schemas["LeadCommand"];
export type LeadFormSettings = Schemas["LeadFormResponse"];

export const openStages = ["new", "contacted", "meeting", "proposal"] as const;
export const leadSources = ["website", "web_form", "referral", "instagram", "whatsapp", "phone", "email", "event", "other"] as const;

const leadsKey = ["leads"] as const;

export function useLeads(view: "open" | "closed", filter: { search?: string; ownerId?: string; partyId?: string } = {}) {
  return useQuery({
    queryKey: [...leadsKey, "list", view, filter],
    queryFn: async () =>
      unwrap(
        await api.GET("/api/v1/crm/leads", {
          params: { query: { view, search: filter.search || undefined, ownerId: filter.ownerId, partyId: filter.partyId } },
        }),
      ),
  });
}

export function useLead(id: string) {
  return useQuery({
    queryKey: [...leadsKey, "one", id],
    queryFn: async () => unwrap(await api.GET("/api/v1/crm/leads/{id}", { params: { path: { id } } })),
  });
}

export function useLeadMutations() {
  const queryClient = useQueryClient();
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: leadsKey });
    refreshTimelineSoon(queryClient);
  };
  return {
    save: useMutation({
      mutationFn: async ({ id, ...body }: LeadInput & { id?: string }) =>
        id
          ? unwrap(await api.PUT("/api/v1/crm/leads/{id}", { params: { path: { id } }, body }))
          : unwrap(await api.POST("/api/v1/crm/leads", { body })),
      onSuccess: refresh,
    }),
    move: useMutation({
      mutationFn: async ({ id, stage, index, lostReason }: { id: string; stage: string; index?: number; lostReason?: string }) =>
        unwrap(await api.POST("/api/v1/crm/leads/{id}/move", { params: { path: { id } }, body: { stage, index, lostReason } })),
      onSuccess: refresh,
    }),
    convert: useMutation({
      mutationFn: async ({ id, partyId }: { id: string; partyId?: string }) =>
        unwrap(await api.POST("/api/v1/crm/leads/{id}/convert", { params: { path: { id } }, body: { partyId } })),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: async (id: string) => {
        unwrap(await api.DELETE("/api/v1/crm/leads/{id}", { params: { path: { id } } }));
      },
      onSuccess: refresh,
    }),
  };
}

export function useLeadForm(enabled = true) {
  return useQuery({
    queryKey: [...leadsKey, "form"],
    queryFn: async () => unwrap(await api.GET("/api/v1/crm/lead-form")),
    enabled,
  });
}

export function useLeadFormMutations() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: [...leadsKey, "form"] });
  return {
    save: useMutation({
      mutationFn: async (body: Schemas["LeadFormCommand"]) => unwrap(await api.PUT("/api/v1/crm/lead-form", { body })),
      onSuccess: refresh,
    }),
    rotate: useMutation({
      mutationFn: async () => unwrap(await api.POST("/api/v1/crm/lead-form/rotate")),
      onSuccess: refresh,
    }),
  };
}

/** The open pipeline per currency: what the board is worth if everything closes. */
export function pipelineTotals(leads: Lead[]): { currency: string; amount: number }[] {
  const totals = new Map<string, number>();
  for (const lead of leads) {
    if (lead.estimatedValue) totals.set(lead.currency, (totals.get(lead.currency) ?? 0) + lead.estimatedValue);
  }
  return [...totals].map(([currency, amount]) => ({ currency, amount }));
}

/** The HTML a site pastes to send visitors to the board; plain form post, no script needed. */
export function embedSnippet(origin: string, key: string, labels: { name: string; email: string; phone: string; company: string; message: string; send: string }) {
  return `<form action="${origin}/api/v1/crm/public/forms/${key}" method="post">
  <input name="name" placeholder="${labels.name}" required>
  <input name="email" type="email" placeholder="${labels.email}">
  <input name="phone" placeholder="${labels.phone}">
  <input name="company" placeholder="${labels.company}">
  <textarea name="message" placeholder="${labels.message}"></textarea>
  <input name="website" tabindex="-1" autocomplete="off" style="position:absolute;left:-9999px" aria-hidden="true">
  <button type="submit">${labels.send}</button>
</form>`;
}

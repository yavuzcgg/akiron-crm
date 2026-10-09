import { describe, expect, it } from "vitest";
import { monthlyTotals, type Contract } from "@/features/contracts/contracts-api";
import { embedSnippet, pipelineTotals, type Lead } from "./leads-api";

describe("pipelineTotals", () => {
  it("adds estimated values per currency and skips leads without one", () => {
    const leads = [
      { estimatedValue: 10_000, currency: "TRY" },
      { estimatedValue: null, currency: "TRY" },
      { estimatedValue: 2_500.5, currency: "TRY" },
      { estimatedValue: 1_000, currency: "EUR" },
    ] as Lead[];
    expect(pipelineTotals(leads)).toEqual([
      { currency: "TRY", amount: 12_500.5 },
      { currency: "EUR", amount: 1_000 },
    ]);
  });
});

describe("monthlyTotals", () => {
  it("counts only active contracts", () => {
    const contracts = [
      { status: "active", amount: 12_000, currency: "TRY" },
      { status: "paused", amount: 5_000, currency: "TRY" },
      { status: "active", amount: 3_000, currency: "TRY" },
    ] as Contract[];
    expect(monthlyTotals(contracts)).toEqual([{ currency: "TRY", amount: 15_000 }]);
  });
});

describe("embedSnippet", () => {
  it("posts to the tenant's public form and carries the hidden honeypot field", () => {
    const html = embedSnippet("https://app.akiron.example", "abc123", { name: "Ad", email: "E-posta", phone: "Tel", company: "Firma", message: "Mesaj", send: "Gönder" });
    expect(html).toContain('action="https://app.akiron.example/api/v1/crm/public/forms/abc123"');
    expect(html).toContain('name="website"');
    expect(html).toContain(">Gönder</button>");
  });
});

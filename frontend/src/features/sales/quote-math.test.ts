import { describe, expect, it } from "vitest";
import { lineAmounts, parseDecimal, quoteTotals } from "./quote-math";

describe("quote math", () => {
  it("matches the API for VAT-inclusive prices, discounts and withholding", () => {
    const totals = quoteTotals([
      { quantity: 1, unitPrice: 18_000, discountPercent: 0, vatRate: 20, withholdingTenths: 0, priceIncludesVat: true },
      { quantity: 3, unitPrice: 10_000, discountPercent: 10, vatRate: 20, withholdingTenths: 3 },
    ]);

    expect(totals).toEqual({ gross: 45_000, discount: 3_000, net: 42_000, vat: 8_400, withholding: 1_620, total: 48_780 });
  });

  it("rounds each line once, half away from zero", () => {
    expect(lineAmounts({ quantity: 12, unitPrice: 49.995, discountPercent: 0, vatRate: 20, withholdingTenths: 0 }).vat).toBe(119.99);
    expect(lineAmounts({ quantity: 1, unitPrice: 0.125, discountPercent: 0, vatRate: 0, withholdingTenths: 0 }).net).toBe(0.13);
  });

  it("reads Turkish and plain decimals", () => {
    expect(parseDecimal("1.234,5")).toBe(1234.5);
    expect(parseDecimal("1234.50")).toBe(1234.5);
    expect(parseDecimal("7.000")).toBe(7000);
    expect(parseDecimal("abc")).toBeNaN();
  });
});

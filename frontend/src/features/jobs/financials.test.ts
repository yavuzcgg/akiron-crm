import { describe, expect, it } from "vitest";
import { parseAmount } from "./financials";

describe("parseAmount", () => {
  it("reads Turkish and plain amounts", () => {
    expect(parseAmount("12.500,50")).toBe(12500.5);
    expect(parseAmount("12500.5")).toBe(12500.5);
    expect(parseAmount("₺ 7.000")).toBe(7000);
    expect(parseAmount("")).toBeNull();
  });

  it("refuses what is not an amount", () => {
    expect(parseAmount("on bin")).toBeNaN();
    expect(parseAmount("1,234")).toBeNaN();
  });
});

import { describe, expect, it } from "vitest";
import { formatMoney, initials } from "./format";

describe("formatMoney", () => {
  it("uses Turkish separators", () => {
    // Intl inserts a non-breaking space between the symbol and the amount.
    expect(formatMoney(1234.5).replace(/\s/g, " ")).toBe("₺1.234,50");
  });
});

describe("initials", () => {
  it("upper-cases with Turkish rules", () => {
    expect(initials("ilker çelik")).toBe("İÇ");
  });

  it("uses at most two words", () => {
    expect(initials("Ayşe Nur Yılmaz")).toBe("AN");
  });
});

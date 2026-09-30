import { describe, expect, it } from "vitest";
import { formatDuration, formatElapsed, formatMoney, initials } from "./format";

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

describe("formatDuration", () => {
  it("shows hours and minutes, dropping a zero part", () => {
    expect(formatDuration(90)).toMatch(/^1 sa\.? 30 dk\.?$/);
    expect(formatDuration(120)).toMatch(/^2 sa\.?$/);
    expect(formatDuration(5)).toMatch(/^5 dk\.?$/);
  });
});

describe("formatElapsed", () => {
  it("pads minutes and seconds", () => {
    expect(formatElapsed(3723)).toBe("1:02:03");
    expect(formatElapsed(59)).toBe("0:00:59");
  });
});

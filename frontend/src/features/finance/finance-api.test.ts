import { describe, expect, it } from "vitest";
import { splitMonthly } from "./finance-api";

describe("splitMonthly", () => {
  it("splits evenly and puts the leftover kuruş on the last instalment", () => {
    const plan = splitMonthly(1000, 3, "2026-10-15");
    expect(plan.map((item) => item.amount)).toEqual([333.33, 333.33, 333.34]);
    expect(plan.map((item) => item.dueDate)).toEqual(["2026-10-15", "2026-11-15", "2026-12-15"]);
  });

  it("keeps month ends within shorter months", () => {
    expect(splitMonthly(300, 3, "2026-01-31").map((item) => item.dueDate)).toEqual(["2026-01-31", "2026-02-28", "2026-03-31"]);
  });
});

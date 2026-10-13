import { describe, expect, it } from "vitest";
import { monthGrid, shortTime } from "./content-api";

describe("monthGrid", () => {
  it("starts on the Monday on or before the 1st and ends on a Sunday", () => {
    // October 2026 starts on a Thursday and ends on a Saturday.
    const days = monthGrid(2026, 9);
    expect(days[0]).toBe("2026-09-28");
    expect(days.at(-1)).toBe("2026-11-01");
    expect(days).toHaveLength(35);
  });

  it("needs six weeks when the month starts late in the week", () => {
    // August 2026 starts on a Saturday.
    const days = monthGrid(2026, 7);
    expect(days[0]).toBe("2026-07-27");
    expect(days).toHaveLength(42);
  });

  it("uses exactly four weeks for a February that starts on a Monday", () => {
    expect(monthGrid(2027, 1)).toHaveLength(28);
  });
});

describe("shortTime", () => {
  it("drops the seconds and tolerates a missing time", () => {
    expect(shortTime("14:30:00")).toBe("14:30");
    expect(shortTime(null)).toBe("");
  });
});

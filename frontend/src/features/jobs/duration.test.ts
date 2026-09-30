import { describe, expect, it } from "vitest";
import { parseDuration } from "./duration";

describe("parseDuration", () => {
  it("reads clock, decimal hours and plain minutes", () => {
    expect(parseDuration("1:30")).toBe(90);
    expect(parseDuration("1,5")).toBe(90);
    expect(parseDuration("2")).toBe(120);
    expect(parseDuration("45")).toBe(45);
  });

  it("refuses what is not a duration", () => {
    expect(parseDuration("bir saat")).toBeNull();
    expect(parseDuration("1:75")).toBeNull();
    expect(parseDuration("")).toBeNull();
  });
});

import { describe, expect, it } from "vitest";
import { progressOf } from "./portal-api";

describe("progressOf", () => {
  it("is the share of checklist items done", () => {
    expect(progressOf({ tasksDone: 1, tasksTotal: 3, stageCategory: "active" })).toBe(33);
  });

  it("says nothing for a job without a checklist, so only the stage speaks", () => {
    expect(progressOf({ tasksDone: 0, tasksTotal: 0, stageCategory: "open" })).toBeNull();
  });

  it("is complete once the job is done, whatever was left unticked", () => {
    expect(progressOf({ tasksDone: 2, tasksTotal: 5, stageCategory: "done" })).toBe(100);
  });
});

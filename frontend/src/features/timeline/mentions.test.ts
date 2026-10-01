import { describe, expect, it } from "vitest";
import { fold, mentionQuery, mentionsIn } from "./mentions";

describe("mentions", () => {
  it("finds the @word being typed before the caret", () => {
    expect(mentionQuery("Merhaba @Ay", 11)).toEqual({ query: "Ay", start: 8 });
    expect(mentionQuery("@", 1)).toEqual({ query: "", start: 0 });
    expect(mentionQuery("e-posta@firma", 13)).toBeNull();
  });

  it("keeps only the mentions still written in the text", () => {
    const ayse = { userId: "1", name: "Ayşe Yılmaz" };
    const mert = { userId: "2", name: "Mert Kaya" };
    expect(mentionsIn("@Ayşe Yılmaz bakar mısın?", [ayse, mert, ayse])).toEqual([ayse]);
  });

  it("folds Turkish letters for matching", () => {
    expect(fold("IŞIK Çağlar")).toBe("isik caglar");
  });
});

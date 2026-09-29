import { describe, expect, it } from "vitest";
import { en } from "./en";
import { tr } from "./tr";
import { translate, translateErrorCode } from "./translate";

describe("translate", () => {
  it("fills placeholders", () => {
    expect(translate("tr", "validation.min_length", { minLength: 10 })).toBe("En az 10 karakter olmalı.");
  });

  it("leaves unknown placeholders visible", () => {
    expect(translate("en", "dashboard.welcome")).toBe("Welcome, {name}");
  });

  it("keeps every language on the same keys", () => {
    expect(Object.keys(en).sort()).toEqual(Object.keys(tr).sort());
  });
});

describe("translateErrorCode", () => {
  it("maps an API error code to its message", () => {
    expect(translateErrorCode("tr", "identity.auth.invalid_credentials")).toBe("E-posta veya parola hatalı.");
  });

  it("maps a validation rule code with its parameters", () => {
    expect(translateErrorCode("en", "validation.max_length", { maxLength: 200 })).toBe("Must be at most 200 characters.");
  });

  it("falls back to the generic message for a code it does not know yet", () => {
    expect(translateErrorCode("en", "finance.invoice.locked_period", { correlationId: "abc" })).toBe(
      "Something went wrong. If it keeps happening, quote this code: abc",
    );
  });
});

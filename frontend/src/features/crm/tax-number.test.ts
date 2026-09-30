import { describe, expect, it } from "vitest";
import { isValidTaxNumber, isValidTckn, isValidVkn } from "./tax-number";

describe("tax numbers", () => {
  it("accepts a VKN with the right check digit and refuses a typo", () => {
    expect(isValidVkn("1234567890")).toBe(true);
    expect(isValidVkn("4567890128")).toBe(true);
    expect(isValidVkn("1234567891")).toBe(false);
    expect(isValidVkn("123456789")).toBe(false);
  });

  it("accepts a TCKN with the right check digits and refuses a leading zero", () => {
    expect(isValidTckn("10000000146")).toBe(true);
    expect(isValidTckn("10000000147")).toBe(false);
    expect(isValidTckn("00000000146")).toBe(false);
  });

  it("lets a person use a sole trader's VKN but not a company a TCKN", () => {
    expect(isValidTaxNumber("person", "1234567890")).toBe(true);
    expect(isValidTaxNumber("company", "10000000146")).toBe(false);
  });
});

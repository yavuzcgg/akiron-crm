import { en } from "./en";
import { tr, type Dictionary, type TranslationKey } from "./tr";

export const locales = ["tr", "en"] as const;
export type Locale = (typeof locales)[number];
export const defaultLocale: Locale = "tr";

export type TranslationParams = Record<string, string | number | null | undefined>;

const dictionaries: Record<Locale, Dictionary> = { tr, en };

export function isLocale(value: unknown): value is Locale {
  return typeof value === "string" && (locales as readonly string[]).includes(value);
}

export function isTranslationKey(value: string): value is TranslationKey {
  return value in tr;
}

/** Looks the key up and fills `{placeholders}`; unknown placeholders are left visible so they get noticed. */
export function translate(locale: Locale, key: TranslationKey, params?: TranslationParams): string {
  const template = dictionaries[locale][key];
  if (!params) return template;

  return template.replace(/\{(\w+)\}/g, (match, name: string) => {
    const value = params[name];
    return value === null || value === undefined ? match : String(value);
  });
}

/**
 * Text for an API error code (ADR-0006). Validation rules use `validation.*` keys, everything else
 * `error.*`; a code the client does not know yet falls back to the generic message.
 */
export function translateErrorCode(locale: Locale, code: string | undefined, params?: TranslationParams): string {
  if (code) {
    const candidates = [code, `error.${code}`];
    for (const candidate of candidates) {
      if (isTranslationKey(candidate)) return translate(locale, candidate, params);
    }
  }

  return translate(locale, "error.common.unexpected", params);
}

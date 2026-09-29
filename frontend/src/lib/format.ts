/**
 * The only place money and dates are formatted (AGENTS.md). Turkish formatting is the product
 * default: 1.234,56 ₺ and 29 Eyl 2026.
 */

const locale = "tr-TR";

export function formatMoney(value: number, currency = "TRY"): string {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat(locale).format(value);
}

export function formatDate(value: string | Date): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(date);
}

export function formatDateTime(value: string | Date): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

/** "Yavuz Çelik" → "YÇ", with Turkish upper-casing so "i" becomes "İ". */
export function initials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toLocaleUpperCase(locale))
    .join("");
}

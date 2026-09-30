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

export function formatTime(value: string | Date): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return new Intl.DateTimeFormat(locale, { hour: "2-digit", minute: "2-digit" }).format(date);
}

/** Local calendar day as YYYY-MM-DD, for grouping timeline entries by day. */
export function dayKey(value: string | Date): string {
  const date = typeof value === "string" ? new Date(value) : value;
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

/** 1536 → "1,5 KB"; binary units, as file managers show them. */
export function formatFileSize(bytes: number): string {
  const units = ["B", "KB", "MB", "GB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: unit === 0 ? 0 : 1 }).format(value)} ${units[unit]}`;
}

/** 90 → "1 sa. 30 dk."; 0 → "0 dk.". Logged time on cards, detail pages and timesheets. */
export function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  const unit = (value: number, name: "hour" | "minute") =>
    new Intl.NumberFormat(locale, { style: "unit", unit: name, unitDisplay: "short" }).format(value);
  if (hours === 0) return unit(rest, "minute");
  return rest === 0 ? unit(hours, "hour") : `${unit(hours, "hour")} ${unit(rest, "minute")}`;
}

/** 3723 → "1:02:03": a running timer. */
export function formatElapsed(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const rest = seconds % 60;
  return `${hours}:${String(minutes).padStart(2, "0")}:${String(rest).padStart(2, "0")}`;
}

/** "Sal 30 Eyl": a day column in a timesheet. */
export function formatWeekday(value: string | Date): string {
  const date = typeof value === "string" ? new Date(`${value}T00:00:00`) : value;
  return new Intl.DateTimeFormat(locale, { weekday: "short", day: "numeric", month: "short" }).format(date);
}

/** A calendar date the API sent as YYYY-MM-DD, shown without shifting it through UTC. */
export function formatCalendarDate(value: string): string {
  return formatDate(new Date(`${value}T00:00:00`));
}

import type { TranslationKey } from "@/lib/i18n";

export const units = ["piece", "hour", "day", "month", "year", "project", "package", "post", "page", "word"] as const;

export const unitLabels: Record<string, TranslationKey> = {
  piece: "sales.unit.piece",
  hour: "sales.unit.hour",
  day: "sales.unit.day",
  month: "sales.unit.month",
  year: "sales.unit.year",
  project: "sales.unit.project",
  package: "sales.unit.package",
  post: "sales.unit.post",
  page: "sales.unit.page",
  word: "sales.unit.word",
};

export const vatRates = [20, 10, 1, 0] as const;

/** KDV tevkifatı ratios in use (tenths); 0 is none. */
export const withholdingOptions = [0, 2, 3, 4, 5, 7, 9, 10] as const;

export const currencies = ["TRY", "USD", "EUR", "GBP"] as const;

export const statusBadges: Record<string, { variant: "secondary" | "info" | "success" | "destructive" | "warning"; label: TranslationKey }> = {
  draft: { variant: "secondary", label: "sales.status.draft" },
  sent: { variant: "info", label: "sales.status.sent" },
  accepted: { variant: "success", label: "sales.status.accepted" },
  rejected: { variant: "destructive", label: "sales.status.rejected" },
  expired: { variant: "warning", label: "sales.status.expired" },
};

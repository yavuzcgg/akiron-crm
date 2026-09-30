import type { TranslationKey } from "@/lib/i18n";
import type { TranslationParams } from "@/lib/i18n/translate";

type Translate = (key: TranslationKey, params?: TranslationParams) => string;

const stageKeys: Record<string, TranslationKey> = {
  todo: "jobs.stage.todo",
  in_progress: "jobs.stage.in_progress",
  review: "jobs.stage.review",
  done: "jobs.stage.done",
};

/**
 * A stage's label: the tenant's own name, or the translated built-in key (the API leaves the name
 * empty for untouched built-in stages so each reader sees their language).
 */
export function stageLabel(stage: { key?: string | null; name?: string | null }, t: Translate): string {
  if (stage.name) return stage.name;
  const key = stage.key ? stageKeys[stage.key] : undefined;
  return key ? t(key) : "—";
}

export const priorityLabels: Record<string, TranslationKey> = {
  low: "jobs.priority.low",
  normal: "jobs.priority.normal",
  high: "jobs.priority.high",
  urgent: "jobs.priority.urgent",
};

export const categoryLabels: Record<string, TranslationKey> = {
  open: "jobs.category.open",
  active: "jobs.category.active",
  done: "jobs.category.done",
};

"use client";

import { useI18n, type TranslationKey } from "@/lib/i18n";
import type { TranslationParams } from "@/lib/i18n/translate";

const systemRoles: Record<string, TranslationKey> = {
  owner: "role.owner",
  admin: "role.admin",
  member: "role.member",
};

/** System roles arrive as stable keys and are translated; custom roles are shown as named. */
export function roleLabel(role: string, t: (key: TranslationKey, params?: TranslationParams) => string): string {
  const key = systemRoles[role];
  return key ? t(key) : role;
}

export function RoleName({ role }: { role: string }) {
  const { t } = useI18n();
  return <>{roleLabel(role, t)}</>;
}

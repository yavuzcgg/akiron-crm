"use client";

import { Badge } from "@/components/ui/badge";
import { useI18n } from "@/lib/i18n";

/** Customer and supplier are labels with a hue, never colour alone (design system). */
export function PartyRoleBadges({ isCustomer, isSupplier }: { isCustomer: boolean; isSupplier: boolean }) {
  const { t } = useI18n();
  return (
    <div className="flex flex-wrap gap-1.5">
      {isCustomer ? <Badge variant="brand">{t("crm.role.customer")}</Badge> : null}
      {isSupplier ? <Badge variant="warning">{t("crm.role.supplier")}</Badge> : null}
    </div>
  );
}

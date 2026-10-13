"use client";

import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useSession } from "@/features/identity/session";
import { PortalSettingsForm } from "@/features/portal/portal-access";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

export default function PortalSettingsPage() {
  const { t } = useI18n();
  const { data: session } = useSession();

  return (
    <div className="grid gap-8">
      <PageHeader title={t("portal.settings.title")} description={t("portal.settings.description")} />
      <Card>
        <CardHeader>
          <CardTitle>{t("portal.settings.cardTitle")}</CardTitle>
          <CardDescription>{t("portal.settings.cardHint")}</CardDescription>
        </CardHeader>
        <CardContent className="max-w-2xl">
          <PortalSettingsForm canManage={hasPermission(session?.permissions, permissions.portal.manage)} />
        </CardContent>
      </Card>
    </div>
  );
}

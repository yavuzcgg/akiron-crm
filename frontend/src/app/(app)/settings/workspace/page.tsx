"use client";

import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { WorkspaceNameForm } from "@/features/identity/account-settings";
import { useSession } from "@/features/identity/session";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

export default function WorkspacePage() {
  const { t } = useI18n();
  const { data: session } = useSession();
  const canManage = hasPermission(session?.permissions, permissions.identity.tenantManage);

  return (
    <div className="grid gap-8">
      <PageHeader title={t("workspace.title")} description={t("workspace.description")} />

      <Card>
        <CardHeader>
          <CardTitle>{t("workspace.name.title")}</CardTitle>
          <CardDescription>{t("workspace.name.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          {session ? <WorkspaceNameForm session={session} canManage={canManage} /> : <Skeleton className="h-24 max-w-md" />}
        </CardContent>
      </Card>
    </div>
  );
}

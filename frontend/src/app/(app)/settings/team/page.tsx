"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { InviteForm, PendingInvitations } from "@/features/identity/invitations";
import { MembersTable } from "@/features/identity/members-table";
import { useSession } from "@/features/identity/session";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

export default function TeamPage() {
  const { t } = useI18n();
  const { data: session } = useSession();
  const canManage = hasPermission(session?.permissions, permissions.identity.membersManage);

  return (
    <div className="mx-auto grid max-w-5xl gap-6">
      {canManage ? (
        <Card>
          <CardHeader>
            <CardTitle>{t("team.invite.title")}</CardTitle>
            <CardDescription>{t("team.invite.description")}</CardDescription>
          </CardHeader>
          <CardContent>
            <InviteForm />
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("team.title")}</CardTitle>
          <CardDescription>{t("team.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <MembersTable />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("team.invitations.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          <PendingInvitations canManage={canManage} />
        </CardContent>
      </Card>
    </div>
  );
}

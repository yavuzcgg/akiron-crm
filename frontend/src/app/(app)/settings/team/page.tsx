"use client";

import { UserPlus } from "lucide-react";
import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { InviteForm, PendingInvitations } from "@/features/identity/invitations";
import { MembersTable } from "@/features/identity/members-table";
import { useSession } from "@/features/identity/session";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

export default function TeamPage() {
  const { t } = useI18n();
  const { data: session } = useSession();
  const [inviteOpen, setInviteOpen] = useState(false);
  const canManage = hasPermission(session?.permissions, permissions.identity.membersManage);

  return (
    <div className="grid gap-8">
      <PageHeader
        title={t("team.title")}
        description={t("team.description")}
        actions={
          canManage ? (
            <Button onClick={() => setInviteOpen(true)}>
              <UserPlus /> {t("team.invite.submit")}
            </Button>
          ) : null
        }
      />

      <Card className="py-0">
        <CardContent className="px-0">
          <MembersTable />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("team.invitations.title")}</CardTitle>
          <CardDescription>{t("team.invite.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <PendingInvitations canManage={canManage} />
        </CardContent>
      </Card>

      <Dialog open={inviteOpen} onOpenChange={setInviteOpen}>
        <DialogContent className="sm:max-w-md" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("team.invite.title")}</DialogTitle>
            <DialogDescription>{t("team.invite.description")}</DialogDescription>
          </DialogHeader>
          <InviteForm onSent={() => setInviteOpen(false)} />
        </DialogContent>
      </Dialog>
    </div>
  );
}

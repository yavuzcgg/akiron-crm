"use client";

import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ChangePasswordForm, ProfileForm } from "@/features/identity/account-settings";
import { useSession } from "@/features/identity/session";
import { useI18n } from "@/lib/i18n";

export default function AccountPage() {
  const { t } = useI18n();
  const { data: session } = useSession();

  return (
    <div className="grid gap-8">
      <PageHeader title={t("account.title")} description={t("account.description")} />

      <Card>
        <CardHeader>
          <CardTitle>{t("account.profile.title")}</CardTitle>
          <CardDescription>{t("account.profile.description")}</CardDescription>
        </CardHeader>
        <CardContent>{session ? <ProfileForm session={session} /> : <Skeleton className="h-40 max-w-md" />}</CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("account.password.title")}</CardTitle>
          <CardDescription>{t("account.password.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <ChangePasswordForm />
        </CardContent>
      </Card>
    </div>
  );
}

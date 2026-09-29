"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MembersTable } from "@/features/identity/members-table";
import { useI18n } from "@/lib/i18n";

export default function TeamPage() {
  const { t } = useI18n();

  return (
    <div className="mx-auto grid max-w-5xl gap-6">
      <Card>
        <CardHeader>
          <CardTitle>{t("team.title")}</CardTitle>
          <CardDescription>{t("team.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          <MembersTable />
        </CardContent>
      </Card>
    </div>
  );
}

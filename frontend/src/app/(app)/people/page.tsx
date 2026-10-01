"use client";

import { PageHeader } from "@/components/page-header";
import { Card, CardContent } from "@/components/ui/card";
import { EmployeesTable } from "@/features/people/employees";
import { useI18n } from "@/lib/i18n";

export default function PeoplePage() {
  const { t } = useI18n();
  return (
    <div className="grid gap-8">
      <PageHeader title={t("people.title")} description={t("people.description")} />
      <Card className="py-0">
        <CardContent className="px-0">
          <EmployeesTable />
        </CardContent>
      </Card>
    </div>
  );
}

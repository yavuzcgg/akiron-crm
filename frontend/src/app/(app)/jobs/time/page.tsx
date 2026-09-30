"use client";

import { PageHeader } from "@/components/page-header";
import { Timesheet } from "@/features/jobs/timesheet";
import { useI18n } from "@/lib/i18n";

export default function TimesheetPage() {
  const { t } = useI18n();
  return (
    <div className="grid gap-8">
      <PageHeader title={t("jobs.timesheet.title")} description={t("jobs.timesheet.description")} />
      <Timesheet />
    </div>
  );
}

"use client";

import { CalendarDays, FileText, UserRoundCheck, Wallet, type LucideIcon } from "lucide-react";
import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { SetupChecklist } from "@/features/dashboard/setup-checklist";
import { FileList } from "@/features/files/file-list";
import { useSession } from "@/features/identity/session";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

/** What comes next (docs/PLAN.md); shipped modules leave this list. */
const roadmap: { icon: LucideIcon; label: TranslationKey; phase: number }[] = [
  { icon: UserRoundCheck, label: "dashboard.roadmap.people", phase: 2 },
  { icon: FileText, label: "nav.quotes", phase: 3 },
  { icon: Wallet, label: "nav.finance", phase: 3 },
  { icon: CalendarDays, label: "nav.content", phase: 4 },
];

function greetingKey(hour: number): TranslationKey {
  if (hour < 12) return "dashboard.greeting.morning";
  if (hour < 18) return "dashboard.greeting.afternoon";
  return "dashboard.greeting.evening";
}

export default function DashboardPage() {
  const { t } = useI18n();
  const { data: session } = useSession();
  // Read once per visit; the greeting must not change between renders.
  const [hour] = useState(() => new Date().getHours());
  if (!session) return null;

  const firstName = session.fullName.split(" ")[0] ?? session.fullName;
  const can = (permission: string) => hasPermission(session.permissions, permission);

  return (
    <div className="grid gap-8">
      <PageHeader
        title={t(greetingKey(hour), { name: firstName })}
        description={t("dashboard.intro", { tenant: session.tenantName })}
      />

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="grid gap-6">
          <SetupChecklist session={session} />

          {can(permissions.timeline.read) ? (
            <Card id="activity" className="scroll-mt-20">
              <CardHeader>
                <CardTitle>{t("timeline.title")}</CardTitle>
                <CardDescription>{t("timeline.description")}</CardDescription>
              </CardHeader>
              <CardContent>
                <TimelineFeed subjectType="workspace" subjectId={session.tenantId} canWriteNotes={can(permissions.timeline.notesWrite)} />
              </CardContent>
            </Card>
          ) : null}
        </div>

        <div className="grid gap-6">
          {can(permissions.files.read) ? (
            <Card id="files" className="scroll-mt-20">
              <CardHeader>
                <CardTitle>{t("files.title")}</CardTitle>
                <CardDescription>{t("files.description")}</CardDescription>
              </CardHeader>
              <CardContent>
                <FileList subjectType="workspace" subjectId={session.tenantId} canWrite={can(permissions.files.write)} />
              </CardContent>
            </Card>
          ) : null}

          <Card>
            <CardHeader>
              <CardTitle>{t("dashboard.roadmap.title")}</CardTitle>
              <CardDescription>{t("dashboard.roadmap.description")}</CardDescription>
            </CardHeader>
            <CardContent>
              <ul className="grid gap-1">
                {roadmap.map((item) => (
                  <li key={item.label} className="flex items-center gap-3 rounded-lg py-1.5">
                    <span className="bg-muted text-muted-foreground flex size-8 items-center justify-center rounded-lg">
                      <item.icon className="size-4" aria-hidden />
                    </span>
                    <span className="flex-1 font-medium">{t(item.label)}</span>
                    <Badge variant="outline" className="tabular">
                      {t("dashboard.roadmap.phase", { phase: item.phase })}
                    </Badge>
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

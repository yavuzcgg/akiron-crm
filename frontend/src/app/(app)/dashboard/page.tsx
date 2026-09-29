"use client";

import { BriefcaseBusiness, FileText, UsersRound, type LucideIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { FileList } from "@/features/files/file-list";
import { useSession } from "@/features/identity/session";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

const upcoming: { icon: LucideIcon; label: TranslationKey; phase: string }[] = [
  { icon: UsersRound, label: "dashboard.next.customers", phase: "Faz 2" },
  { icon: BriefcaseBusiness, label: "dashboard.next.jobs", phase: "Faz 2" },
  { icon: FileText, label: "dashboard.next.quotes", phase: "Faz 3" },
];

export default function DashboardPage() {
  const { t } = useI18n();
  const { data: session } = useSession();
  if (!session) return null;

  const firstName = session.fullName.split(" ")[0] ?? session.fullName;
  const canReadTimeline = hasPermission(session.permissions, permissions.timeline.read);

  return (
    <div className="mx-auto grid max-w-5xl gap-6">
      <div className="grid gap-1">
        <h1 className="text-2xl font-semibold tracking-tight">{t("dashboard.welcome", { name: firstName })}</h1>
        <p className="text-muted-foreground">{t("dashboard.intro", { tenant: session.tenantName })}</p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_18rem]">
        {canReadTimeline ? (
          <Card>
            <CardHeader>
              <CardTitle>{t("timeline.title")}</CardTitle>
              <CardDescription>{t("timeline.description")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed
                subjectType="workspace"
                subjectId={session.tenantId}
                canWriteNotes={hasPermission(session.permissions, permissions.timeline.notesWrite)}
              />
            </CardContent>
          </Card>
        ) : (
          <div />
        )}

        <section className="grid content-start gap-3">
          {hasPermission(session.permissions, permissions.files.read) ? (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">{t("files.title")}</CardTitle>
                <CardDescription>{t("files.description")}</CardDescription>
              </CardHeader>
              <CardContent>
                <FileList
                  subjectType="workspace"
                  subjectId={session.tenantId}
                  canWrite={hasPermission(session.permissions, permissions.files.write)}
                />
              </CardContent>
            </Card>
          ) : null}
          <h2 className="text-muted-foreground text-sm font-medium">{t("dashboard.next.title")}</h2>
          {upcoming.map((item) => (
            <Card key={item.label} className="border-dashed">
              <CardHeader>
                <div className="flex items-center justify-between">
                  <item.icon className="text-muted-foreground size-5" />
                  <Badge variant="outline">{item.phase}</Badge>
                </div>
                <CardTitle className="text-base">{t(item.label)}</CardTitle>
                <CardDescription>{t("common.soon")}</CardDescription>
              </CardHeader>
            </Card>
          ))}
        </section>
      </div>
    </div>
  );
}

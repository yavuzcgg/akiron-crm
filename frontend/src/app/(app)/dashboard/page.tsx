"use client";

import { BriefcaseBusiness, FileText, UsersRound, type LucideIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useSession } from "@/features/identity/session";
import { useI18n, type TranslationKey } from "@/lib/i18n";

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

  return (
    <div className="mx-auto grid max-w-5xl gap-6">
      <div className="grid gap-1">
        <h1 className="text-2xl font-semibold tracking-tight">{t("dashboard.welcome", { name: firstName })}</h1>
        <p className="text-muted-foreground">{t("dashboard.intro", { tenant: session.tenantName })}</p>
      </div>

      <section className="grid gap-3">
        <h2 className="text-muted-foreground text-sm font-medium">{t("dashboard.next.title")}</h2>
        <div className="grid gap-3 sm:grid-cols-3">
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
        </div>
      </section>
    </div>
  );
}

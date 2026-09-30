"use client";

import { Plus, Search, Settings2, UserRound } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useSession } from "@/features/identity/session";
import { WorkOrderBoard } from "@/features/jobs/board";
import { StageManager } from "@/features/jobs/stage-manager";
import { WorkOrderForm } from "@/features/jobs/work-order-form";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

export default function JobsBoardPage() {
  const { t } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const canWrite = hasPermission(session?.permissions, permissions.jobs.workOrdersWrite);
  const canManageStages = hasPermission(session?.permissions, permissions.jobs.stagesManage);

  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [mine, setMine] = useState(false);
  // undefined: closed · string: the stage a new card goes into ("" = first open stage).
  const [creatingIn, setCreatingIn] = useState<string | undefined>(undefined);
  const [stagesOpen, setStagesOpen] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  return (
    <div className="grid gap-6">
      <PageHeader
        title={t("jobs.title")}
        description={t("jobs.description")}
        actions={
          canWrite ? (
            <Button onClick={() => setCreatingIn("")}>
              <Plus /> {t("jobs.workOrder.new")}
            </Button>
          ) : null
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full sm:w-72">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
          <Input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder={t("jobs.board.searchPlaceholder")}
            aria-label={t("jobs.board.search")}
            className="h-9 pl-9"
          />
        </div>
        <Button variant={mine ? "secondary" : "outline"} size="sm" className="h-9" aria-pressed={mine} onClick={() => setMine(!mine)}>
          <UserRound /> {t("jobs.board.mine")}
        </Button>
        {canManageStages ? (
          <Button variant="ghost" size="sm" className="h-9 sm:ml-auto" onClick={() => setStagesOpen(true)}>
            <Settings2 /> {t("jobs.stages.manage")}
          </Button>
        ) : null}
      </div>

      <WorkOrderBoard filter={{ search, mine }} canWrite={canWrite} onAdd={(stageId) => setCreatingIn(stageId)} />

      <Dialog open={creatingIn !== undefined} onOpenChange={(open) => (open ? null : setCreatingIn(undefined))}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("jobs.workOrder.newTitle")}</DialogTitle>
            <DialogDescription>{t("jobs.workOrder.newDescription")}</DialogDescription>
          </DialogHeader>
          {creatingIn !== undefined ? (
            <WorkOrderForm
              stageId={creatingIn || undefined}
              onSaved={(workOrder) => {
                setCreatingIn(undefined);
                router.push(`/jobs/${workOrder.id}`);
              }}
              onCancel={() => setCreatingIn(undefined)}
            />
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog open={stagesOpen} onOpenChange={setStagesOpen}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("jobs.stages.title")}</DialogTitle>
            <DialogDescription>{t("jobs.stages.description")}</DialogDescription>
          </DialogHeader>
          {stagesOpen ? <StageManager /> : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

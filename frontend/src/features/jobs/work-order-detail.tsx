"use client";

import { AlertCircle, Archive, ArrowLeft, Building2, Pencil } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { FileList } from "@/features/files/file-list";
import { useSession } from "@/features/identity/session";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { ApiError } from "@/lib/api/errors";
import { formatDate, initials } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { Checklist } from "./checklist";
import { Financials } from "./financials";
import { useArchiveWorkOrder, useMoveWorkOrder, useStages, useWorkOrder } from "./jobs-api";
import { stageLabel } from "./labels";
import { TimePanel } from "./time-panel";
import { DueDate, PriorityBadge } from "./work-order-card";
import { WorkOrderForm } from "./work-order-form";

function Fact({ label, children }: { label: TranslationKey; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="grid gap-1">
      <dt className="text-muted-foreground text-xs font-medium">{t(label)}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

export function WorkOrderDetail({ id }: { id: string }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const workOrder = useWorkOrder(id);
  const stages = useStages();
  const move = useMoveWorkOrder();
  const archive = useArchiveWorkOrder(id);
  const [editOpen, setEditOpen] = useState(false);

  const granted = session?.permissions;
  const canWrite = hasPermission(granted, permissions.jobs.workOrdersWrite);
  const canTrack = hasPermission(granted, permissions.jobs.timeWrite);
  const canReadClients = hasPermission(granted, permissions.crm.partiesRead);
  const fail = (error: unknown) => toast.error(tError(error instanceof ApiError ? error.code : "common.network"));

  const back = (
    <Link href="/jobs" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("jobs.backToBoard")}
    </Link>
  );

  if (workOrder.isPending) {
    return (
      <div className="grid gap-6">
        {back}
        <Skeleton className="h-14 w-96" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (workOrder.isError) {
    const error = workOrder.error;
    return (
      <div className="grid gap-6">
        {back}
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {error instanceof ApiError && error.status === 403 ? t("jobs.noPermission") : tError(error instanceof ApiError ? error.code : "common.network")}
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const data = workOrder.data;

  return (
    <div className="grid gap-6">
      {back}

      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="grid min-w-0 gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-muted-foreground font-mono text-xs">{data.number}</span>
            <PriorityBadge priority={data.priority} />
          </div>
          <h1 className="text-[22px] leading-7 font-semibold tracking-tight break-words">{data.title}</h1>
          {data.partyName ? (
            <p className="text-muted-foreground flex items-center gap-1.5 text-sm">
              <Building2 className="size-4" aria-hidden />
              {canReadClients && data.partyId ? (
                <Link href={`/crm/parties/${data.partyId}`} className="text-primary underline-offset-4 hover:underline">
                  {data.partyName}
                </Link>
              ) : (
                data.partyName
              )}
            </p>
          ) : null}
        </div>
        <div className="flex shrink-0 flex-wrap items-end gap-2">
          <div className="grid gap-1">
            <Label htmlFor="wo-stage" className="text-muted-foreground text-xs">
              {t("jobs.field.stage")}
            </Label>
            <select
              id="wo-stage"
              className="border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 h-9 rounded-lg border px-3 text-sm font-medium outline-none focus-visible:ring-3 disabled:opacity-60"
              value={data.stage.id}
              disabled={!canWrite || move.isPending}
              onChange={(event) =>
                move.mutate(
                  { id: data.id, stageId: event.target.value },
                  { onSuccess: () => void workOrder.refetch(), onError: fail },
                )
              }
            >
              {(stages.data ?? [data.stage]).map((stage) => (
                <option key={stage.id} value={stage.id}>
                  {stageLabel(stage, t)}
                </option>
              ))}
            </select>
          </div>
          {canWrite ? (
            <>
              <Button
                variant="outline"
                onClick={() => {
                  if (!window.confirm(t("jobs.workOrder.archiveConfirm", { number: data.number }))) return;
                  archive.mutate(undefined, {
                    onSuccess: () => {
                      toast.success(t("jobs.workOrder.archived", { number: data.number }));
                      router.replace("/jobs");
                    },
                    onError: fail,
                  });
                }}
                disabled={archive.isPending}
              >
                <Archive /> {t("jobs.workOrder.archive")}
              </Button>
              <Button onClick={() => setEditOpen(true)}>
                <Pencil /> {t("jobs.workOrder.edit")}
              </Button>
            </>
          ) : null}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px]">
        <div className="grid content-start gap-6">
          {data.description ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("jobs.field.description")}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm leading-6 whitespace-pre-wrap">{data.description}</p>
              </CardContent>
            </Card>
          ) : null}

          <Card>
            <CardHeader>
              <CardTitle>{t("jobs.tasks.title")}</CardTitle>
              <CardDescription>{t("jobs.tasks.description")}</CardDescription>
            </CardHeader>
            <CardContent>
              <Checklist workOrderId={data.id} tasks={data.tasks} canWrite={canWrite} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t("jobs.detail.discussion")}</CardTitle>
              <CardDescription>{t("jobs.detail.discussionHint")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed subjectType="work_order" subjectId={data.id} canWriteNotes={hasPermission(granted, permissions.timeline.notesWrite)} />
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card>
            <CardContent>
              <dl className="grid gap-4">
                <Fact label="jobs.field.assignees">
                  {data.assignees.length === 0 ? (
                    <span className="text-muted-foreground">{t("jobs.detail.nobody")}</span>
                  ) : (
                    <ul className="grid gap-1.5">
                      {data.assignees.map((assignee) => (
                        <li key={assignee.userId} className="flex items-center gap-2">
                          <span className="bg-primary-soft text-primary-strong flex size-6 items-center justify-center rounded-full text-[10px] font-semibold">
                            {assignee.fullName ? initials(assignee.fullName) : "?"}
                          </span>
                          {assignee.fullName || t("jobs.card.formerMember")}
                        </li>
                      ))}
                    </ul>
                  )}
                </Fact>
                <Fact label="jobs.field.dueDate">
                  {data.dueDate ? <DueDate dueDate={data.dueDate} done={data.completedAt !== null} /> : <span className="text-muted-foreground">—</span>}
                </Fact>
                <Fact label="jobs.detail.opened">{formatDate(data.createdAt)}</Fact>
                {data.completedAt ? <Fact label="jobs.detail.completed">{formatDate(data.completedAt)}</Fact> : null}
              </dl>
            </CardContent>
          </Card>

          {data.financials ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("jobs.financials.title")}</CardTitle>
                <CardDescription>{t("jobs.financials.description")}</CardDescription>
              </CardHeader>
              <CardContent>
                <Financials key={data.id} workOrder={data} />
              </CardContent>
            </Card>
          ) : null}

          <Card>
            <CardHeader>
              <CardTitle>{t("jobs.time.title")}</CardTitle>
            </CardHeader>
            <CardContent>
              <TimePanel workOrder={data} canTrack={canTrack} />
            </CardContent>
          </Card>

          {hasPermission(granted, permissions.files.read) ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("files.title")}</CardTitle>
                <CardDescription>{t("jobs.detail.files")}</CardDescription>
              </CardHeader>
              <CardContent>
                <FileList subjectType="work_order" subjectId={data.id} canWrite={hasPermission(granted, permissions.files.write)} />
              </CardContent>
            </Card>
          ) : null}
        </div>
      </div>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("jobs.workOrder.editTitle")}</DialogTitle>
            <DialogDescription>{data.number}</DialogDescription>
          </DialogHeader>
          {editOpen ? <WorkOrderForm workOrder={data} onSaved={() => setEditOpen(false)} onCancel={() => setEditOpen(false)} /> : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

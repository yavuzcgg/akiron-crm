"use client";

import { AlertCircle, Archive, ArrowLeft, Building2, Pencil, UserRound } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { FileList } from "@/features/files/file-list";
import { useSession } from "@/features/identity/session";
import { PartyWorkOrders } from "@/features/jobs/party-work-orders";
import { PartyQuotes } from "@/features/sales/party-quotes";
import { TimelineFeed } from "@/features/timeline/timeline-feed";
import { ApiError } from "@/lib/api/errors";
import { formatDate } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { useArchiveParty, useParty, type Party } from "./parties-api";
import { PartyRoleBadges } from "./party-badges";
import { CustomFieldValues } from "./custom-fields";
import { PartyContacts } from "./party-contacts";
import { PartyForm } from "./party-form";

function Detail({ label, children }: { label: TranslationKey; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="grid gap-0.5">
      <dt className="text-muted-foreground text-xs font-medium tracking-[0.02em]">{t(label)}</dt>
      <dd className="text-sm break-words">{children || "—"}</dd>
    </div>
  );
}

function PartyInfo({ party }: { party: Party }) {
  const address = [party.addressLine, [party.district, party.city].filter(Boolean).join(" / ")].filter(Boolean).join(", ");
  return (
    <dl className="grid gap-4 sm:grid-cols-2">
      <Detail label={party.kind === "company" ? "crm.field.vkn" : "crm.field.tckn"}>
        {party.taxNumber ? <span className="tabular">{party.taxNumber}</span> : null}
      </Detail>
      <Detail label="crm.field.taxOffice">{party.taxOffice}</Detail>
      <Detail label="crm.field.email">
        {party.email ? (
          <a href={`mailto:${party.email}`} className="text-primary underline-offset-4 hover:underline">
            {party.email}
          </a>
        ) : null}
      </Detail>
      <Detail label="crm.field.phone">
        {party.phone ? (
          <a href={`tel:${party.phone}`} className="text-primary tabular underline-offset-4 hover:underline">
            {party.phone}
          </a>
        ) : null}
      </Detail>
      <Detail label="crm.field.website">
        {party.website ? (
          <a
            href={/^https?:\/\//i.test(party.website) ? party.website : `https://${party.website}`}
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary underline-offset-4 hover:underline"
          >
            {party.website}
          </a>
        ) : null}
      </Detail>
      <Detail label="crm.field.address">{address}</Detail>
    </dl>
  );
}

export function PartyDetail({ id }: { id: string }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const party = useParty(id);
  const archive = useArchiveParty(id);
  const [editOpen, setEditOpen] = useState(false);

  const canWrite = hasPermission(session?.permissions, permissions.crm.partiesWrite);
  const canNote = hasPermission(session?.permissions, permissions.timeline.notesWrite);
  const canReadFiles = hasPermission(session?.permissions, permissions.files.read);
  const canWriteFiles = hasPermission(session?.permissions, permissions.files.write);

  const back = (
    <Link href="/crm/parties" className={buttonVariants({ variant: "ghost", size: "sm", className: "-ml-2 justify-self-start" })}>
      <ArrowLeft /> {t("crm.backToList")}
    </Link>
  );

  if (party.isPending) {
    return (
      <div className="grid gap-6">
        {back}
        <Skeleton className="h-14 w-80" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (party.isError) {
    const error = party.error;
    return (
      <div className="grid gap-6">
        {back}
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {error instanceof ApiError && error.status === 403
              ? t("crm.noPermission")
              : tError(error instanceof ApiError ? error.code : "common.network")}
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const data = party.data;
  const KindIcon = data.kind === "person" ? UserRound : Building2;

  const onArchive = async () => {
    if (!window.confirm(t("crm.party.archiveConfirm", { name: data.name }))) return;
    try {
      await archive.mutateAsync();
      toast.success(t("crm.party.archived", { name: data.name }));
      router.replace("/crm/parties");
    } catch (error) {
      toast.error(tError(error instanceof ApiError ? error.code : "common.network"));
    }
  };

  return (
    <div className="grid gap-6">
      {back}

      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex min-w-0 items-start gap-4">
          <span className="bg-primary-soft text-primary-strong flex size-12 shrink-0 items-center justify-center rounded-xl">
            <KindIcon className="size-5" aria-hidden />
          </span>
          <div className="grid min-w-0 gap-1.5">
            <h1 className="text-[22px] leading-7 font-semibold tracking-tight break-words">{data.name}</h1>
            <div className="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1.5 text-sm">
              <span className="font-mono text-xs">{data.code}</span>
              <span aria-hidden>·</span>
              <span>{t(data.kind === "person" ? "crm.kind.person" : "crm.kind.company")}</span>
              <PartyRoleBadges isCustomer={data.isCustomer} isSupplier={data.isSupplier} />
            </div>
          </div>
        </div>
        {canWrite ? (
          <div className="flex shrink-0 gap-2">
            <Button variant="outline" onClick={() => void onArchive()} disabled={archive.isPending}>
              <Archive /> {t("crm.party.archive")}
            </Button>
            <Button onClick={() => setEditOpen(true)}>
              <Pencil /> {t("crm.party.edit")}
            </Button>
          </div>
        ) : null}
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle>{t("crm.detail.info")}</CardTitle>
              <CardDescription>{t("crm.detail.since", { date: formatDate(data.createdAt) })}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-4">
                <PartyInfo party={data} />
                <CustomFieldValues values={data.customFields} />
              </div>
            </CardContent>
          </Card>

          {hasPermission(session?.permissions, permissions.sales.quotesRead) ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("sales.quotes.title")}</CardTitle>
                <CardDescription>{t("sales.party.description")}</CardDescription>
              </CardHeader>
              <CardContent>
                <PartyQuotes partyId={data.id} partyName={data.name} canWrite={hasPermission(session?.permissions, permissions.sales.quotesWrite)} />
              </CardContent>
            </Card>
          ) : null}

          {hasPermission(session?.permissions, permissions.jobs.workOrdersRead) ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("jobs.party.title")}</CardTitle>
                <CardDescription>{t("jobs.party.description")}</CardDescription>
              </CardHeader>
              <CardContent>
                <PartyWorkOrders
                  partyId={data.id}
                  partyName={data.name}
                  canWrite={hasPermission(session?.permissions, permissions.jobs.workOrdersWrite)}
                />
              </CardContent>
            </Card>
          ) : null}

          <Card>
            <CardHeader>
              <CardTitle>{t("timeline.title")}</CardTitle>
              <CardDescription>{t("crm.detail.timeline")}</CardDescription>
            </CardHeader>
            <CardContent>
              <TimelineFeed subjectType="party" subjectId={data.id} canWriteNotes={canNote} />
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle>{t("crm.detail.contacts")}</CardTitle>
              <CardDescription>{t("crm.contact.description")}</CardDescription>
            </CardHeader>
            <CardContent>
              <PartyContacts partyId={data.id} contacts={data.contacts} canWrite={canWrite} />
            </CardContent>
          </Card>

          {canReadFiles ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("files.title")}</CardTitle>
                <CardDescription>{t("crm.detail.files")}</CardDescription>
              </CardHeader>
              <CardContent>
                <FileList subjectType="party" subjectId={data.id} canWrite={canWriteFiles} />
              </CardContent>
            </Card>
          ) : null}
        </div>
      </div>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("crm.party.editTitle")}</DialogTitle>
            <DialogDescription>{data.name}</DialogDescription>
          </DialogHeader>
          {editOpen ? <PartyForm party={data} onSaved={() => setEditOpen(false)} onCancel={() => setEditOpen(false)} /> : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

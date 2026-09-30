"use client";

import { BriefcaseBusiness, Plus } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { useI18n } from "@/lib/i18n";
import { usePartyWorkOrders } from "./jobs-api";
import { WorkOrderCardBody } from "./work-order-card";
import { WorkOrderForm } from "./work-order-form";

/** A client's work orders on its CRM page, with a shortcut to open a new one for it. */
export function PartyWorkOrders({ partyId, partyName, canWrite }: { partyId: string; partyName: string; canWrite: boolean }) {
  const { t } = useI18n();
  const router = useRouter();
  const workOrders = usePartyWorkOrders(partyId);
  const [open, setOpen] = useState(false);

  const newButton = canWrite ? (
    <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
      <Plus /> {t("jobs.workOrder.new")}
    </Button>
  ) : null;

  return (
    <>
      {workOrders.isPending ? (
        <Skeleton className="h-24 w-full" />
      ) : (workOrders.data?.items.length ?? 0) === 0 ? (
        <EmptyState icon={BriefcaseBusiness} title={t("jobs.party.empty")} className="py-6" action={newButton} />
      ) : (
        <div className="grid gap-3">
          <ul className="grid gap-2">
            {workOrders.data!.items.map((card) => (
              <li key={card.id} className="hover:border-primary/40 relative rounded-lg border p-3 transition-colors">
                <Link href={`/jobs/${card.id}`} className="absolute inset-0 rounded-lg" aria-label={t("jobs.card.open", { number: card.number, title: card.title })} />
                <WorkOrderCardBody card={card} />
              </li>
            ))}
          </ul>
          {newButton ? <div>{newButton}</div> : null}
        </div>
      )}

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("jobs.workOrder.newTitle")}</DialogTitle>
            <DialogDescription>{partyName}</DialogDescription>
          </DialogHeader>
          {open ? (
            <WorkOrderForm
              client={{ partyId, name: partyName }}
              onSaved={(workOrder) => {
                setOpen(false);
                router.push(`/jobs/${workOrder.id}`);
              }}
              onCancel={() => setOpen(false)}
            />
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}

"use client";

import { FileSignature, Plus } from "lucide-react";
import { useState } from "react";
import { EmptyState } from "@/components/empty-state";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useI18n } from "@/lib/i18n";
import { ContractDialog } from "./contract-dialog";
import { ContractRows } from "./contracts-page";
import { useContracts } from "./contracts-api";

/** A client's contracts on its CRM page, with a shortcut to start one. */
export function PartyContracts({ partyId, partyName, canWrite }: { partyId: string; partyName: string; canWrite: boolean }) {
  const { t } = useI18n();
  const contracts = useContracts({ partyId }, 1, 20);
  const [creating, setCreating] = useState(false);
  const add = canWrite ? (
    <Button variant="outline" size="sm" onClick={() => setCreating(true)}>
      <Plus /> {t("contracts.new")}
    </Button>
  ) : null;
  const dialog = creating ? <ContractDialog open onOpenChange={setCreating} party={{ id: partyId, name: partyName }} /> : null;

  if (contracts.isPending) return <Skeleton className="h-20 w-full" />;
  if ((contracts.data?.items.length ?? 0) === 0) {
    return (
      <>
        <EmptyState icon={FileSignature} title={t("contracts.party.empty")} className="py-6" action={add} />
        {dialog}
      </>
    );
  }

  return (
    <div className="grid gap-3">
      <div className="-mx-6 overflow-x-auto">
        <ContractRows items={contracts.data!.items} showParty={false} />
      </div>
      {add ? <div>{add}</div> : null}
      {dialog}
    </div>
  );
}

"use client";

import { Plus, SlidersHorizontal } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { CustomFieldManager } from "@/features/crm/custom-fields";
import { PartiesTable } from "@/features/crm/parties-table";
import { PartyForm } from "@/features/crm/party-form";
import { useSession } from "@/features/identity/session";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

export default function PartiesPage() {
  const { t } = useI18n();
  const router = useRouter();
  const { data: session } = useSession();
  const [createOpen, setCreateOpen] = useState(false);
  const [fieldsOpen, setFieldsOpen] = useState(false);
  const canShapeCards = hasPermission(session?.permissions, permissions.crm.customFieldsManage);
  const canWrite = hasPermission(session?.permissions, permissions.crm.partiesWrite);

  const createButton = canWrite ? (
    <Button onClick={() => setCreateOpen(true)}>
      <Plus /> {t("crm.party.new")}
    </Button>
  ) : null;

  return (
    <div className="grid gap-8">
      <PageHeader
        title={t("crm.title")}
        description={t("crm.description")}
        actions={
          <>
            {canShapeCards ? (
              <Button variant="ghost" onClick={() => setFieldsOpen(true)}>
                <SlidersHorizontal /> {t("crm.customFields.manage")}
              </Button>
            ) : null}
            {createButton}
          </>
        }
      />

      <Card className="py-0">
        <CardContent className="px-0">
          <PartiesTable createAction={createButton} />
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("crm.party.newTitle")}</DialogTitle>
            <DialogDescription>{t("crm.party.newDescription")}</DialogDescription>
          </DialogHeader>
          {createOpen ? (
            <PartyForm
              onSaved={(party) => {
                setCreateOpen(false);
                router.push(`/crm/parties/${party.id}`);
              }}
              onCancel={() => setCreateOpen(false)}
            />
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog open={fieldsOpen} onOpenChange={setFieldsOpen}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("crm.customFields.title")}</DialogTitle>
            <DialogDescription>{t("crm.customFields.description")}</DialogDescription>
          </DialogHeader>
          {fieldsOpen ? <CustomFieldManager /> : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

"use client";

import { AlertCircle, Package, Pencil, Plus, Search, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { useSession } from "@/features/identity/session";
import { ApiError } from "@/lib/api/errors";
import { formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { currencies, unitLabels, units, vatRates, withholdingOptions } from "./labels";
import { parseDecimal } from "./quote-math";
import { useCatalog, useCatalogMutations, type CatalogItem } from "./sales-api";

export const selectClass =
  "border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 h-10 w-full rounded-lg border px-3 text-sm outline-none focus-visible:ring-3";

function CatalogForm({ item, onDone }: { item?: CatalogItem; onDone: () => void }) {
  const { t, tError } = useI18n();
  const { save } = useCatalogMutations();
  const [name, setName] = useState(item?.name ?? "");
  const [description, setDescription] = useState(item?.description ?? "");
  const [unit, setUnit] = useState<string>(item?.unit ?? "piece");
  const [price, setPrice] = useState(item ? String(item.unitPrice).replace(".", ",") : "");
  const [currency, setCurrency] = useState(item?.currency ?? "TRY");
  const [includesVat, setIncludesVat] = useState(item?.priceIncludesVat ?? false);
  const [vatRate, setVatRate] = useState(item?.vatRate ?? 20);
  const [withholding, setWithholding] = useState(item?.withholdingTenths ?? 0);
  const [withholdingCode, setWithholdingCode] = useState(item?.withholdingCode ?? "");
  const [error, setError] = useState<string | null>(null);

  return (
    <form
      className="grid gap-4"
      noValidate
      onSubmit={async (event) => {
        event.preventDefault();
        const unitPrice = parseDecimal(price);
        if (!name.trim() || Number.isNaN(unitPrice) || unitPrice < 0) {
          setError(t(name.trim() ? "sales.catalog.priceInvalid" : "validation.required"));
          return;
        }
        setError(null);
        try {
          await save.mutateAsync({
            id: item?.id,
            name: name.trim(),
            description: description.trim() || null,
            unit,
            unitPrice,
            currency,
            priceIncludesVat: includesVat,
            vatRate,
            withholdingTenths: withholding,
            withholdingCode: withholdingCode.trim() || null,
          });
          toast.success(t("sales.catalog.saved", { name: name.trim() }));
          onDone();
        } catch (failure) {
          setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
        }
      }}
    >
      {error ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
      <FormField id="cat-name" label={t("sales.catalog.name")} value={name} onChange={(event) => setName(event.target.value)} maxLength={200} />
      <div className="grid gap-1.5">
        <Label htmlFor="cat-description" className="text-[13px] font-medium">
          {t("sales.catalog.description")}
        </Label>
        <Textarea id="cat-description" rows={2} maxLength={2000} value={description} onChange={(event) => setDescription(event.target.value)} />
      </div>
      <div className="grid items-start gap-4 sm:grid-cols-3">
        <FormField id="cat-price" inputMode="decimal" label={t("sales.catalog.price")} value={price} onChange={(event) => setPrice(event.target.value)} />
        <div className="grid gap-1.5">
          <Label htmlFor="cat-currency" className="text-[13px] font-medium">
            {t("sales.field.currency")}
          </Label>
          <select id="cat-currency" className={selectClass} value={currency} onChange={(event) => setCurrency(event.target.value)}>
            {currencies.map((code) => (
              <option key={code}>{code}</option>
            ))}
          </select>
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="cat-unit" className="text-[13px] font-medium">
            {t("sales.field.unit")}
          </Label>
          <select id="cat-unit" className={selectClass} value={unit} onChange={(event) => setUnit(event.target.value)}>
            {units.map((option) => (
              <option key={option} value={option}>
                {t(unitLabels[option]!)}
              </option>
            ))}
          </select>
        </div>
      </div>
      <label className="flex cursor-pointer items-center gap-2 text-sm">
        <input type="checkbox" className="accent-primary size-4" checked={includesVat} onChange={(event) => setIncludesVat(event.target.checked)} />
        {t("sales.catalog.includesVat")}
      </label>
      <div className="grid items-start gap-4 sm:grid-cols-3">
        <div className="grid gap-1.5">
          <Label htmlFor="cat-vat" className="text-[13px] font-medium">
            {t("sales.field.vatRate")}
          </Label>
          <select id="cat-vat" className={selectClass} value={vatRate} onChange={(event) => setVatRate(Number(event.target.value))}>
            {vatRates.map((rate) => (
              <option key={rate} value={rate}>
                %{rate}
              </option>
            ))}
          </select>
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="cat-withholding" className="text-[13px] font-medium">
            {t("sales.field.withholding")}
          </Label>
          <select id="cat-withholding" className={selectClass} value={withholding} onChange={(event) => setWithholding(Number(event.target.value))}>
            {withholdingOptions.map((tenths) => (
              <option key={tenths} value={tenths}>
                {tenths === 0 ? t("sales.withholding.none") : `${tenths}/10`}
              </option>
            ))}
          </select>
        </div>
        <FormField
          id="cat-withholding-code"
          label={t("sales.catalog.withholdingCode")}
          hint={t("sales.catalog.withholdingCodeHint")}
          value={withholdingCode}
          onChange={(event) => setWithholdingCode(event.target.value)}
          maxLength={10}
          disabled={withholding === 0}
        />
      </div>
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onDone}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? t("common.saving") : t("common.save")}
        </Button>
      </div>
    </form>
  );
}

export function CatalogPage() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const canManage = hasPermission(session?.permissions, permissions.sales.catalogManage);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<CatalogItem | "new" | null>(null);
  const catalog = useCatalog(search);
  const { remove } = useCatalogMutations();

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const newButton = canManage ? (
    <Button onClick={() => setEditing("new")}>
      <Plus /> {t("sales.catalog.new")}
    </Button>
  ) : null;

  return (
    <div className="grid gap-8">
      <PageHeader title={t("sales.catalog.title")} description={t("sales.catalog.pageDescription")} actions={newButton} />

      <Card className="py-0">
        <CardContent className="px-0">
          <div className="border-b p-4">
            <div className="relative w-full sm:max-w-sm">
              <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" aria-hidden />
              <Input type="search" className="h-9 pl-9" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={t("sales.catalog.search")} aria-label={t("sales.catalog.search")} />
            </div>
          </div>
          {catalog.isPending ? (
            <div className="grid gap-2 p-5">
              <Skeleton className="h-11 w-full" />
              <Skeleton className="h-11 w-full" />
            </div>
          ) : catalog.isError ? (
            <div className="p-5">
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>{tError(catalog.error instanceof ApiError ? catalog.error.code : "common.network")}</AlertDescription>
              </Alert>
            </div>
          ) : catalog.data.length === 0 ? (
            <EmptyState icon={Package} title={t("sales.catalog.empty")} description={t("sales.catalog.emptyHint")} action={newButton} />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/60 hover:bg-muted/60">
                    <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("sales.catalog.name")}</TableHead>
                    <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium sm:table-cell">{t("sales.field.unit")}</TableHead>
                    <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium md:table-cell">{t("sales.field.vatRate")}</TableHead>
                    <TableHead className="text-muted-foreground h-10 text-right text-xs font-medium">{t("sales.catalog.price")}</TableHead>
                    {canManage ? <TableHead className="h-10 w-24 pr-5" /> : null}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {catalog.data.map((item) => (
                    <TableRow key={item.id} className="hover:bg-muted/40">
                      <TableCell className="py-3 pl-5">
                        <div className="grid min-w-0">
                          <span className="font-medium">{item.name}</span>
                          {item.description ? <span className="text-muted-foreground truncate text-xs">{item.description}</span> : null}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground hidden sm:table-cell">{t(unitLabels[item.unit] ?? "sales.unit.piece")}</TableCell>
                      <TableCell className="text-muted-foreground hidden md:table-cell">
                        %{item.vatRate}
                        {item.withholdingTenths > 0 ? ` · ${t("sales.withholding.short", { tenths: item.withholdingTenths })}` : ""}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatMoney(item.unitPrice, item.currency)}
                        <span className="text-muted-foreground block text-xs">{t(item.priceIncludesVat ? "sales.catalog.vatIncluded" : "sales.catalog.vatExcluded")}</span>
                      </TableCell>
                      {canManage ? (
                        <TableCell className="pr-5">
                          <div className="flex justify-end gap-1">
                            <Button variant="ghost" size="icon-sm" onClick={() => setEditing(item)} aria-label={t("sales.catalog.edit", { name: item.name })}>
                              <Pencil />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              onClick={() => {
                                if (window.confirm(t("sales.catalog.removeConfirm", { name: item.name })))
                                  remove.mutate(item.id, { onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")) });
                              }}
                              aria-label={t("sales.catalog.remove", { name: item.name })}
                            >
                              <Trash2 />
                            </Button>
                          </div>
                        </TableCell>
                      ) : null}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={editing !== null} onOpenChange={(open) => (open ? null : setEditing(null))}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t(editing === "new" ? "sales.catalog.new" : "sales.catalog.editTitle")}</DialogTitle>
            <DialogDescription>{t("sales.catalog.formHint")}</DialogDescription>
          </DialogHeader>
          {editing !== null ? <CatalogForm key={editing === "new" ? "new" : editing.id} item={editing === "new" ? undefined : editing} onDone={() => setEditing(null)} /> : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

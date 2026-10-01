"use client";

import { AlertCircle, Pencil, Palmtree, UsersRound } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useSession } from "@/features/identity/session";
import { ApiError } from "@/lib/api/errors";
import { formatCalendarDate, formatMoney, initials } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";
import { useEmployees, useUpdateEmployee, type Employee } from "./people-api";

function EmployeeForm({ employee, onDone }: { employee: Employee; onDone: () => void }) {
  const { t, tError } = useI18n();
  const update = useUpdateEmployee(employee.userId);
  const [jobTitle, setJobTitle] = useState(employee.jobTitle ?? "");
  const [department, setDepartment] = useState(employee.department ?? "");
  const [phone, setPhone] = useState(employee.phone ?? "");
  const [startDate, setStartDate] = useState(employee.startDate ?? "");
  const [hourlyCost, setHourlyCost] = useState(employee.hourlyCost?.toString().replace(".", ",") ?? "");
  const [allowance, setAllowance] = useState(String(employee.annualLeaveDays));
  const [error, setError] = useState<string | null>(null);

  return (
    <form
      className="grid gap-4"
      noValidate
      onSubmit={async (event) => {
        event.preventDefault();
        const cost = hourlyCost.trim() === "" ? null : Number(hourlyCost.replace(/\./g, "").replace(",", "."));
        const days = Number(allowance);
        if ((cost !== null && (!Number.isFinite(cost) || cost < 0)) || !Number.isInteger(days) || days < 0 || days > 60) {
          setError(t("validation.invalid_value"));
          return;
        }
        setError(null);
        try {
          await update.mutateAsync({
            jobTitle: jobTitle.trim() || null,
            department: department.trim() || null,
            phone: phone.trim() || null,
            startDate: startDate || null,
            hourlyCost: cost,
            annualLeaveDays: days,
          });
          toast.success(t("people.saved", { name: employee.fullName }));
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
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField id="emp-title" label={t("people.field.jobTitle")} value={jobTitle} onChange={(event) => setJobTitle(event.target.value)} maxLength={100} />
        <FormField id="emp-department" label={t("people.field.department")} value={department} onChange={(event) => setDepartment(event.target.value)} maxLength={100} />
        <FormField id="emp-phone" type="tel" label={t("people.field.phone")} value={phone} onChange={(event) => setPhone(event.target.value)} maxLength={30} />
        <FormField id="emp-start" type="date" label={t("people.field.startDate")} value={startDate} onChange={(event) => setStartDate(event.target.value)} />
        <FormField
          id="emp-cost"
          inputMode="decimal"
          label={t("people.field.hourlyCost")}
          hint={t("people.field.hourlyCostHint")}
          value={hourlyCost}
          onChange={(event) => setHourlyCost(event.target.value)}
        />
        <FormField
          id="emp-allowance"
          type="number"
          min={0}
          max={60}
          label={t("people.field.annualLeaveDays")}
          hint={t("people.field.annualLeaveDaysHint")}
          value={allowance}
          onChange={(event) => setAllowance(event.target.value)}
        />
      </div>
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onDone}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" disabled={update.isPending}>
          {update.isPending ? t("common.saving") : t("common.save")}
        </Button>
      </div>
    </form>
  );
}

/** The team directory; admins edit profiles, cost readers also see hourly costs. */
export function EmployeesTable() {
  const { t, tError } = useI18n();
  const { data: session } = useSession();
  const employees = useEmployees();
  const [editing, setEditing] = useState<Employee | null>(null);
  const canManage = hasPermission(session?.permissions, permissions.people.manage);
  const seesCosts = hasPermission(session?.permissions, permissions.people.costsRead);

  if (employees.isPending) {
    return (
      <div className="grid gap-2 p-5">
        <Skeleton className="h-11 w-full" />
        <Skeleton className="h-11 w-full" />
      </div>
    );
  }

  if (employees.isError) {
    return (
      <div className="p-5">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{tError(employees.error instanceof ApiError ? employees.error.code : "common.network")}</AlertDescription>
        </Alert>
      </div>
    );
  }

  if (employees.data.length === 0) return <EmptyState icon={UsersRound} title={t("people.empty")} />;

  return (
    <>
      <div className="overflow-x-auto">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/60 hover:bg-muted/60">
              <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("people.column.person")}</TableHead>
              <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium md:table-cell">{t("people.field.department")}</TableHead>
              <TableHead className="text-muted-foreground hidden h-10 text-xs font-medium lg:table-cell">{t("people.field.startDate")}</TableHead>
              {seesCosts ? <TableHead className="text-muted-foreground h-10 text-right text-xs font-medium">{t("people.field.hourlyCost")}</TableHead> : null}
              {canManage ? <TableHead className="h-10 w-12 pr-5" /> : null}
            </TableRow>
          </TableHeader>
          <TableBody>
            {employees.data.map((employee) => (
              <TableRow key={employee.userId} className="hover:bg-muted/40">
                <TableCell className="py-3 pl-5">
                  <div className="flex items-center gap-3">
                    <span className="bg-primary-soft text-primary-strong flex size-9 shrink-0 items-center justify-center rounded-full text-xs font-semibold">
                      {initials(employee.fullName)}
                    </span>
                    <div className="grid min-w-0">
                      <span className="flex items-center gap-2 font-medium">
                        {employee.fullName}
                        {employee.onLeaveToday ? (
                          <Badge variant="warning">
                            <Palmtree aria-hidden /> {t("people.onLeave")}
                          </Badge>
                        ) : null}
                      </span>
                      <span className="text-muted-foreground truncate text-xs">{employee.jobTitle ?? "—"}</span>
                    </div>
                  </div>
                </TableCell>
                <TableCell className="text-muted-foreground hidden md:table-cell">{employee.department ?? "—"}</TableCell>
                <TableCell className="text-muted-foreground hidden tabular-nums lg:table-cell">
                  {employee.startDate ? formatCalendarDate(employee.startDate) : "—"}
                </TableCell>
                {seesCosts ? (
                  <TableCell className="text-right tabular-nums">
                    {employee.hourlyCost !== null ? t("people.perHour", { amount: formatMoney(employee.hourlyCost) }) : <span className="text-muted-foreground">—</span>}
                  </TableCell>
                ) : null}
                {canManage ? (
                  <TableCell className="pr-5">
                    <Button variant="ghost" size="icon-sm" onClick={() => setEditing(employee)} aria-label={t("people.edit", { name: employee.fullName })}>
                      <Pencil />
                    </Button>
                  </TableCell>
                ) : null}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <Dialog open={editing !== null} onOpenChange={(open) => (open ? null : setEditing(null))}>
        <DialogContent className="sm:max-w-xl" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{editing?.fullName}</DialogTitle>
            <DialogDescription>{t("people.editDescription")}</DialogDescription>
          </DialogHeader>
          {editing ? <EmployeeForm key={editing.userId} employee={editing} onDone={() => setEditing(null)} /> : null}
        </DialogContent>
      </Dialog>
    </>
  );
}

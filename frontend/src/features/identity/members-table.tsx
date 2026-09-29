"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertCircle } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { RoleName } from "./role-name";

export function MembersTable() {
  const { t, tError } = useI18n();
  const members = useQuery({
    queryKey: ["identity", "members", { page: 1, pageSize: 100 }],
    queryFn: async () =>
      unwrap(await api.GET("/api/v1/identity/members", { params: { query: { page: 1, pageSize: 100 } } })),
  });

  if (members.isPending) {
    return (
      <div className="grid gap-2">
        <Skeleton className="h-9 w-full" />
        <Skeleton className="h-9 w-full" />
      </div>
    );
  }

  if (members.isError) {
    const error = members.error;
    const message =
      error instanceof ApiError && error.status === 403 ? t("team.noPermission") : tError(
        error instanceof ApiError ? error.code : "common.network",
        error instanceof ApiError ? error.params : undefined,
      );

    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>{message}</AlertDescription>
      </Alert>
    );
  }

  if (members.data.items.length === 0) {
    return <p className="text-muted-foreground text-sm">{t("team.empty")}</p>;
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("team.column.name")}</TableHead>
          <TableHead>{t("team.column.email")}</TableHead>
          <TableHead>{t("team.column.role")}</TableHead>
          <TableHead className="text-right">{t("team.column.joinedAt")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {members.data.items.map((member) => (
          <TableRow key={member.userId}>
            <TableCell className="font-medium">{member.fullName}</TableCell>
            <TableCell className="text-muted-foreground">{member.email}</TableCell>
            <TableCell>
              <Badge variant="secondary">
                <RoleName role={member.role} />
              </Badge>
            </TableCell>
            <TableCell className="text-right tabular-nums">{formatDate(member.joinedAt)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

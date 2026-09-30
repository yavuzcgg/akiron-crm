"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertCircle, Users } from "lucide-react";
import { EmptyState } from "@/components/empty-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatDate, initials } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { roleLabel } from "./role-name";

const roleBadge = { owner: "brand", admin: "info" } as const;

export function MembersTable() {
  const { t, tError } = useI18n();
  const members = useQuery({
    queryKey: ["identity", "members", { page: 1, pageSize: 100 }],
    queryFn: async () =>
      unwrap(await api.GET("/api/v1/identity/members", { params: { query: { page: 1, pageSize: 100 } } })),
  });

  if (members.isPending) {
    return (
      <div className="grid gap-2 p-5">
        <Skeleton className="h-11 w-full" />
        <Skeleton className="h-11 w-full" />
      </div>
    );
  }

  if (members.isError) {
    const error = members.error;
    const message =
      error instanceof ApiError && error.status === 403
        ? t("team.noPermission")
        : tError(error instanceof ApiError ? error.code : "common.network", error instanceof ApiError ? error.params : undefined);

    return (
      <div className="p-5">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      </div>
    );
  }

  if (members.data.items.length === 0) {
    return <EmptyState icon={Users} title={t("team.empty")} />;
  }

  return (
    <Table>
      <TableHeader>
        <TableRow className="bg-muted/60 hover:bg-muted/60">
          <TableHead className="text-muted-foreground h-10 pl-5 text-xs font-medium">{t("team.column.name")}</TableHead>
          <TableHead className="text-muted-foreground h-10 text-xs font-medium">{t("team.column.role")}</TableHead>
          <TableHead className="text-muted-foreground h-10 pr-5 text-right text-xs font-medium">{t("team.column.joinedAt")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {members.data.items.map((member) => (
          <TableRow key={member.userId} className="hover:bg-muted/40">
            <TableCell className="py-3 pl-5">
              <div className="flex items-center gap-3">
                <Avatar className="size-9">
                  <AvatarFallback className="bg-primary-soft text-primary-strong text-xs font-semibold">
                    {initials(member.fullName)}
                  </AvatarFallback>
                </Avatar>
                <div className="grid min-w-0">
                  <span className="truncate font-medium">{member.fullName}</span>
                  <span className="text-muted-foreground truncate text-xs">{member.email}</span>
                </div>
              </div>
            </TableCell>
            <TableCell>
              <Badge variant={roleBadge[member.role as keyof typeof roleBadge] ?? "secondary"}>{roleLabel(member.role, t)}</Badge>
            </TableCell>
            <TableCell className="text-muted-foreground tabular pr-5 text-right">{formatDate(member.joinedAt)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

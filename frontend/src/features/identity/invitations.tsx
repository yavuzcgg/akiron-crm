"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, X } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { formatDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { applyApiError } from "./form-errors";
import { roleLabel } from "./role-name";

const invitationsKey = ["identity", "invitations"] as const;

/** Roles an invitation can grant; ownership is transferred, never invited (API rule). */
const invitableRoles = ["member", "admin"] as const;

const fields = ["email", "role"] as const;

export function InviteForm() {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);

  const invite = useMutation({
    mutationFn: async (body: { email: string; role: string }) =>
      unwrap(await api.POST("/api/v1/identity/invitations", { body })),
    onSuccess: (invitation) => {
      toast.success(t("team.invite.sent", { email: invitation.email }));
      void queryClient.invalidateQueries({ queryKey: invitationsKey });
      refreshTimelineSoon(queryClient);
    },
  });

  const schema = useMemo(
    () =>
      z.object({
        email: z.string().trim().min(1, t("validation.required")).email(t("validation.email")),
        role: z.enum(invitableRoles),
      }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", role: "member" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await invite.mutateAsync(values);
      form.reset({ email: "", role: values.role });
    } catch (error) {
      setFormError(applyApiError(error, fields, form.setError, tError));
    }
  });

  return (
    <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-[1fr_10rem_auto] sm:items-end" noValidate>
      {formError ? (
        <Alert variant="destructive" className="sm:col-span-3">
          <AlertCircle />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      ) : null}
      <FormField
        id="invite-email"
        type="email"
        autoComplete="off"
        label={t("team.invite.email")}
        error={form.formState.errors.email?.message}
        {...form.register("email")}
      />
      <div className="grid gap-1.5">
        <Label htmlFor="invite-role">{t("team.invite.role")}</Label>
        <select
          id="invite-role"
          className="border-input bg-background h-8 rounded-lg border px-2.5 text-sm"
          {...form.register("role")}
        >
          {invitableRoles.map((role) => (
            <option key={role} value={role}>
              {roleLabel(role, t)}
            </option>
          ))}
        </select>
      </div>
      <Button type="submit" disabled={invite.isPending}>
        {invite.isPending ? t("common.loading") : t("team.invite.submit")}
      </Button>
    </form>
  );
}

export function PendingInvitations({ canManage }: { canManage: boolean }) {
  const { t } = useI18n();
  const queryClient = useQueryClient();

  const invitations = useQuery({
    queryKey: invitationsKey,
    queryFn: async () => unwrap(await api.GET("/api/v1/identity/invitations")),
  });

  const revoke = useMutation({
    mutationFn: async (id: string) => {
      unwrap(await api.DELETE("/api/v1/identity/invitations/{id}", { params: { path: { id } } }));
    },
    onSuccess: () => {
      toast.success(t("team.invitations.revoked"));
      void queryClient.invalidateQueries({ queryKey: invitationsKey });
    },
  });

  if (invitations.isPending) return <Skeleton className="h-10 w-full" />;
  if (invitations.isError || invitations.data.length === 0) {
    return <p className="text-muted-foreground text-sm">{t("team.invitations.empty")}</p>;
  }

  return (
    <ul className="divide-border divide-y">
      {invitations.data.map((invitation) => (
        <li key={invitation.id} className="flex items-center gap-3 py-2.5">
          <div className="grid flex-1 gap-0.5">
            <span className="text-sm font-medium">{invitation.email}</span>
            <span className="text-muted-foreground text-xs">
              {invitation.status === "expired"
                ? t("team.invitations.expired")
                : t("team.invitations.expires", { date: formatDate(invitation.expiresAt) })}
            </span>
          </div>
          <Badge variant="secondary">{roleLabel(invitation.role, t)}</Badge>
          {canManage ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => revoke.mutate(invitation.id)}
              disabled={revoke.isPending}
              aria-label={t("team.invitations.revoke")}
            >
              <X /> {t("team.invitations.revoke")}
            </Button>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
